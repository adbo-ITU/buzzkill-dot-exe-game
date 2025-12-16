using Unity.Entities;
using Unity.Mathematics;

public struct CameraPosition : IComponentData
{
    public float3 Value;
}

public struct RenderLodLink : IComponentData
{
    public Entity CubeVisual;
    public float SwitchDistanceSq;
    public byte IsFar;
}

public struct RenderLodNeedsInit : IComponentData, IEnableableComponent { }
