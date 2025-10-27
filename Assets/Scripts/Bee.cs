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
            targetFlower = Entity.Null,
            homeHive = null
        });
        AddComponent(entity, new TravellingToFlower()); //  TODO change this to at hive
    }
}

public struct BeeData : IComponentData
{
    public float speed;
    public float3 destination;
    public Entity targetFlower;
    public float nectarCapacity;
    public float nectarCarried;
    public Nullable<Entity> homeHive;
}

public struct TravellingToFlower : IComponentData {}
public struct TravellingToHome : IComponentData {}
public struct AtFlower : IComponentData {}
public struct AtHive : IComponentData {}
