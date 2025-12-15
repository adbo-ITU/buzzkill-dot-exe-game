using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(InitializationSystemGroup))] 
public partial struct HiveSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HiveSpawner>();
        state.RequireForUpdate<SimulationConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var em = state.EntityManager;
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        var spawner = SystemAPI.GetSingleton<HiveSpawner>();
        var rnd = new Random(42); //TODO: should seed always be 42??
        
        var hiveManager = new HiveManager
        {
            hiveEntities = new NativeArray<Entity>(config.numHives, Allocator.Persistent),
            hiveData = new NativeArray<HiveData>(config.numHives, Allocator.Persistent),
        };
        
        var hiveManagerEntity = em.CreateEntity();
        em.AddComponentData(hiveManagerEntity, hiveManager);
        
        var prefab = spawner.hivePrefab;
        
        for (int i = 0; i < config.numHives; i++)
        {
            const float hiveHeight = 5f;

            FlowerManager.GetRandomPointInCircle(ref rnd, config.worldSize/2f, out var xz);
            xz += config.worldSize/2.5f * math.float2(1, 1);
            float3 pos = new float3(xz.x, 0, xz.y); 

            var e =  em.Instantiate(prefab);

            var capacity = rnd.NextFloat(5f, 20f);
                
           var hiveData = new HiveData
            {
                nectarAmount = capacity,
                position = pos + new float3(0, hiveHeight, 0)
            };
            hiveManager.hiveEntities[i] = e;
            hiveManager.hiveData[i] = hiveData;
            
            em.AddComponentData(e, hiveData);
            var transform = LocalTransform.FromPosition(pos).WithScale(hiveHeight);
            em.SetComponentData(e, transform);
        }
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<HiveManager>(out var hiveManager))
        {
            if (hiveManager.hiveEntities.IsCreated)
                hiveManager.hiveEntities.Dispose();
            if (hiveManager.hiveData.IsCreated)
                hiveManager.hiveData.Dispose();
        }
    }

}