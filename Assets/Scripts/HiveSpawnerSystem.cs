using Unity.Burst;
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
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem .Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged ).AsParallelWriter();
        
        var handle = new HiveSpawnJob
        {
            ecb = ecb
        }.Schedule(state.Dependency);
        
        handle.Complete();
    }
}

[BurstCompile]
public partial struct HiveSpawnJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref HiveSpawner spawner, Entity entity)
    {
        var rnd = new Random(42);

        var prefabs = new []
        {
            spawner.HivePrefab
        };
        
        for (float i = 0; i < spawner.numHive; i++)
        {
                const float HiveHeight = 5f;
            
                float x = rnd.NextFloat(0f, 50f);
                float z = rnd.NextFloat(0f, 50f);
                float3 pos = new float3(x, 0, z); 

                var prefab = prefabs[rnd.NextInt(prefabs.Length)];
                var e = ecb.Instantiate(chunkKey, prefab);

                var capacity = rnd.NextFloat(5f, 20f);
                
                ecb.AddComponent(chunkKey, e, new HiveData
                {
                    nectarAmount = capacity,
                    position = pos + new float3(0, HiveHeight, 0)
                });

                var transform = LocalTransform.FromPosition(pos).WithScale(HiveHeight);
                ecb.SetComponent(chunkKey, e, transform);
        }
    }
}