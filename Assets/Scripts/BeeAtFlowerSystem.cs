using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(BeeFlyingSystem))] 
[UpdateAfter(typeof(BeeAtHiveSystem))]
partial struct BeeAtFlowerSystem : ISystem
{
    private ComponentLookup<FlowerData> _flowerLookup;
    private ComponentLookup<HiveData> _hiveLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowerManager>();
        _flowerLookup = state.GetComponentLookup<FlowerData>(isReadOnly: false);
        _hiveLookup = state.GetComponentLookup<HiveData>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        _flowerLookup.Update(ref state);
        _hiveLookup.Update(ref state);
        
        var flowerManager = SystemAPI.GetSingleton<FlowerManager>();
        
        var atFlowerJob = new BeeAtFlowerJob
        {
            time = SystemAPI.Time.ElapsedTime,
            deltaTime = SystemAPI.Time.DeltaTime,
            flowerLookup = _flowerLookup,
            hiveLookup = _hiveLookup,
            ecb = ecb,
            flowerManager = flowerManager
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
    public double time;
    public float deltaTime;
    public ComponentLookup<FlowerData> flowerLookup;
    [ReadOnly] public ComponentLookup<HiveData> hiveLookup;
    [ReadOnly] public FlowerManager flowerManager;
    
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, in AtFlower atFlower, ref BeeData bee)
    {
        var rot = math.mul(quaternion.RotateZ(2f * deltaTime), quaternion.RotateY(2f * deltaTime));
        trans.Rotation = math.mul(trans.Rotation, rot);
        
        var targetFlower = bee.targetFlower;
        if (targetFlower == Entity.Null) return;

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

        if (!flowerIsEmpty && !beeIsSaturated) return;

        ecb.RemoveComponent<AtFlower>(chunkKey, entity);

        float3 to;
        if (beeIsSaturated)
        {
            var hive = hiveLookup.GetRefRO(bee.homeHive);
            bee.targetFlower = Entity.Null;
            to = hive.ValueRO.position;
            ecb.AddComponent<TravellingToHome>(chunkKey, entity);
        }
        else
        {
            var rng = BeeData.GetRng(time, entity);
            var (flowerEntity, flowerData) = flowerManager.GetRandomFlower(ref rng);
            bee.targetFlower = flowerEntity;
            to = flowerData.position;
            ecb.AddComponent<TravellingToFlower>(chunkKey, entity);
        }

        ecb.AddComponent(chunkKey, entity, new FlightPath()
        {
            time = 0,
            from = trans.Position,
            to = to,
            position = trans.Position
        });
    }
}