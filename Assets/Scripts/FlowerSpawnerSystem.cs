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
        state.RequireForUpdate<FlowerPrefabs>();
        state.RequireForUpdate<SimulationConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var em = state.EntityManager;
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        var flowerPrefabs = SystemAPI.GetSingleton<FlowerPrefabs>();
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
            [0] = flowerPrefabs.flowerPrefabA,
            [1] = flowerPrefabs.flowerPrefabB,
            [2] = flowerPrefabs.flowerPrefabC,
            [3] = flowerPrefabs.flowerPrefabD,
            [4] = flowerPrefabs.flowerPrefabE,
        };

        for (int i = 0; i < config.numFlowers; i++)
        {
            const float flowerHeight = 5f;

            var xz = FlowerManager.GetRandomPointInCircle(ref rnd, config.worldSize/2f) + config.worldSize/2.5f * math.float2(1, 1);
            float3 pos = new float3(xz.x, 0, xz.y); 

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