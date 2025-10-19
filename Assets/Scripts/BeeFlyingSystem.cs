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
        var move = new BeeMoveJob
        {
            deltaTime = (float)SystemAPI.Time.DeltaTime,
        }.Schedule(state.Dependency);

        move.Complete();
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
        var speed = math.length(bee.velocity);

        trans.Position += direction * speed * deltaTime;

        return true;
    }
}

[BurstCompile]
public partial struct BeeMoveJob : IJobEntity
{
    public float deltaTime;

    void Execute(Entity entity, ref LocalTransform trans, ref BeeData bee, in TravellingToFlower travellingToFlower)
    {
        var moved = BeeFlyingSystem.TravelBee(ref trans, ref bee, deltaTime);
        
        // if (!moved)
    }
}