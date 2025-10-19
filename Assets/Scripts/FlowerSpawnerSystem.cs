using Unity.Burst;
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
            ecb = ecb
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
        for (float i = 0; i < spawner.numFlower; i++)
        {
                float x = rnd.NextFloat(0f, 50f);
                float z = rnd.NextFloat(0f, 50f);
                var e = ecb.Instantiate(chunkKey, spawner.flowerPrefab);
                float3 pos = new float3(x, 0,z); 
                
                var capacity = rnd.NextFloat(5f, 20f);

                
                ecb.AddComponent(chunkKey, e, new FlowerData
                {
                    nectarCapacity = capacity,
                    nectarAmount = capacity,
                    position = pos,
                });
                ecb.SetComponent(chunkKey, e, LocalTransform.FromPosition(pos));
        }
    }
}