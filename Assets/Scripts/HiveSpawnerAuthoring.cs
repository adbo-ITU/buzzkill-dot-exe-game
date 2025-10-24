using Unity.Entities;
using UnityEngine;

public class HiveSpawnerAuthoring : MonoBehaviour
{
    public GameObject HivePrefab;
    public int numHives;

    class Baker : Baker<HiveSpawnerAuthoring>
    {
        public override void Bake(HiveSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var spawner = new HiveSpawner
            {
                HivePrefab = GetEntity(authoring.HivePrefab, TransformUsageFlags.Dynamic),
                numHive = authoring.numHives,
            };
            AddComponent(entity, spawner);
        }
    }
}

public struct HiveSpawner : IComponentData
{
    public Entity HivePrefab;
    public int numHive;
}