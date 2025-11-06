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
    public (Entity, HiveData) GetHive(int hiveIndex)
    {
        return (hiveEntities[hiveIndex], hiveData[hiveIndex]);
    }
}