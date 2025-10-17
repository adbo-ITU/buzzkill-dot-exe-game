using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

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
        var n = spawner.numFlower;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var e = ecb.Instantiate(chunkKey, spawner.flowerPrefab);
                float3 pos = new float3(x * 2f, 0f, y * 2f); // TODO change to pretty
                
                ecb.AddComponent(chunkKey, e, new FlowerData
                {
                    nectarCapacity = 10,
                    nectarAmount = 10,

                });
                ecb.SetComponent(chunkKey, e, LocalTransform.FromPosition(pos));
            }
        }
    }
}