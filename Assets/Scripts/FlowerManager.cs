using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;


public struct FlowerManager : IComponentData
{
    public NativeArray<Entity> flowerEntities;
    public NativeArray<FlowerData> flowerData;

    public (Entity, FlowerData) GetRandomFlower(Random rng)
    {
        var index = rng.NextInt(0, flowerEntities.Length);
        return (flowerEntities[index], flowerData[index]);
    }
}