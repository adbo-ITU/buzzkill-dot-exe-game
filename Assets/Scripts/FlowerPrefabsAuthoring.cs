using Unity.Entities;
using UnityEngine;

public class FlowerPrefabsAuthoring : MonoBehaviour
{
    public GameObject flowerPrefabA;
    public GameObject flowerPrefabB;
    public GameObject flowerPrefabC;
    public GameObject flowerPrefabD;
    public GameObject flowerPrefabE;

    class Baker : Baker<FlowerPrefabsAuthoring>
    {
        public override void Bake(FlowerPrefabsAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            var spawner = new FlowerPrefabs
            {
                flowerPrefabA = GetEntity(authoring.flowerPrefabA, TransformUsageFlags.Dynamic),
                flowerPrefabB = GetEntity(authoring.flowerPrefabB, TransformUsageFlags.Dynamic),
                flowerPrefabC = GetEntity(authoring.flowerPrefabC, TransformUsageFlags.Dynamic),
                flowerPrefabD = GetEntity(authoring.flowerPrefabD, TransformUsageFlags.Dynamic),
                flowerPrefabE = GetEntity(authoring.flowerPrefabE, TransformUsageFlags.Dynamic),
            };
            AddComponent(entity, spawner);
        }
    }
}

public struct FlowerPrefabs : IComponentData
{
    public Entity flowerPrefabA;
    public Entity flowerPrefabB;
    public Entity flowerPrefabC;
    public Entity flowerPrefabD;
    public Entity flowerPrefabE;
}