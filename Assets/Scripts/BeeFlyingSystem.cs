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
}

[BurstCompile]
public partial struct BeeMoveJob : IJobEntity
{
    public float deltaTime;

    public void Execute(Entity entity, ref LocalTransform trans, in BeeData bee)
    {
        var between = bee.destination - trans.Position;
        var direction = math.normalize(between);
        var speed = math.length(bee.velocity);

        trans.Position += direction * speed * deltaTime;
    }
}