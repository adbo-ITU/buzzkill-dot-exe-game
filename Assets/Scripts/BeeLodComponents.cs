using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Singleton component holding the main camera's world position.
/// Updated each frame by CameraPositionBridge MonoBehaviour.
/// </summary>
public struct CameraPosition : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Links the detailed bee entity to its far-LOD cube representation.
/// Added to bee root entities during spawning.
/// </summary>
public struct BeeLodLink : IComponentData
{
    /// <summary>Reference to the bee root entity (near LOD)</summary>
    public Entity NearRoot;

    /// <summary>Reference to the far-LOD cube entity</summary>
    public Entity FarEntity;

    /// <summary>Squared distance threshold for LOD switching (default: 100*100 = 10000)</summary>
    public float SwitchDistanceSq;

    /// <summary>Current LOD state: 0 = near (detailed bee visible), 1 = far (cube visible)</summary>
    public byte IsFar;
}

/// <summary>
/// Tag component to identify entities that need LOD initialization.
/// Used to handle deferred ECB timing - cube rendering is disabled on first LOD system pass.
/// </summary>
public struct BeeLodNeedsInit : IComponentData, IEnableableComponent { }
