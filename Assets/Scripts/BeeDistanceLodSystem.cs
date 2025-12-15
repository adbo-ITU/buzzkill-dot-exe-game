using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Handles distance-based LOD switching for bee entities.
/// ONLY toggles rendering on child entities with MaterialMeshInfo.
/// The root entity (physics/gameplay) is NEVER toggled - stays active at all distances.
/// </summary>
[UpdateAfter(typeof(BeeFlyingSystem))]
public partial struct BeeDistanceLodSystem : ISystem
{
    // Small hysteresis margin to prevent flickering at threshold boundary
    private const float HysteresisMargin = 5f;
    private const float LodDistanceThreshold = 500f;
    private const float NearToFarThresholdSq = (LodDistanceThreshold + HysteresisMargin) * (LodDistanceThreshold + HysteresisMargin);
    private const float FarToNearThresholdSq = (LodDistanceThreshold - HysteresisMargin) * (LodDistanceThreshold - HysteresisMargin);

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraPosition>();
        state.RequireForUpdate<RenderLodLink>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var cameraPosition = SystemAPI.GetSingleton<CameraPosition>().Value;

        // Phase 1: Initialize newly spawned bees (disable cube rendering initially)
        int initCount = InitializeNewBees(ref state);

        // Phase 2: Update LOD based on distance
        int switchCount = UpdateLodDistances(ref state, cameraPosition);

        // Debug output (remove after testing)
        if (initCount > 0 || switchCount > 0)
        {
            UnityEngine.Debug.Log($"[LOD] Camera at {cameraPosition}, Init: {initCount}, Switches: {switchCount}");
        }
    }

    private int InitializeNewBees(ref SystemState state)
    {
        int count = 0;
        // Process bees that need initialization (just spawned)
        // Disable cube's MaterialMeshInfo since we start in near LOD
        foreach (var (lodLink, needsInit, entity) in
            SystemAPI.Query<RefRO<RenderLodLink>, EnabledRefRW<RenderLodNeedsInit>>()
                .WithEntityAccess())
        {
            var cubeEntity = lodLink.ValueRO.CubeVisual;

            // Disable cube rendering (far LOD starts hidden)
            if (SystemAPI.HasComponent<MaterialMeshInfo>(cubeEntity))
            {
                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(cubeEntity, false);
            }

            // Clear initialization flag - done processing this bee
            needsInit.ValueRW = false;
            count++;
        }
        return count;
    }

    private int UpdateLodDistances(ref SystemState state, float3 cameraPosition)
    {
        int switchCount = 0;
        // Get lookups for LinkedEntityGroup and MaterialMeshInfo
        var linkedGroupLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
        var materialMeshInfoLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);

        foreach (var (lodLink, transform, beeRootEntity) in
            SystemAPI.Query<RefRW<RenderLodLink>, RefRO<LocalTransform>>()
                .WithDisabled<RenderLodNeedsInit>()  // Skip bees still initializing
                .WithEntityAccess())
        {
            var beePosition = transform.ValueRO.Position;
            var distanceSq = math.distancesq(beePosition, cameraPosition);

            var currentlyFar = lodLink.ValueRO.IsFar != 0;
            var shouldBeFar = ShouldBeFarLod(distanceSq, currentlyFar);

            // Only process if LOD state needs to change
            if (currentlyFar == shouldBeFar) continue;

            // Update state
            lodLink.ValueRW.IsFar = shouldBeFar ? (byte)1 : (byte)0;
            switchCount++;

            var cubeEntity = lodLink.ValueRO.CubeVisual;

            if (shouldBeFar)
            {
                // Switch to FAR LOD: Disable bee visual meshes, enable cube
                SetBeeChildVisualsRendering(beeRootEntity, false, ref linkedGroupLookup, ref materialMeshInfoLookup);
                if (materialMeshInfoLookup.HasComponent(cubeEntity))
                {
                    materialMeshInfoLookup.SetComponentEnabled(cubeEntity, true);
                }
            }
            else
            {
                // Switch to NEAR LOD: Enable bee visual meshes, disable cube
                SetBeeChildVisualsRendering(beeRootEntity, true, ref linkedGroupLookup, ref materialMeshInfoLookup);
                if (materialMeshInfoLookup.HasComponent(cubeEntity))
                {
                    materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
                }
            }
        }
        return switchCount;
    }

    [BurstCompile]
    private static bool ShouldBeFarLod(float distanceSq, bool currentlyFar)
    {
        // Apply hysteresis to prevent flickering at boundary
        if (currentlyFar)
        {
            // Currently far - need to go below near threshold to switch to near
            return distanceSq >= FarToNearThresholdSq;
        }
        else
        {
            // Currently near - need to exceed far threshold to switch to far
            return distanceSq >= NearToFarThresholdSq;
        }
    }

    /// <summary>
    /// Toggle rendering on CHILD entities that have MaterialMeshInfo.
    /// CRITICAL: Never toggles the root entity - physics/gameplay stays active.
    /// </summary>
    [BurstCompile]
    private static void SetBeeChildVisualsRendering(
        in Entity rootEntity,
        bool enabled,
        ref BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        ref ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup)
    {
        // If no LinkedEntityGroup, there are no children to toggle
        if (!linkedGroupLookup.HasBuffer(rootEntity))
        {
            return;
        }

        var linkedGroup = linkedGroupLookup[rootEntity];

        for (int i = 0; i < linkedGroup.Length; i++)
        {
            var childEntity = linkedGroup[i].Value;

            // CRITICAL: Skip the root entity - never disable physics/gameplay
            if (childEntity == rootEntity) continue;

            // Only toggle MaterialMeshInfo on child entities that have it
            if (materialMeshInfoLookup.HasComponent(childEntity))
            {
                materialMeshInfoLookup.SetComponentEnabled(childEntity, enabled);
            }
        }
    }
}
