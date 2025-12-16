using System;
using Unity.Entities;
using UnityEngine;

public class BeeSpawnerAuthoring : MonoBehaviour
{
    public GameObject beePrefab;
    public GameObject cubePrefab;

    class Baker : Baker<BeeSpawnerAuthoring>
    {
        public override void Bake(BeeSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var spawner = new BeeSpawner
            {
                beePrefab = GetEntity(authoring.beePrefab, TransformUsageFlags.Dynamic),
                cubePrefab = GetEntity(authoring.cubePrefab, TransformUsageFlags.Dynamic),
            };
            AddComponent(entity, spawner);
        }
    }
}

public struct BeeSpawner : IComponentData
{
    public Entity beePrefab;
    public Entity cubePrefab;
}