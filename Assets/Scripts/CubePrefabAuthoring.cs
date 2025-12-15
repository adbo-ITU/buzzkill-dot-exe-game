using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component for the far-LOD cube prefab.
/// Attach to the yellow cube prefab GameObject along with a MeshRenderer.
/// The MeshRenderer will automatically provide MaterialMeshInfo during baking.
/// </summary>
public class CubePrefabAuthoring : MonoBehaviour
{
    class Baker : Baker<CubePrefabAuthoring>
    {
        public override void Bake(CubePrefabAuthoring authoring)
        {
            // TransformUsageFlags.Dynamic is required for the cube to follow the bee via Parent component
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            // No additional components needed - the cube just needs:
            // - LocalTransform (automatic from TransformUsageFlags.Dynamic)
            // - MaterialMeshInfo (automatic from MeshRenderer on the prefab)
            // - Parent component will be added at spawn time by BeeSpawnerSystem
        }
    }
}
