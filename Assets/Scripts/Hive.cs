using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class Hive : MonoBehaviour
{
    public float3 position;
    public float nectarAmount;
    public int id;
}

class HiveBaker : Baker<Hive>
{
    public override void Bake(Hive authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new HiveData
        {
            nectarAmount = authoring.nectarAmount,
            position = authoring.position,
            id = authoring.id
        });
    }
}

public struct HiveData : IComponentData
{
    public float nectarAmount;
    public float3 position;
    public int id;
}
