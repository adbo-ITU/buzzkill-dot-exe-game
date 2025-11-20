using System;
using System.Numerics;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
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
    public static float3 TravelBee(ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, float deltaTime)
    {
        var between = flightPath.to - trans.Position;
        var distance = math.length(between);

        if (distance <= 1)
        {
            return float3.zero;
        }
        
        flightPath.time += deltaTime;
        
        var direction = math.normalize(between);

        return direction * bee.speed;
        
        // flightPath.position += direction * bee.speed * deltaTime;
        //
        // var totalDist = math.length(flightPath.to - flightPath.from);
        // var travelled = math.length(flightPath.position - flightPath.from);
        // var progress = travelled / totalDist;
        //
        // var height = math.sin(progress * math.PI) * totalDist / 10f;
        // var arc = math.up() * height;
        //
        // var orthogonal = math.normalize(math.cross(direction, math.up()));
        // var verticalWiggle = math.sin(flightPath.time * 10f) * math.up() / 3f;
        // var horizontalWiggle = orthogonal * math.cos(flightPath.time * 20f) / 2f;
        // var wiggle = verticalWiggle + horizontalWiggle;
        //
        // trans.Position = flightPath.position + arc + wiggle;
    }
}

[BurstCompile]
public partial struct BeeToFlowerJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, in TravellingToFlower travellingToFlower)
    {
        var newVelocity = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime);
        
        ecb.SetComponent(chunkKey, entity, new PhysicsVelocity
        {
            Linear  = newVelocity,
            Angular = float3.zero
        });
        
        if (newVelocity is { x: 0, y: 0, z: 0 })
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

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, in TravellingToHome travellingToHome)
    {
        var newVelocity = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime);
        
        ecb.SetComponent(chunkKey, entity, new PhysicsVelocity
        {
            Linear  = newVelocity,
            Angular = float3.zero
        });
        
        if (newVelocity is { x: 0, y: 0, z: 0 })
        {
            ecb.RemoveComponent<TravellingToHome>(chunkKey, entity);
            ecb.AddComponent(chunkKey, entity, new AtHive());
        }
    }
}