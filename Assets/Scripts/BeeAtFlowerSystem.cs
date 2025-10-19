using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct BeeAtFlowerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = (float)SystemAPI.Time.DeltaTime;
        
        var atFlowerJob = new BeeAtFlowerJob
        {
            deltaTime = deltaTime,
        }.Schedule(state.Dependency);

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
public partial struct BeeAtFlowerJob : IJobEntity
{
    public float deltaTime;

    void Execute(Entity entity, ref LocalTransform trans, in AtFlower atFlower)
    {
        var rot = math.mul(quaternion.RotateZ(2f * deltaTime), quaternion.RotateY(2f * deltaTime));
        trans.Rotation = math.mul(trans.Rotation, rot);
    }
}