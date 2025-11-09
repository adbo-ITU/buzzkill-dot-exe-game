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
        state.RequireForUpdate<SimulationConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var em = state.EntityManager;
        var spawner = SystemAPI.GetSingleton<FlowerSpawner>();
        var config = SystemAPI.GetSingleton<SimulationConfig>().Config;
        var rnd = new Random(42);  //TODO: should seed always be 42??
        
        var flowerManager = new FlowerManager
        {
            flowerEntities = new NativeArray<Entity>(config.numFlowers, Allocator.Persistent),
            flowerData = new NativeArray<FlowerData>(config.numFlowers, Allocator.Persistent),
        };
        
        var flowerManagerEntity = em.CreateEntity();
        em.AddComponentData(flowerManagerEntity, flowerManager);
        
        var prefabs = new NativeArray<Entity>(5, Allocator.Temp)
        {
            [0] = spawner.flowerPrefabA,
            [1] = spawner.flowerPrefabB,
            [2] = spawner.flowerPrefabC,
            [3] = spawner.flowerPrefabD,
            [4] = spawner.flowerPrefabE,
        };

        for (int i = 0; i < config.numFlowers; i++)
        {
            const float flowerHeight = 5f;
            
            float x = rnd.NextFloat(0f, 50f);
            float z = rnd.NextFloat(0f, 50f);
            float3 pos = new float3(x, 0, z); 

            var prefab = prefabs[rnd.NextInt(prefabs.Length)];
            var e = em.Instantiate(prefab);

            var capacity = rnd.NextFloat(5f, 20f);
            var flowerData = new FlowerData
            {
                nectarCapacity = capacity,
                nectarAmount = capacity,
                position = pos + new float3(0, flowerHeight, 0)
            };

            flowerManager.flowerEntities[i] = e;
            flowerManager.flowerData[i] = flowerData;
                
            em.AddComponentData(e, flowerData);
            var transform = LocalTransform.FromPosition(pos).WithScale(flowerHeight);
            em.SetComponentData(e, transform);
        }

        prefabs.Dispose();
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<FlowerManager>(out var flowerManager))
        {
            if (flowerManager.flowerEntities.IsCreated)
                flowerManager.flowerEntities.Dispose();
            if (flowerManager.flowerData.IsCreated)
                flowerManager.flowerData.Dispose();
            
        }
    }

}