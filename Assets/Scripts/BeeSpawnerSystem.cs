using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
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
        
        var handle = new BeeSpawnJob
        {
            ecb = ecb
        }.Schedule(state.Dependency);
        
        handle.Complete();
    }
}

[BurstCompile]
public partial struct BeeSpawnJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref BeeSpawner spawner, Entity entity)
    {
        var rnd = new Random(42);

        for (float i = 0; i < spawner.numBees; i++)
        {
            var e = ecb.Instantiate(chunkKey, spawner.beePrefab);
            ecb.AddComponent(chunkKey, e, new BeeData
            {
                velocity = math.float3(10, 0, 0),
                destination = rnd.NextFloat3(25),
                nectarCapacity = 10,
                nectarCarried = 0,
                homeHive = 0,
                state = BeeState.TravellingToFlower,
            });
        }
    }
}
