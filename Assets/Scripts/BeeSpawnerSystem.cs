using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(FlowerSpawnerSystem))] 
[UpdateAfter(typeof(HiveSpawnerSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))] 
public partial struct BeeSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeeSpawner>();
        state.RequireForUpdate<FlowerData>();
        state.RequireForUpdate<HiveData>();
        state.RequireForUpdate<FlowerManager>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;

        var j = 0;
        var hives = new NativeArray<(HiveData, Entity)>(3, Allocator.TempJob); // todo: get correct number of hives from where?
        foreach ((RefRO<HiveData> hive, Entity entity) in SystemAPI.Query<RefRO<HiveData>>().WithEntityAccess()) 
        {
            hives[j++] = (hive.ValueRO, entity);
        }
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var handle = new BeeSpawnJob
        {
            ecb = ecb,
            hives = hives,
            numHives = hives.Length,
        }.Schedule(state.Dependency);

        handle.Complete();

        hives.Dispose();
    }
}

[BurstCompile]
public partial struct BeeSpawnJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public NativeArray<(HiveData, Entity)> hives;
    public int numHives;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref BeeSpawner spawner, Entity entity)
    {
        for (int i = 0; i < spawner.numBees; i++)
        {
            // modulus to find hive to be home
            var hiveIndex = i % numHives; 
            var (hive, hiveEntity) = hives[hiveIndex];
            
            var e = ecb.Instantiate(chunkKey, spawner.beePrefab);
            ecb.AddComponent(chunkKey, e, new BeeData
            {
                speed = 20,
                destination = float3.zero,
                nectarCapacity = 40,
                nectarCarried = 0,
                homeHive = hiveEntity,
                targetFlower = Entity.Null,
            });
            ecb.AddComponent(chunkKey, e, new AtHive());
            ecb.SetComponent(chunkKey, e, LocalTransform.FromPosition(hive.position));
        }
    }
}
