using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct FlowerSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowerSpawner>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem .Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged ).AsParallelWriter();
        
        var handle = new FlowerSpawnJob
        {
            ecb = ecb,
        }.Schedule(state.Dependency);
        
        handle.Complete();
    }
}

[BurstCompile]
public partial struct FlowerSpawnJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref FlowerSpawner spawner, Entity entity)
    {
        var rnd = new Random(42);
        
        var prefabs = new NativeArray<Entity>(5, Allocator.Temp)
        {
            [0] = spawner.flowerPrefabA,
            [1] = spawner.flowerPrefabB,
            [2] = spawner.flowerPrefabC,
            [3] = spawner.flowerPrefabD,
            [4] = spawner.flowerPrefabE,
        };

        var flowerManager = new FlowerManager
        {
            flowerMap = new NativeHashMap<Entity, FlowerData>(spawner.numFlower, Allocator.Persistent),
            random = rnd,
        };

        for (float i = 0; i < spawner.numFlower; i++)
        {
                const float flowerHeight = 5f;
            
                float x = rnd.NextFloat(0f, 50f);
                float z = rnd.NextFloat(0f, 50f);
                float3 pos = new float3(x, 0, z); 

                var prefab = prefabs[rnd.NextInt(prefabs.Length)];
                var e = ecb.Instantiate(chunkKey, prefab);

                var capacity = rnd.NextFloat(5f, 20f);
                var flowerData = new FlowerData
                {
                    nectarCapacity = capacity,
                    nectarAmount = capacity,
                    position = pos + new float3(0, flowerHeight, 0)
                };
                flowerManager.flowerMap[e] = flowerData;
                
                ecb.AddComponent(chunkKey, e, flowerData);
                var transform = LocalTransform.FromPosition(pos).WithScale(flowerHeight);
                ecb.SetComponent(chunkKey, e, transform);
        }

        var flowerManagerEntity = ecb.CreateEntity(chunkKey);
        ecb.AddComponent(chunkKey, flowerManagerEntity, flowerManager);
        
        prefabs.Dispose();
    }
}