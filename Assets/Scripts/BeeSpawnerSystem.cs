using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(FlowerSpawnerSystem))] 
[UpdateInGroup(typeof(InitializationSystemGroup))] 
public partial struct BeeSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeeSpawner>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem .Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged ).AsParallelWriter();

        var i = 0;
        var flowers = new NativeArray<(LocalTransform, Entity)>(1000, Allocator.TempJob);
        foreach ((RefRO<FlowerData> flower, RefRO<LocalTransform> trans, Entity entity) in SystemAPI.Query<RefRO<FlowerData>, RefRO<LocalTransform>>().WithEntityAccess()) 
        {
            flowers[i++] = (trans.ValueRO, entity);
        }

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
    public NativeArray<(LocalTransform, Entity)> flowers;
    public int numFlowers;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref BeeSpawner spawner, Entity entity)
    {
        var rnd = new Random(42);

        for (float i = 0; i < spawner.numBees; i++)
        {
            var flowerIndex = rnd.NextInt(numFlowers);
            var (flowerTrans, flowerEntity) =  flowers[flowerIndex];

            var e = ecb.Instantiate(chunkKey, spawner.beePrefab);
            ecb.AddComponent(chunkKey, e, new BeeData
            {
                velocity = math.float3(10, 0, 0),
                destination = flowerTrans.Position,
                nectarCapacity = 10,
                nectarCarried = 0,
                homeHive = 0,
                state = BeeState.TravellingToFlower,
                targetFlower = flowerEntity,
            });
        }
    }
}
