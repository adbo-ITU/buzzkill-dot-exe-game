using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(BeeFlyingSystem))] 
partial struct BeeAtHiveSystem : ISystem
{
    [NativeDisableParallelForRestriction] private ComponentLookup<HiveData> _hiveLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationConfig>();
        state.RequireForUpdate<FlowerManager>();
        _hiveLookup = state.GetComponentLookup<HiveData>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;

        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        _hiveLookup.Update(ref state);
        
        var flowerManager = SystemAPI.GetSingleton<FlowerManager>();

        switch (config.executionMode)
        {
            case ExecutionMode.Scheduled:
            {
                var atHiveJob = new BeeAtHiveJob
                {
                    time = SystemAPI.Time.ElapsedTime,
                    deltaTime = SystemAPI.Time.DeltaTime,
                    hiveLookup = _hiveLookup,
                    ecb = ecb,
                    flowerManager = flowerManager
                }.Schedule(state.Dependency);

                state.Dependency = atHiveJob;
            }
                break;

            case ExecutionMode.ScheduledParallel:
            {
                var atHiveJob = new BeeAtHiveJob
                {
                    time = SystemAPI.Time.ElapsedTime,
                    deltaTime = SystemAPI.Time.DeltaTime,
                    hiveLookup = _hiveLookup,
                    ecb = ecb,
                    flowerManager = flowerManager
                }.ScheduleParallel(state.Dependency);

                state.Dependency = atHiveJob;
            }
                break;

            case ExecutionMode.MainThread:
            {
                var ecbSingleThread =
                    SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged);

                var hiveLookup = SystemAPI.GetComponentLookup<HiveData>(false);
                var deltaTime = SystemAPI.Time.DeltaTime;
                var time = SystemAPI.Time.ElapsedTime;

                foreach (var (trans, bee, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>, RefRW<BeeData>>()
                             .WithAll<AtHive>()
                             .WithEntityAccess())
                {
                    var hive = bee.ValueRO.homeHive;
                    if (!hiveLookup.HasComponent(hive)) continue;
                    var hiveData = hiveLookup[hive];

                    var maxNectarToGive = 5f * deltaTime;
                    var nectarGiven = math.min(bee.ValueRO.nectarCarried, maxNectarToGive);
                    bee.ValueRW.nectarCarried -= nectarGiven;
                    hiveData.nectarAmount += nectarGiven;
                    hiveLookup[hive] = hiveData;

                    var beeIsDepleted = bee.ValueRO.nectarCarried <= 0.01;
                    if (!beeIsDepleted) continue;

                    ecbSingleThread.SetComponentEnabled<AtHive>(entity, false);

                    var rng = BeeData.GetRng(time, entity);
                    var (flowerEntity, flowerData) = flowerManager.GetRandomFlower(ref rng);
                    bee.ValueRW.targetFlower = flowerEntity;
                    ecbSingleThread.SetComponentEnabled<TravellingToFlower>(entity, true);
                    ecbSingleThread.AddComponent(entity, new FlightPath()
                    {
                        time = 0,
                        from = trans.ValueRO.Position,
                        to = flowerData.position,
                    });
                }
            }
                break;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct BeeAtHiveJob : IJobEntity
{
    public double time;
    public float deltaTime;
    [NativeDisableParallelForRestriction] public ComponentLookup<HiveData> hiveLookup;
    public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public FlowerManager flowerManager;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, in AtHive atHive, ref BeeData bee)
    {
        var hive = bee.homeHive;
        if (!hiveLookup.HasComponent(hive)) return;
        var hiveData = hiveLookup[hive];
        
        var maxNectarToGive = 5f * deltaTime;
        var nectarGiven = math.min(bee.nectarCarried, maxNectarToGive);
        bee.nectarCarried -= nectarGiven;
        hiveData.nectarAmount += nectarGiven;
        hiveLookup[hive] = hiveData;

        var beeIsDepleted = bee.nectarCarried <= 0.01;
        if (!beeIsDepleted) return;
        
        ecb.SetComponentEnabled<AtHive>(chunkKey, entity, false);

        var rng = BeeData.GetRng(time, entity);
        var (flowerEntity, flowerData) = flowerManager.GetRandomFlower(ref rng);
        bee.targetFlower = flowerEntity;
        ecb.SetComponentEnabled<TravellingToFlower>(chunkKey, entity, true);
        ecb.AddComponent(chunkKey, entity, new FlightPath()
        {
            time = 0,
            from = trans.Position,
            to = flowerData.position,
        });
    }
}