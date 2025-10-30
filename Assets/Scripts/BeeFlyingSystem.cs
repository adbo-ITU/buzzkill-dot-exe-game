using System;
using System.Numerics;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
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
    public static bool TravelBee(ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, float deltaTime)
    {
        var between = bee.destination - flightPath.position;
        var distance = math.length(between);

        if (distance <= 1)
        {
            return false;
        }
        
        flightPath.time += deltaTime;

        float3 from = flightPath.from;
        float3 to = flightPath.to;
        var direction = math.normalize(to - from);

        flightPath.position += direction * bee.speed * deltaTime;
        
        var totalDist = math.length(flightPath.to - flightPath.from);
        var travelled = math.length(flightPath.position - from);
        var progress = travelled / totalDist;

        var height = math.sin(progress * math.PI) * totalDist / 10f;
        var arc = math.up() * height;

        var orthogonal = math.normalize(math.cross(direction, math.up()));
        var verticalWiggle = math.sin(flightPath.time * 10f) * math.up() / 3f;
        var horizontalWiggle = orthogonal * math.cos(flightPath.time * 20f) / 2f;
        var wiggle = verticalWiggle + horizontalWiggle;
        
        trans.Position = flightPath.position + arc + wiggle;

        return true;
    }
}

[BurstCompile]
public partial struct BeeToFlowerJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public float deltaTime;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, ref BeeData bee, ref FlightPath flightPath, in TravellingToFlower travellingToFlower)
    {
        var moved = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime);

        if (!moved)
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
        var moved = BeeFlyingSystem.TravelBee(ref trans, ref bee, ref flightPath, deltaTime);

        if (!moved)
        {
            ecb.RemoveComponent<TravellingToHome>(chunkKey, entity);
            ecb.AddComponent(chunkKey, entity, new AtHive());
        }
    }
}