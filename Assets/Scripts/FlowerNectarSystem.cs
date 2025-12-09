using System;
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
        state.RequireForUpdate<SimulationConfig>();
        _flowerLookup = state.GetComponentLookup<FlowerData>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

    
        _flowerLookup.Update(ref state);
        
        switch (config.executionMode)
        {
            case ExecutionMode.Scheduled:
            {
                var flowerNectarJob = new FlowerNectarJob
                {
                    ecb = ecb,
                    deltaTime = SystemAPI.Time.DeltaTime,
                    nectarRegenRate = 1.5f,
                }.Schedule(state.Dependency);
        
                flowerNectarJob.Complete();
            } break;

            case ExecutionMode.ScheduledParallel:
            {
                var flowerNectarJob = new FlowerNectarJob
                {
                    ecb = ecb,
                    deltaTime = SystemAPI.Time.DeltaTime,
                    nectarRegenRate = 1.5f,
                }.ScheduleParallel(state.Dependency);
        
                flowerNectarJob.Complete();
            } break;
            
            case ExecutionMode.MainThread:
            {
                var deltaTime = SystemAPI.Time.DeltaTime;
                var nectarRegenRate = 1.5f;
                foreach (var flower in SystemAPI.Query<RefRW<FlowerData>>())
                {
                    flower.ValueRW.nectarAmount += nectarRegenRate * deltaTime;
                    if (flower.ValueRO.nectarAmount >= flower.ValueRO.nectarCapacity)
                    {
                        flower.ValueRW.nectarAmount = flower.ValueRO.nectarCapacity;
                    }
                }
            } break;
        }
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