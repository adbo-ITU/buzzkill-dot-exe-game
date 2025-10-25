using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(BeeFlyingSystem))] 
partial struct BeeAtFlowerSystem : ISystem
{
    private ComponentLookup<FlowerData> _flowerLookup;
    private ComponentLookup<HiveData> _hiveLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _flowerLookup = state.GetComponentLookup<FlowerData>(isReadOnly: false);
        _hiveLookup = state.GetComponentLookup<HiveData>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        _flowerLookup.Update(ref state);
        _hiveLookup.Update(ref state);
        
        var atFlowerJob = new BeeAtFlowerJob
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            flowerLookup = _flowerLookup,
            hiveLookup = _hiveLookup,
            ecb = ecb
        }.Schedule(state.Dependency);

        atFlowerJob.Complete();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct BeeAtFlowerJob : IJobEntity
{
    public float deltaTime;
    public ComponentLookup<FlowerData> flowerLookup;
    // marked as read-only hive lookup 
    public ComponentLookup<HiveData> hiveLookup;
    
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, in AtFlower atFlower, ref BeeData bee)
    {
        var rot = math.mul(quaternion.RotateZ(2f * deltaTime), quaternion.RotateY(2f * deltaTime));
        trans.Rotation = math.mul(trans.Rotation, rot);

        if (bee.targetFlower == null) return;
        var targetFlower = (Entity) bee.targetFlower;

        var flower = flowerLookup.GetRefRW(targetFlower);

        var maxNectarToTake = 2f * deltaTime;
        var nectarBeeCanTake = math.min(maxNectarToTake, bee.nectarCapacity - bee.nectarCarried);

        if (flower.ValueRW.nectarAmount > 0)
        {
            var nectarTaken = math.min(flower.ValueRW.nectarAmount, nectarBeeCanTake);
            flower.ValueRW.nectarAmount -= nectarTaken;
            bee.nectarCarried += nectarTaken;
        }

        var flowerIsEmpty = flower.ValueRW.nectarAmount <= 0.01;
        var beeIsSaturated = bee.nectarCapacity - bee.nectarCarried <= 0.01;

        if (flowerIsEmpty || beeIsSaturated)
        {
            ecb.RemoveComponent<AtFlower>(chunkKey, entity);
            
            bee.targetFlower = null;
            
            if (bee.homeHive == null) return; // TODO: handle no hive case
            var hive =  (Entity) bee.homeHive;
            bee.destination = hiveLookup.GetRefRO(hive).ValueRO.position;

            // TODO: find a new flower to visit before going home in case the bee is not saturated
            ecb.AddComponent<TravellingToHome>(chunkKey, entity);
        }
    }
}