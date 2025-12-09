using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(BeeFlyingSystem))] 
[UpdateAfter(typeof(BeeAtHiveSystem))]
partial struct BeeAtFlowerSystem : ISystem
{
    private ComponentLookup<FlowerData> _flowerLookup;
    private ComponentLookup<HiveData> _hiveLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationConfig>();
        state.RequireForUpdate<FlowerManager>();
        _flowerLookup = state.GetComponentLookup<FlowerData>(isReadOnly: false);
        _hiveLookup = state.GetComponentLookup<HiveData>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        _flowerLookup.Update(ref state);
        _hiveLookup.Update(ref state);
        
        var flowerManager = SystemAPI.GetSingleton<FlowerManager>();
        
        switch (config.executionMode)
        {
            case ExecutionMode.Scheduled:
            {
                var atFlowerJob = new BeeAtFlowerJob
                {
                    time = SystemAPI.Time.ElapsedTime,
                    deltaTime = SystemAPI.Time.DeltaTime,
                    flowerLookup = _flowerLookup,
                    hiveLookup = _hiveLookup,
                    ecb = ecb,
                    flowerManager = flowerManager
                }.Schedule(state.Dependency);

                atFlowerJob.Complete();
            } break;

            case ExecutionMode.ScheduledParallel:
            {
                var atFlowerJob = new BeeAtFlowerJob
                {
                    time = SystemAPI.Time.ElapsedTime,
                    deltaTime = SystemAPI.Time.DeltaTime,
                    flowerLookup = _flowerLookup,
                    hiveLookup = _hiveLookup,
                    ecb = ecb,
                    flowerManager = flowerManager
                }.ScheduleParallel(state.Dependency);

                atFlowerJob.Complete();
            } break;
            
            case ExecutionMode.MainThread: {
                var ecbSingleThread =
                    SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged);
                
                var flowerLookup = SystemAPI.GetComponentLookup<FlowerData>(false);
                var hiveLookup = SystemAPI.GetComponentLookup<HiveData>(true);
                
                var deltaTime = SystemAPI.Time.DeltaTime;
                var time = SystemAPI.Time.ElapsedTime;
                
                foreach (var (trans, bee, velocity, mass, entity) in 
                         SystemAPI.Query<RefRW<LocalTransform>, RefRW<BeeData>, RefRW<PhysicsVelocity>, RefRO<PhysicsMass>>()
                             .WithAll<AtFlower>()
                             .WithEntityAccess())
                {
                    var targetFlower = bee.ValueRO.targetFlower;
                    if (targetFlower == Entity.Null) continue;

                    var flower = flowerLookup.GetRefRW(targetFlower);

                    var between = flower.ValueRO.position - trans.ValueRO.Position;
                    var dist = math.length(between);
                    
                    if (dist > 1f)
                    {
                        velocity.ValueRW.ApplyImpulse(
                            mass.ValueRO,
                            float3.zero,
                            quaternion.identity,
                            math.normalizesafe(between) * dist,
                            trans.ValueRO.Position);
                    }
                    else
                    {
                        velocity.ValueRW.Linear = float3.zero;
                    }

                    velocity.ValueRW.Angular = math.float3(2f, 2f, 2f);

                    var maxNectarToTake = 5f * deltaTime;
                    var nectarBeeCanTake = math.min(maxNectarToTake, bee.ValueRO.nectarCapacity - bee.ValueRO.nectarCarried);

                    if (flower.ValueRO.nectarAmount > 0)
                    {
                        var nectarTaken = math.min(flower.ValueRO.nectarAmount, nectarBeeCanTake);
                        flower.ValueRW.nectarAmount -= nectarTaken;
                        bee.ValueRW.nectarCarried += nectarTaken;
                    }

                    var flowerIsEmpty = flower.ValueRO.nectarAmount <= 0.01;
                    var beeIsSaturated = bee.ValueRO.nectarCapacity - bee.ValueRO.nectarCarried <= 0.01;

                    if (!flowerIsEmpty && !beeIsSaturated) continue;

                    ecbSingleThread.RemoveComponent<AtFlower>(entity);
                    float3 to;
                    if (beeIsSaturated)
                    {
                        var hive = hiveLookup.GetRefRO(bee.ValueRO.homeHive);
                        bee.ValueRW.targetFlower = Entity.Null;
                        to = hive.ValueRO.position;
                        ecbSingleThread.AddComponent<TravellingToHome>(entity);
                    }
                    else
                    {
                        var rng = BeeData.GetRng(time, entity);
                        var (flowerEntity, flowerData) = flowerManager.GetRandomFlower(ref rng);
                        bee.ValueRW.targetFlower = flowerEntity;
                        to = flowerData.position;
                        ecbSingleThread.AddComponent<TravellingToFlower>(entity);
                    }

                    ecbSingleThread.AddComponent(entity, new FlightPath()
                    {
                        time = 0,
                        from = trans.ValueRO.Position,
                        to = to,
                        position = trans.ValueRO.Position
                    });
                }
            } break;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct BeeAtFlowerJob : IJobEntity
{
    public double time;
    public float deltaTime;
    public ComponentLookup<FlowerData> flowerLookup;
    [ReadOnly] public ComponentLookup<HiveData> hiveLookup;
    [ReadOnly] public FlowerManager flowerManager;
    
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, in AtFlower atFlower, ref BeeData bee,
        ref PhysicsVelocity velocity,
        in PhysicsMass mass
        )
    {
        // var rot = math.mul(quaternion.RotateZ(2f * deltaTime), quaternion.RotateY(2f * deltaTime));
        // trans.Rotation = math.mul(trans.Rotation, rot);
        
        var targetFlower = bee.targetFlower;
        if (targetFlower == Entity.Null) return;

        var flower = flowerLookup.GetRefRW(targetFlower);

        var between = flower.ValueRO.position - trans.Position;
        var dist = math.length(between);
        
        if (dist > 1f)
        {
            velocity.ApplyImpulse(
                mass,
                float3.zero,
                quaternion.identity,
                math.normalizesafe(between) * dist,
                trans.Position);
        }
        else
        {
            velocity.Linear = float3.zero;
        }

        velocity.Angular = math.float3(2f, 2f, 2f);

        var maxNectarToTake = 5f * deltaTime;
        var nectarBeeCanTake = math.min(maxNectarToTake, bee.nectarCapacity - bee.nectarCarried);

        if (flower.ValueRW.nectarAmount > 0)
        {
            var nectarTaken = math.min(flower.ValueRW.nectarAmount, nectarBeeCanTake);
            flower.ValueRW.nectarAmount -= nectarTaken;
            bee.nectarCarried += nectarTaken;
        }

        var flowerIsEmpty = flower.ValueRW.nectarAmount <= 0.01;
        var beeIsSaturated = bee.nectarCapacity - bee.nectarCarried <= 0.01;

        if (!flowerIsEmpty && !beeIsSaturated) return;

        ecb.RemoveComponent<AtFlower>(chunkKey, entity);
        float3 to;
        if (beeIsSaturated)
        {
            var hive = hiveLookup.GetRefRO(bee.homeHive);
            bee.targetFlower = Entity.Null;
            to = hive.ValueRO.position;
            ecb.AddComponent<TravellingToHome>(chunkKey, entity);
        }
        else
        {
            var rng = BeeData.GetRng(time, entity);
            var (flowerEntity, flowerData) = flowerManager.GetRandomFlower(ref rng);
            bee.targetFlower = flowerEntity;
            to = flowerData.position;
            ecb.AddComponent<TravellingToFlower>(chunkKey, entity);
        }

        ecb.AddComponent(chunkKey, entity, new FlightPath()
        {
            time = 0,
            from = trans.Position,
            to = to,
            position = trans.Position
        });
    }
}