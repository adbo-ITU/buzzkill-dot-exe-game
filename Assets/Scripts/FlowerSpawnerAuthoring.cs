using Unity.Entities;
using UnityEngine;

public class FlowerSpawnerAuthoring : MonoBehaviour
{
    public GameObject flowerPrefab;
    public int numFlowers;

    class Baker : Baker<FlowerSpawnerAuthoring>
    {
        public override void Bake(FlowerSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var spawner = new FlowerSpawner
            {
                flowerPrefab = GetEntity(authoring.flowerPrefab, TransformUsageFlags.Dynamic),
                numFlower = authoring.numFlowers,
            };
            AddComponent(entity, spawner);
        }
    }
}

public struct FlowerSpawner : IComponentData
{
    public Entity flowerPrefab;
    public int numFlower;
}