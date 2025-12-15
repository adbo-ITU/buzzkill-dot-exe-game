using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tag to identify entities that participate in bee LOD system.
/// </summary>
public struct BeeLodTag : IComponentData { }

/// <summary>
/// Per-entity LOD state tracking. Stores indices into the shared RenderMeshArray
/// and the current LOD state to avoid redundant MaterialMeshInfo writes.
/// </summary>
public struct DistanceLod : IComponentData
{
    /// <summary>Squared distance threshold for LOD switch (e.g., 100*100 = 10000)</summary>
    public float SwitchDistanceSq;

    /// <summary>Mesh index for near/detailed LOD (into RenderMeshArray.Meshes)</summary>
    public int NearMeshIndex;

    /// <summary>Material index for near/detailed LOD (into RenderMeshArray.Materials)</summary>
    public int NearMaterialIndex;

    /// <summary>Mesh index for far/simple LOD (into RenderMeshArray.Meshes)</summary>
    public int FarMeshIndex;

    /// <summary>Material index for far/simple LOD (into RenderMeshArray.Materials)</summary>
    public int FarMaterialIndex;

    /// <summary>Current state: 0 = Near (detailed), 1 = Far (simple cube)</summary>
    public byte State;
}

/// <summary>
/// Singleton component providing camera world position to Burst jobs.
/// Updated each frame by CameraPositionBridge MonoBehaviour.
/// </summary>
public struct CameraPosition : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Singleton config created by BeeLodBootstrap.
/// Used to synchronize initialization order with BeeSpawnerSystem.
/// </summary>
public struct BeeLodConfig : IComponentData
{
    public float SwitchDistance;
}
