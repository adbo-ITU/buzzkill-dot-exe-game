using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class Bee : MonoBehaviour
{
    public float3 position;
    public float3 velocity;
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
            velocity = authoring.velocity,
            destination = authoring.destination,
            nectarCapacity = authoring.nectarCapacity,
            nectarCarried = authoring.nectarCarried,
            homeHive = authoring.homeHive,
            state = BeeState.TravellingToFlower,
            targetFlower = null
        });
    }
}

public struct BeeData : IComponentData
{
    public float3 velocity;
    public float3 destination;
    public Nullable<Entity> targetFlower;
    public float nectarCapacity;
    public float nectarCarried;
    public int homeHive;
    public BeeState state;
}

public enum BeeState : byte
{
    TravellingToFlower,
    TravellingHome,
    AtFlower,
    AtHive,
}