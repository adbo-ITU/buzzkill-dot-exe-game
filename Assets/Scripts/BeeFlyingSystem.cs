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
        state.RequireForUpdate<CameraData>();

        _flowerQuery = SystemAPI.QueryBuilder()
            .WithAll<TravellingToFlower, LocalTransform, FlightPath, PhysicsVelocity>()
            .Build();

        _hiveQuery = SystemAPI.QueryBuilder()
            .WithAll<TravellingToHome, LocalTransform, FlightPath, PhysicsVelocity>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        var cameraPos = SystemAPI.GetSingleton<CameraData>().position;

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
                    cameraPosition = cameraPos,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                    FlightPathHandle = SystemAPI.GetComponentTypeHandle<FlightPath>(),
                    VelocityHandle = SystemAPI.GetComponentTypeHandle<PhysicsVelocity>(),
                    EntityHandle = SystemAPI.GetEntityTypeHandle()
                }.Schedule(_flowerQuery, state.Dependency);

                var flyToHiveJob = new BeeToHiveChunkJob
                {
                    ecb = ecb2,
                    deltaTime = deltaTime,
                    cameraPosition = cameraPos,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
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
                    cameraPosition = cameraPos,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                    FlightPathHandle = SystemAPI.GetComponentTypeHandle<FlightPath>(),
                    VelocityHandle = SystemAPI.GetComponentTypeHandle<PhysicsVelocity>(),
                    EntityHandle = SystemAPI.GetEntityTypeHandle()
                }.ScheduleParallel(_flowerQuery, state.Dependency);

                var flyToHiveJob = new BeeToHiveChunkJob
                {
                    ecb = ecb2,
                    deltaTime = deltaTime,
                    cameraPosition = cameraPos,
                    TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
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
                foreach (var (trans, flightPath, velocity, travelToFlower, entity) in
                         SystemAPI.Query<RefRW<LocalTransform>, RefRW<FlightPath>,
                                 RefRW<PhysicsVelocity>, EnabledRefRO<TravellingToFlower>>()
                             .WithAny<TravellingToFlower, TravellingToHome>()
                             .WithEntityAccess())
                {
                    var reachedDest = BeeFlyingSystem.TravelBee(
                        ref trans.ValueRW, ref flightPath.ValueRW,
                        deltaTime, ref velocity.ValueRW, cameraPos);

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

    private const float CameraLodDistance = 100f;
    private const float CameraLodDistanceSq = 100f * 100f;

    [BurstCompile]
    public static bool TravelBee(ref LocalTransform trans, ref FlightPath flightPath, float deltaTime, ref PhysicsVelocity velocity, in float3 cameraPos)
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
        var straightVel = direction * flightPath.speed * 5f;

        var camDistanceSq = math.lengthsq(trans.Position - cameraPos);
        var isNearCamera = camDistanceSq < CameraLodDistanceSq;

        // Only compute wiggle when close to camera AND distance to target is large
        if (isNearCamera && distance >= 2f)
        {
            var orthogonal = math.normalize(math.cross(direction, math.up()));
            var wiggleFactor = deltaTime * BeeInverseMass;
            math.sincos(flightPath.time * 7f, out var sinVertical, out var cosVertical);
            var cos14 = cosVertical * cosVertical - sinVertical * sinVertical;
            var horizontalWiggle = cos14 * 125f * wiggleFactor;
            var verticalWiggle = sinVertical * 50f * wiggleFactor;

            velocity.Linear += math.up() * verticalWiggle + orthogonal * horizontalWiggle;
        }

        var desiredVel = straightVel;
        var lerpFactor = math.saturate(deltaTime * 1.5f);

        // Branchless lift calculation - use math.select to avoid branch misprediction
        var fromDistanceSq = math.lengthsq(flightPath.from - trans.Position);
        var liftAmount = math.min(100f, 10f * math.rsqrt(math.max(fromDistanceSq, 0.0001f)));
        var applyLift = math.select(0f, 1f, fromDistanceSq < distanceSq);
        desiredVel += math.up() * liftAmount * applyLift;

        velocity.Angular = float3.zero;
        velocity.Linear = math.lerp(velocity.Linear, desiredVel, lerpFactor);

        // Only update rotation when close to camera (nlerp is much faster than slerp, visually similar for small angles)
        if (isNearCamera)
        {
            var targetRotation = quaternion.LookRotationSafe(direction, math.up());
            var t = lerpFactor * 6.67f;
            trans.Rotation = math.normalize(new quaternion(math.lerp(trans.Rotation.value, targetRotation.value, t)));
        }

        return false;
    }
}

[BurstCompile]
public struct BeeToFlowerChunkJob : IJobChunk
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;
    public float3 cameraPosition;

    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<LocalTransform> TransformHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<FlightPath> FlightPathHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public EntityTypeHandle EntityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var transforms = chunk.GetNativeArray(ref TransformHandle);
        var flightPaths = chunk.GetNativeArray(ref FlightPathHandle);
        var velocities = chunk.GetNativeArray(ref VelocityHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out int i))
        {
            var trans = transforms[i];
            var flightPath = flightPaths[i];
            var velocity = velocities[i];

            var reachedDest = BeeFlyingSystem.TravelBee(ref trans, ref flightPath, deltaTime, ref velocity, cameraPosition);

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
    public float3 cameraPosition;

    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<LocalTransform> TransformHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<FlightPath> FlightPathHandle;
    [NativeDisableContainerSafetyRestriction]
    public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
    [ReadOnly, NativeDisableContainerSafetyRestriction]
    public EntityTypeHandle EntityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var transforms = chunk.GetNativeArray(ref TransformHandle);
        var flightPaths = chunk.GetNativeArray(ref FlightPathHandle);
        var velocities = chunk.GetNativeArray(ref VelocityHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out int i))
        {
            var trans = transforms[i];
            var flightPath = flightPaths[i];
            var velocity = velocities[i];

            var reachedDest = BeeFlyingSystem.TravelBee(ref trans, ref flightPath, deltaTime, ref velocity, cameraPosition);

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
