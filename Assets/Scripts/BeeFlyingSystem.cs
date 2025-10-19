using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct BeeFlyingSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var deltaTime = (float)SystemAPI.Time.DeltaTime;

        var flyToFlowerJob = new BeeToFlowerJob
        {
            ecb = ecb,
            deltaTime = deltaTime,
        }.Schedule(state.Dependency);

        var atFlowerJob = new BeeAtFlowerJob
        {
            deltaTime = deltaTime,
        }.Schedule(flyToFlowerJob);

        atFlowerJob.Complete();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    
    [BurstCompile]
    public static bool TravelBee(ref LocalTransform trans, ref BeeData bee, float deltaTime)
    {
        var between = bee.destination - trans.Position;
        var distance = math.length(between);

        if (distance <= 1)
        {
            return false;
        }

        var direction = math.normalize(between);

        trans.Position += direction * bee.speed * deltaTime;

        return true;
    }
}

[BurstCompile]
public partial struct BeeToFlowerJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, ref BeeData bee, in TravellingToFlower travellingToFlower)
    {
        var moved = BeeFlyingSystem.TravelBee(ref trans, ref bee, deltaTime);

        if (!moved)
        {
            ecb.RemoveComponent<TravellingToFlower>(chunkKey, entity);
            ecb.AddComponent(chunkKey, entity, new AtFlower());
        }
    }
}

[BurstCompile]
public partial struct BeeAtFlowerJob : IJobEntity
{
    public float deltaTime;

    void Execute(Entity entity, ref LocalTransform trans, in AtFlower atFlower)
    {
        var rot = math.mul(quaternion.RotateZ(2f * deltaTime), quaternion.RotateY(2f * deltaTime));
        trans.Rotation = math.mul(trans.Rotation, rot);
    }
}