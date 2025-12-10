using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

partial struct BeeFlyingSystem : ISystem
{
    private EntityQuery _flowerQuery;
    private EntityQuery _hiveQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationConfig>();

        _flowerQuery = SystemAPI.QueryBuilder()
            .WithAll<TravellingToFlower, LocalTransform, BeeData, FlightPath, PhysicsVelocity>()
            .Build();

        _hiveQuery = SystemAPI.QueryBuilder()
            .WithAll<TravellingToHome, LocalTransform, BeeData, FlightPath, PhysicsVelocity>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;

        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();

        var deltaTime = (float)SystemAPI.Time.DeltaTime;

        switch (config.executionMode)
        {
            case ExecutionMode.Scheduled:
            {
                // Create separate ECBs for each job to allow parallel execution
                var ecb1 = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
                var ecb2 = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

                var flyToFlowerJob = new BeeToFlowerChunkJob
                {
                    ecb = ecb1,
                    deltaTime = deltaTime,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                    BeeDataHandle = SystemAPI.GetComponentTypeHandle<BeeData>(),
                    FlightPathHandle = SystemAPI.GetComponentTypeHandle<FlightPath>(),
                    VelocityHandle = SystemAPI.GetComponentTypeHandle<PhysicsVelocity>(),
                    EntityHandle = SystemAPI.GetEntityTypeHandle()
                }.Schedule(_flowerQuery, state.Dependency);

                var flyToHiveJob = new BeeToHiveChunkJob
                {
                    ecb = ecb2,
                    deltaTime = deltaTime,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                    BeeDataHandle = SystemAPI.GetComponentTypeHandle<BeeData>(),
                    FlightPathHandle = SystemAPI.GetComponentTypeHandle<FlightPath>(),
                    VelocityHandle = SystemAPI.GetComponentTypeHandle<PhysicsVelocity>(),
                    EntityHandle = SystemAPI.GetEntityTypeHandle()
                }.Schedule(_hiveQuery, state.Dependency);

                JobHandle.CombineDependencies(flyToFlowerJob, flyToHiveJob).Complete();
            } break;

            case ExecutionMode.ScheduledParallel:
            {
                // Create separate ECBs for each job to allow parallel execution
                var ecb1 = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
                var ecb2 = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

                var flyToFlowerJob = new BeeToFlowerChunkJob
                {
                    ecb = ecb1,
                    deltaTime = deltaTime,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                    BeeDataHandle = SystemAPI.GetComponentTypeHandle<BeeData>(),
                    FlightPathHandle = SystemAPI.GetComponentTypeHandle<FlightPath>(),
                    VelocityHandle = SystemAPI.GetComponentTypeHandle<PhysicsVelocity>(),
                    EntityHandle = SystemAPI.GetEntityTypeHandle()
                }.ScheduleParallel(_flowerQuery, state.Dependency);

                var flyToHiveJob = new BeeToHiveChunkJob
                {
                    ecb = ecb2,
                    deltaTime = deltaTime,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                    BeeDataHandle = SystemAPI.GetComponentTypeHandle<BeeData>(),
                    FlightPathHandle = SystemAPI.GetComponentTypeHandle<FlightPath>(),
                    VelocityHandle = SystemAPI.GetComponentTypeHandle<PhysicsVelocity>(),
                    EntityHandle = SystemAPI.GetEntityTypeHandle()
                }.ScheduleParallel(_hiveQuery, state.Dependency);

                JobHandle.CombineDependencies(flyToFlowerJob, flyToHiveJob).Complete();
            } break;

            case ExecutionMode.MainThread:
            {
                var ecbSingleThread =
                    SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged);

                // Single consolidated loop for all travelling bees (better cache utilization)
                foreach (var (trans, bee, flightPath, velocity, travelToFlower, entity) in
                         SystemAPI.Query<RefRW<LocalTransform>, RefRW<BeeData>, RefRW<FlightPath>,
                                 RefRW<PhysicsVelocity>, EnabledRefRO<TravellingToFlower>>()
                             .WithAny<TravellingToFlower, TravellingToHome>()
                             .WithEntityAccess())
                {
                    var reachedDest = BeeFlyingSystem.TravelBee(
                        ref trans.ValueRW, in bee.ValueRO, ref flightPath.ValueRW,
                        deltaTime, ref velocity.ValueRW);

                    if (reachedDest)
                    {
                        if (travelToFlower.ValueRO)
                        {
                            ecbSingleThread.SetComponentEnabled<TravellingToFlower>(entity, false);
                            ecbSingleThread.SetComponentEnabled<AtFlower>(entity, true);
                        }
                        else
                        {
                            ecbSingleThread.SetComponentEnabled<TravellingToHome>(entity, false);
                            ecbSingleThread.SetComponentEnabled<AtHive>(entity, true);
                        }
                    }
                }
            } break;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    // All bees have mass=5, so InverseMass=0.2f - avoid per-entity PhysicsMass lookup
    private const float BeeInverseMass = 0.2f;

    [BurstCompile]
    public static bool TravelBee(ref LocalTransform trans, in BeeData bee, ref FlightPath flightPath, float deltaTime, ref PhysicsVelocity velocity)
    {
        var between = flightPath.to - trans.Position;
        var distanceSq = math.lengthsq(between);

        if (distanceSq <= 1f)
        {
            return true;
        }

        flightPath.time += deltaTime;

        // Use rsqrt (single instruction) instead of sqrt + division
        var invDistance = math.rsqrt(distanceSq);
        var distance = distanceSq * invDistance;  // distanceSq / sqrt(distanceSq) = sqrt(distanceSq)
        var direction = between * invDistance;
        var straightVel = direction * bee.speed * 5f;

        // Only compute wiggle when distance >= 2f (skip expensive sin/cos/normalize when near destination)
        if (distance >= 2f)
        {
            var orthogonal = math.normalize(math.cross(direction, math.up()));
            var wiggleFactor = deltaTime * BeeInverseMass;
            var verticalWiggle = math.sin(flightPath.time * 7f) * 50f * wiggleFactor;
            var horizontalWiggle = math.cos(flightPath.time * 15f) * 125f * wiggleFactor;

            velocity.Linear += math.up() * verticalWiggle + orthogonal * horizontalWiggle;
        }

        var desiredVel = straightVel;
        var lerpFactor = math.saturate(deltaTime * 1.5f);

        // Compute fromDistanceSq once, reuse distanceSq we already have
        var fromDistanceSq = math.lengthsq(flightPath.from - trans.Position);
        if (fromDistanceSq < distanceSq)
        {
            // Use rsqrt instead of 1/sqrt for lift calculation
            desiredVel += math.up() * math.min(100f, 10f * math.rsqrt(fromDistanceSq));
        }

        velocity.Angular = float3.zero;
        velocity.Linear = math.lerp(velocity.Linear, desiredVel, lerpFactor);

        // Make the bee face its movement direction (nlerp is much faster than slerp, visually similar for small angles)
        var targetRotation = quaternion.LookRotationSafe(direction, math.up());
        var t = lerpFactor * 6.67f;
        trans.Rotation = math.normalize(new quaternion(math.lerp(trans.Rotation.value, targetRotation.value, t)));

        return false;
    }
}

[BurstCompile]
public struct BeeToFlowerChunkJob : IJobChunk
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<LocalTransform> TransformHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<BeeData> BeeDataHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<FlightPath> FlightPathHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public EntityTypeHandle EntityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var transforms = chunk.GetNativeArray(ref TransformHandle);
        var bees = chunk.GetNativeArray(ref BeeDataHandle);
        var flightPaths = chunk.GetNativeArray(ref FlightPathHandle);
        var velocities = chunk.GetNativeArray(ref VelocityHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out int i))
        {
            var trans = transforms[i];
            var bee = bees[i];
            var flightPath = flightPaths[i];
            var velocity = velocities[i];

            var reachedDest = BeeFlyingSystem.TravelBee(ref trans, in bee, ref flightPath, deltaTime, ref velocity);

            transforms[i] = trans;
            flightPaths[i] = flightPath;
            velocities[i] = velocity;

            if (reachedDest)
            {
                ecb.SetComponentEnabled<TravellingToFlower>(unfilteredChunkIndex, entities[i], false);
                ecb.SetComponentEnabled<AtFlower>(unfilteredChunkIndex, entities[i], true);
            }
        }
    }
}

[BurstCompile]
public struct BeeToHiveChunkJob : IJobChunk
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<LocalTransform> TransformHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<BeeData> BeeDataHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<FlightPath> FlightPathHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public EntityTypeHandle EntityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var transforms = chunk.GetNativeArray(ref TransformHandle);
        var bees = chunk.GetNativeArray(ref BeeDataHandle);
        var flightPaths = chunk.GetNativeArray(ref FlightPathHandle);
        var velocities = chunk.GetNativeArray(ref VelocityHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out int i))
        {
            var trans = transforms[i];
            var bee = bees[i];
            var flightPath = flightPaths[i];
            var velocity = velocities[i];

            var reachedDest = BeeFlyingSystem.TravelBee(ref trans, in bee, ref flightPath, deltaTime, ref velocity);

            transforms[i] = trans;
            flightPaths[i] = flightPath;
            velocities[i] = velocity;

            if (reachedDest)
            {
                ecb.SetComponentEnabled<TravellingToHome>(unfilteredChunkIndex, entities[i], false);
                ecb.SetComponentEnabled<AtHive>(unfilteredChunkIndex, entities[i], true);
            }
        }
    }
}
