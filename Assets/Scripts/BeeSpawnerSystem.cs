using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(FlowerSpawnerSystem))] 
[UpdateInGroup(typeof(InitializationSystemGroup))] 
public partial struct BeeSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeeSpawner>();
        state.RequireForUpdate<FlowerData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var i = 0;
        var flowers = new NativeArray<(FlowerData, Entity)>(1000, Allocator.TempJob);
        foreach ((RefRO<FlowerData> flower, Entity entity) in SystemAPI.Query<RefRO<FlowerData>>().WithEntityAccess()) 
        {
            flowers[i++] = (flower.ValueRO, entity);
        }
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var handle = new BeeSpawnJob
        {
            ecb = ecb,
            flowers = flowers,
            numFlowers = i
        }.Schedule(state.Dependency);

        handle.Complete();

        flowers.Dispose();
    }
}

[BurstCompile]
public partial struct BeeSpawnJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public NativeArray<(FlowerData, Entity)> flowers;
    public int numFlowers;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref BeeSpawner spawner, Entity entity)
    {
        var rnd = new Random(42);

        for (float i = 0; i < spawner.numBees; i++)
        {
            float x = rnd.NextFloat(0f, 50f);
            float y = rnd.NextFloat(10f, 30f);
            float z = rnd.NextFloat(0f, 50f);
            float3 pos = new float3(x, y,z);

            var flowerIndex = rnd.NextInt(numFlowers);
            var (flower, flowerEntity) = flowers[flowerIndex];

            var e = ecb.Instantiate(chunkKey, spawner.beePrefab);
            ecb.AddComponent(chunkKey, e, new BeeData
            {
                velocity = math.float3(10, 0, 0),
                destination = flower.position,
                nectarCapacity = 10,
                nectarCarried = 0,
                homeHive = 0,
                targetFlower = flowerEntity,
            });
            ecb.SetComponent(chunkKey, e, new TravellingToFlower());
            ecb.SetComponent(chunkKey, e, LocalTransform.FromPosition(pos));
        }
    }
}
