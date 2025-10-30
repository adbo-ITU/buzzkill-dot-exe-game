using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public struct HiveManager : IComponentData
{
    public NativeArray<Entity> hiveEntities;
    public NativeArray<HiveData> hiveData;
    
    [BurstCompile]
    public (Entity, HiveData) GetRandomHive(Random rng)
    {
        var index = rng.NextInt(0, hiveEntities.Length);
        // modules wihh hive entity length
        Debug.Log($"Hive index: {index}");
        return (hiveEntities[index], hiveData[index]);
    }
}