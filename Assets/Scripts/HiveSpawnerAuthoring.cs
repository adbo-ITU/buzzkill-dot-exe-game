using Unity.Entities;
using UnityEngine;

public class HiveSpawnerAuthoring : MonoBehaviour
{
    public GameObject hivePrefab;
    public int numHives;

    class Baker : Baker<HiveSpawnerAuthoring>
    {
        public override void Bake(HiveSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var spawner = new HiveSpawner
            {
                hivePrefab = GetEntity(authoring.hivePrefab, TransformUsageFlags.Dynamic),
                numHives = authoring.numHives,
            };
            AddComponent(entity, spawner);
        }
    }
}

public struct HiveSpawner : IComponentData
{
    public Entity hivePrefab;
    public int numHives;
}