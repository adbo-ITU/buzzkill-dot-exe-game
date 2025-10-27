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
}