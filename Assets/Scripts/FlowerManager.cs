using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public struct FlowerManager : IComponentData
{
    public NativeArray<Entity> flowerEntities;
    public NativeArray<FlowerData> flowerData;

    [BurstCompile]
    public (Entity, FlowerData) GetRandomFlower(ref Random rng)
    {
        var index = rng.NextInt(0, flowerEntities.Length);
        return (flowerEntities[index], flowerData[index]);
    }
    
    [BurstCompile]
    public static float2 GetRandomPointInCircle(ref Random rng, float radius)
    {
        float2 point;
        do
        {
            point = new float2(
                rng.NextFloat(-radius, radius),
                rng.NextFloat(-radius, radius)
            );
        } while (math.lengthsq(point) > radius * radius);
    
        return point;
    }
}