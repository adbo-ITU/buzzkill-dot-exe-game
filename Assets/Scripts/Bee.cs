using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

class Bee : MonoBehaviour
{
    public float3 position;
    public float speed;
    public float nectarCapacity;
    public float nectarCarried;
}

class BeeBaker : Baker<Bee>
{
    public override void Bake(Bee authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new BeeData
        {
            nectarCapacity = authoring.nectarCapacity,
            nectarCarried = authoring.nectarCarried,
            targetFlower = Entity.Null,
            homeHive = Entity.Null
        });
        AddComponent(entity, new TravellingToFlower());
    }
}

public struct BeeData : IComponentData
{
    public Entity targetFlower;
    public float nectarCapacity;
    public float nectarCarried;
    public Entity homeHive;

    public static Random GetRng(double time, Entity entity)
    {
        return new Random((uint)((long)((entity.Index * time) * 275869127) ^ 6953540479));
    }
}

public struct TravellingToFlower : IComponentData, IEnableableComponent {}
public struct TravellingToHome : IComponentData, IEnableableComponent {}
public struct AtFlower : IComponentData, IEnableableComponent {}
public struct AtHive : IComponentData, IEnableableComponent {}

public struct FlightPath : IComponentData
{
    public float time;
    public float speed;
    public float3 from;
    public float3 to;
}