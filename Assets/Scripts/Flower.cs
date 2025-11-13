using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class Flower : MonoBehaviour
{
    public float3 position;
    public float nectarCapacity;
    public float nectarAmount;
}

class FlowerBaker : Baker<Flower>
{
    public override void Bake(Flower authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new FlowerData
        {
            nectarCapacity = authoring.nectarCapacity,
            nectarAmount = authoring.nectarAmount,
            position = authoring.position,
        });
    }
}

public struct FlowerData : IComponentData
{
    public float nectarCapacity;
    public float nectarAmount;
    public float3 position;
}