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
/// Links the bee root entity to its far-LOD cube visual.
/// Attached to the bee root entity (which has physics/gameplay).
/// Only rendering is toggled - physics stays active at all distances.
/// </summary>
public struct RenderLodLink : IComponentData
{
    /// <summary>Reference to the far-LOD cube visual entity (child of bee root)</summary>
    public Entity CubeVisual;

    /// <summary>Squared distance threshold for LOD switching (default: 100*100 = 10000)</summary>
    public float SwitchDistanceSq;

    /// <summary>Current LOD state: 0 = near (detailed bee visible), 1 = far (cube visible)</summary>
    public byte IsFar;
}

/// <summary>
/// Tag component to identify entities that need LOD initialization.
/// Used to handle deferred ECB timing - cube rendering is disabled on first LOD system pass.
/// </summary>
public struct RenderLodNeedsInit : IComponentData, IEnableableComponent { }

