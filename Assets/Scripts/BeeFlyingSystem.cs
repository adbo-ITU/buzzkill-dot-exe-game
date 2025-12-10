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
            .WithAll<TravellingToFlower, LocalTransform, BeeData, FlightPath, PhysicsVelocity, PhysicsMass>()
            .Build();

        _hiveQuery = SystemAPI.QueryBuilder()
            .WithAll<TravellingToHome, LocalTransform, BeeData, FlightPath, PhysicsVelocity, PhysicsMass>()
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
                    MassHandle = SystemAPI.GetComponentTypeHandle<PhysicsMass>(true),
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
                    MassHandle = SystemAPI.GetComponentTypeHandle<PhysicsMass>(true),
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
                    MassHandle = SystemAPI.GetComponentTypeHandle<PhysicsMass>(true),
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
                    MassHandle = SystemAPI.GetComponentTypeHandle<PhysicsMass>(true),
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
                foreach (var (trans, bee, flightPath, velocity, mass, travelToFlower, entity) in
                         SystemAPI.Query<RefRW<LocalTransform>, RefRW<BeeData>, RefRW<FlightPath>,
                                 RefRW<PhysicsVelocity>, RefRO<PhysicsMass>, EnabledRefRO<TravellingToFlower>>()
                             .WithAny<TravellingToFlower, TravellingToHome>()
                             .WithEntityAccess())
                {
                    var reachedDest = BeeFlyingSystem.TravelBee(
                        ref trans.ValueRW, ref bee.ValueRW, ref flightPath.ValueRW,
                        deltaTime, ref velocity.ValueRW, in mass.ValueRO);

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

    [BurstCompile]
    public static bool TravelBee(ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, float deltaTime, ref PhysicsVelocity velocity, in PhysicsMass mass)
    {
        var between = flightPath.to - trans.Position;
        var distanceSq = math.lengthsq(between);

        if (distanceSq <= 1f)
        {
            return true;
        }

        flightPath.time += deltaTime;

        // Single sqrt, then derive direction via multiplication instead of second sqrt
        var distance = math.sqrt(distanceSq);
        var invDistance = 1f / distance;
        var direction = between * invDistance;
        var straightVel = direction * bee.speed * 5f;

        // Only compute wiggle when distance >= 2f (skip expensive sin/cos/normalize when near destination)
        if (distance >= 2f)
        {
            var orthogonal = math.normalize(math.cross(direction, math.up()));
            var wiggleFactor = deltaTime * mass.InverseMass;
            var verticalWiggle = math.sin(flightPath.time * 7f) * 50f * wiggleFactor;
            var horizontalWiggle = math.cos(flightPath.time * 15f) * 125f * wiggleFactor;

            velocity.Linear += math.up() * verticalWiggle + orthogonal * horizontalWiggle;
        }

        var desiredVel = straightVel;
        var lerpFactor = math.saturate(deltaTime * 1.5f);

        if (math.distancesq(flightPath.from, trans.Position) < math.distancesq(flightPath.to, trans.Position))
        {
            desiredVel += math.up() * math.min(100f, 10f / math.distance(flightPath.from, trans.Position));
        }

        velocity.Angular = float3.zero;
        velocity.Linear = math.lerp(velocity.Linear, desiredVel, lerpFactor);

        // Make the bee face its movement direction
        var targetRotation = quaternion.LookRotationSafe(direction, math.up());
        trans.Rotation = math.slerp(trans.Rotation, targetRotation, lerpFactor * 6.67f);

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
    public ComponentTypeHandle<PhysicsMass> MassHandle;
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public EntityTypeHandle EntityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var transforms = chunk.GetNativeArray(ref TransformHandle);
        var bees = chunk.GetNativeArray(ref BeeDataHandle);
        var flightPaths = chunk.GetNativeArray(ref FlightPathHandle);
        var velocities = chunk.GetNativeArray(ref VelocityHandle);
        var masses = chunk.GetNativeArray(ref MassHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out int i))
        {
            var trans = transforms[i];
            var bee = bees[i];
            var flightPath = flightPaths[i];
            var velocity = velocities[i];
            var mass = masses[i];

            var reachedDest = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime, ref velocity, in mass);

            transforms[i] = trans;
            bees[i] = bee;
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
    public ComponentTypeHandle<PhysicsMass> MassHandle;
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public EntityTypeHandle EntityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var transforms = chunk.GetNativeArray(ref TransformHandle);
        var bees = chunk.GetNativeArray(ref BeeDataHandle);
        var flightPaths = chunk.GetNativeArray(ref FlightPathHandle);
        var velocities = chunk.GetNativeArray(ref VelocityHandle);
        var masses = chunk.GetNativeArray(ref MassHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out int i))
        {
            var trans = transforms[i];
            var bee = bees[i];
            var flightPath = flightPaths[i];
            var velocity = velocities[i];
            var mass = masses[i];

            var reachedDest = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime, ref velocity, in mass);

            transforms[i] = trans;
            bees[i] = bee;
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
