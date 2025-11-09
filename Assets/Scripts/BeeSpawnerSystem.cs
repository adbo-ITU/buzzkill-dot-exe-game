using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(FlowerSpawnerSystem))] 
[UpdateAfter(typeof(HiveSpawnerSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))] 
public partial struct BeeSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeeSpawner>();
        state.RequireForUpdate<HiveData>();
        state.RequireForUpdate<HiveManager>();
        state.RequireForUpdate<SimulationConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var hiveManager = SystemAPI.GetSingleton<HiveManager>();
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var handle = new BeeSpawnJob
        {
            time = SystemAPI.Time.ElapsedTime,
            ecb = ecb,
            hiveManager = hiveManager,
            config = config,
        }.Schedule(state.Dependency);

        handle.Complete();
    }
}

[BurstCompile]
public partial struct BeeSpawnJob : IJobEntity
{
    public double time;
    public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public HiveManager  hiveManager;
    [ReadOnly] public SimulationConfigValues config;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref BeeSpawner spawner, Entity entity)
    {
        var rng = BeeData.GetRng(time, entity);
        for (int i = 0; i < config.numBees; i++)
        {
            var (hiveEntity, hiveData) = hiveManager.GetRandomHive(ref rng);
            
            var e = ecb.Instantiate(chunkKey, spawner.beePrefab);
            ecb.AddComponent(chunkKey, e, new BeeData
            {
                speed = 20,
                nectarCapacity = 40,
                nectarCarried = 0,
                homeHive = hiveEntity,
                targetFlower = Entity.Null,
            });
            ecb.AddComponent(chunkKey, e, new AtHive());
            ecb.SetComponent(chunkKey, e, LocalTransform.FromPosition(hiveData.position));
        }
    }
}
