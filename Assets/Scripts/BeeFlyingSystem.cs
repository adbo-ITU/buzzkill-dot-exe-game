using System;
using System.Numerics;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;


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
        
        var flyToHiveJob = new BeeToHiveJob()
        {
            ecb = ecb,
            deltaTime = deltaTime,
        }.Schedule(flyToFlowerJob);

        flyToHiveJob.Complete();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    
    [BurstCompile]
    public static bool TravelBee(ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, float deltaTime, ref PhysicsVelocity velocity, in PhysicsMass mass)
    {
        var between = flightPath.to - trans.Position;
        var distance = math.length(between);

        if (distance <= 1)
        {
            return true;
        }
        
        flightPath.time += deltaTime;

        var direction = math.normalize(between);
        var straightVel = direction * bee.speed * 5f;

        var orthogonal = math.normalize(math.cross(direction, math.up()));
        var verticalWiggle = math.sin(flightPath.time * 7f) * math.up() / 5f;
        var horizontalWiggle = orthogonal * math.cos(flightPath.time * 15f) / 2f;
        var wiggle = (verticalWiggle + horizontalWiggle);

        if (distance >= 2f)
        {
            velocity.ApplyImpulse(
                mass,
                float3.zero,
                quaternion.identity,
                wiggle * deltaTime * 250f,
                trans.Position);
        }
        
        var desiredVel = straightVel;
        var lerpFactor = math.saturate(deltaTime * 1.5f);
        
        if (math.distancesq(flightPath.from, trans.Position) < math.distancesq(flightPath.to, trans.Position))
        {
            desiredVel += math.up() * math.min(100f, 10f / math.distance(flightPath.from, trans.Position));
        }

        velocity.Angular = float3.zero;
        velocity.Linear = math.lerp(velocity.Linear, desiredVel, lerpFactor);
        // Make the bee face its movement direction
        if (math.lengthsq(velocity.Linear) > 0.01f)
        {
            var targetRotation = quaternion.LookRotationSafe(math.normalize(desiredVel), math.up());
            trans.Rotation = math.slerp(trans.Rotation, targetRotation, math.saturate(deltaTime * 10f));
        }

        return false;
    }
}

[BurstCompile]
public partial struct BeeToFlowerJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, in TravellingToFlower travellingToFlower,
        ref PhysicsVelocity velocity, in PhysicsMass mass)
    {
        var reachedDest = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime, ref velocity, in mass);
        
        if (reachedDest)
        {
            ecb.RemoveComponent<TravellingToFlower>(chunkKey, entity);
            ecb.AddComponent(chunkKey, entity, new AtFlower());
        }
    }
}

[BurstCompile]
public partial struct BeeToHiveJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, in TravellingToHome travellingToHome, ref  PhysicsVelocity velocity, in PhysicsMass mass)
    {
        var reachedDest = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime, ref velocity, in mass);
        
        if (reachedDest)
        {
            ecb.RemoveComponent<TravellingToHome>(chunkKey, entity);
            ecb.AddComponent(chunkKey, entity, new AtHive());
        }
    }
}