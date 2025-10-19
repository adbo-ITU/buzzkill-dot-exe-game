using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class Bee : MonoBehaviour
{
    public float3 position;
    public float speed;
    public float3 destination;
    public float nectarCapacity;
    public float nectarCarried;
    public int homeHive;
}

class BeeBaker : Baker<Bee>
{
    public override void Bake(Bee authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new BeeData
        {
            speed = authoring.speed,
            destination = authoring.destination,
            nectarCapacity = authoring.nectarCapacity,
            nectarCarried = authoring.nectarCarried,
            homeHive = authoring.homeHive,
            targetFlower = null
        });
        AddComponent(entity, new TravellingToFlower());
    }
}

public struct BeeData : IComponentData
{
    public float speed;
    public float3 destination;
    public Nullable<Entity> targetFlower;
    public float nectarCapacity;
    public float nectarCarried;
    public int homeHive;
}

public struct TravellingToFlower : IComponentData {}
public struct TravellingToHome : IComponentData {}
public struct AtFlower : IComponentData {}
public struct AtHive : IComponentData {}
