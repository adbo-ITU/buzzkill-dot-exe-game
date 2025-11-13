using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[UpdateAfter(typeof(BeeFlyingSystem))] 
partial struct FlowerNectarSystem : ISystem
{
    private ComponentLookup<FlowerData> _flowerLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _flowerLookup = state.GetComponentLookup<FlowerData>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

    
        _flowerLookup.Update(ref state);
        
        var flowerNectarJob = new FlowerNectarJob
        {
            ecb = ecb,
            deltaTime = SystemAPI.Time.DeltaTime,
            nectarRegenRate = 5f,
        }.Schedule(state.Dependency);
        
        flowerNectarJob.Complete();
    }
    
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct FlowerNectarJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;
    public float nectarRegenRate;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity,
        ref FlowerData flower)
    {
        flower.nectarAmount += nectarRegenRate * deltaTime;
        if (flower.nectarAmount >= flower.nectarCapacity)
        {
            flower.nectarAmount = flower.nectarCapacity;
        }
    }
}