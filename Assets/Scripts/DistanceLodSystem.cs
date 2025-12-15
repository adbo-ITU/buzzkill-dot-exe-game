using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Burst-compiled system that swaps MaterialMeshInfo based on distance from camera.
/// Runs in PresentationSystemGroup before rendering to ensure LOD changes apply to current frame.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct DistanceLodSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Only run when we have camera position and at least one LOD entity
        state.RequireForUpdate<CameraPosition>();
        state.RequireForUpdate<BeeLodTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var cameraPos = SystemAPI.GetSingleton<CameraPosition>().Value;

        // Schedule parallel job for LOD switching
        new DistanceLodJob
        {
            CameraPosition = cameraPos
        }.ScheduleParallel();
    }
}

/// <summary>
/// Burst-compiled job that evaluates distance and swaps MaterialMeshInfo indices.
/// Only writes to MaterialMeshInfo when state actually changes to avoid unnecessary work.
/// </summary>
[BurstCompile]
public partial struct DistanceLodJob : IJobEntity
{
    [ReadOnly] public float3 CameraPosition;

    public void Execute(
        ref DistanceLod lod,
        ref MaterialMeshInfo meshInfo,
        in LocalToWorld localToWorld,
        in BeeLodTag tag)
    {
        // Calculate squared distance to camera (avoids sqrt)
        var delta = localToWorld.Position - CameraPosition;
        var distSq = math.lengthsq(delta);

        // Determine desired LOD state: 0 = near (detailed), 1 = far (cube)
        byte desiredState = distSq >= lod.SwitchDistanceSq ? (byte)1 : (byte)0;

        // Only update if state changed (avoid redundant writes)
        if (lod.State != desiredState)
        {
            lod.State = desiredState;

            if (desiredState == 0)
            {
                // Near: use detailed bee mesh
                meshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    lod.NearMaterialIndex,
                    lod.NearMeshIndex
                );
            }
            else
            {
                // Far: use simple yellow cube
                meshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    lod.FarMaterialIndex,
                    lod.FarMeshIndex
                );
            }
        }
    }
}
