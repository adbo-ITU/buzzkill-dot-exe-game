using Unity.Entities;
using UnityEngine;

public class BeeSpawnerAuthoring : MonoBehaviour
{
    [Header("Bee Prefab")]
    public GameObject beePrefab;

    [Header("LOD Settings")]
    [Tooltip("Low-poly prefab to use when bee is far from camera (must have MeshRenderer)")]
    public GameObject lowPolyPrefab;

    [Tooltip("Distance at which to switch to low-poly mesh")]
    public float lodDistance = 100f;

    class Baker : Baker<BeeSpawnerAuthoring>
    {
        public override void Bake(BeeSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);

            // Bee spawner component
            AddComponent(entity, new BeeSpawner
            {
                beePrefab = GetEntity(authoring.beePrefab, TransformUsageFlags.Dynamic),
            });

            // LOD config - store reference to low-poly prefab entity
            var lowPolyPrefabEntity = Entity.Null;
            if (authoring.lowPolyPrefab != null)
            {
                lowPolyPrefabEntity = GetEntity(authoring.lowPolyPrefab, TransformUsageFlags.Renderable);
            }

            AddComponent(entity, new BeeLODConfig
            {
                lodDistanceSq = authoring.lodDistance * authoring.lodDistance,
                lowPolyPrefab = lowPolyPrefabEntity
            });
        }
    }
}

public struct BeeSpawner : IComponentData
{
    public Entity beePrefab;
}