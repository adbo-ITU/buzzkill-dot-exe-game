using Unity.Entities;
using UnityEngine;

public class CubePrefabAuthoring : MonoBehaviour
{
    class Baker : Baker<CubePrefabAuthoring>
    {
        public override void Bake(CubePrefabAuthoring authoring)
        {
            GetEntity(authoring, TransformUsageFlags.Dynamic);
        }
    }
}
