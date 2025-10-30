using System;
using Unity.Entities;
using UnityEngine;

public class BeeSpawnerAuthoring : MonoBehaviour
{
    public GameObject beePrefab;
    public int numBees;
    public int numHives;

    class Baker : Baker<BeeSpawnerAuthoring>
    {
        public override void Bake(BeeSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var spawner = new BeeSpawner
            {
                beePrefab = GetEntity(authoring.beePrefab, TransformUsageFlags.Dynamic),
                numBees = authoring.numBees,
                numHives = authoring.numHives,
                hiveEntity = null
            };
            AddComponent(entity, spawner);
        }
    }
}

public struct BeeSpawner : IComponentData
{
    public Entity beePrefab;
    public int numBees;
    public int numHives;
    public Nullable<Entity> hiveEntity;
}