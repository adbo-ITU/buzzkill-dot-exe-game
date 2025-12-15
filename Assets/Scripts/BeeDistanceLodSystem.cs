using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Handles distance-based LOD switching for bee entities.
/// Toggles MaterialMeshInfo on detailed bee mesh entities and far-LOD cube
/// based on distance from camera.
/// </summary>
[UpdateAfter(typeof(BeeFlyingSystem))]
public partial struct BeeDistanceLodSystem : ISystem
{
    // Small hysteresis margin to prevent flickering at threshold boundary
    private const float HysteresisMargin = 5f;
    private const float LodDistanceThreshold = 100f;
    private const float NearToFarThresholdSq = (LodDistanceThreshold + HysteresisMargin) * (LodDistanceThreshold + HysteresisMargin);
    private const float FarToNearThresholdSq = (LodDistanceThreshold - HysteresisMargin) * (LodDistanceThreshold - HysteresisMargin);

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraPosition>();
        state.RequireForUpdate<BeeLodLink>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var cameraPosition = SystemAPI.GetSingleton<CameraPosition>().Value;

        // Phase 1: Initialize newly spawned bees (disable cube rendering initially)
        InitializeNewBees(ref state);

        // Phase 2: Update LOD based on distance
        UpdateLodDistances(ref state, cameraPosition);
    }

    [BurstCompile]
    private void InitializeNewBees(ref SystemState state)
    {
        // Process bees that need initialization (just spawned)
        // Disable cube's MaterialMeshInfo since we start in near LOD
        foreach (var (lodLink, needsInit, entity) in
            SystemAPI.Query<RefRO<BeeLodLink>, EnabledRefRW<BeeLodNeedsInit>>()
                .WithEntityAccess())
        {
            var cubeEntity = lodLink.ValueRO.FarEntity;

            // Disable cube rendering (far LOD starts hidden)
            if (SystemAPI.HasComponent<MaterialMeshInfo>(cubeEntity))
            {
                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(cubeEntity, false);
            }

            // Clear initialization flag - done processing this bee
            needsInit.ValueRW = false;
        }
    }

    [BurstCompile]
    private void UpdateLodDistances(ref SystemState state, float3 cameraPosition)
    {
        // Get lookups for LinkedEntityGroup and MaterialMeshInfo
        var linkedGroupLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
        var materialMeshInfoLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);

        foreach (var (lodLink, transform, entity) in
            SystemAPI.Query<RefRW<BeeLodLink>, RefRO<LocalTransform>>()
                .WithDisabled<BeeLodNeedsInit>()  // Skip bees still initializing
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

            var nearRoot = lodLink.ValueRO.NearRoot;
            var cubeEntity = lodLink.ValueRO.FarEntity;

            if (shouldBeFar)
            {
                // Switch to FAR LOD: Disable bee meshes, enable cube
                SetLinkedEntityGroupRendering(nearRoot, false, ref linkedGroupLookup, ref materialMeshInfoLookup);
                if (materialMeshInfoLookup.HasComponent(cubeEntity))
                {
                    materialMeshInfoLookup.SetComponentEnabled(cubeEntity, true);
                }
            }
            else
            {
                // Switch to NEAR LOD: Enable bee meshes, disable cube
                SetLinkedEntityGroupRendering(nearRoot, true, ref linkedGroupLookup, ref materialMeshInfoLookup);
                if (materialMeshInfoLookup.HasComponent(cubeEntity))
                {
                    materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
                }
            }
        }
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

    [BurstCompile]
    private static void SetLinkedEntityGroupRendering(
        in Entity rootEntity,
        bool enabled,
        ref BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        ref ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup)
    {
        // LinkedEntityGroup contains all entities in the prefab hierarchy
        // including the root entity itself
        if (!linkedGroupLookup.HasBuffer(rootEntity))
        {
            // No LinkedEntityGroup - just try to toggle on the root itself
            if (materialMeshInfoLookup.HasComponent(rootEntity))
            {
                materialMeshInfoLookup.SetComponentEnabled(rootEntity, enabled);
            }
            return;
        }

        var linkedGroup = linkedGroupLookup[rootEntity];

        for (int i = 0; i < linkedGroup.Length; i++)
        {
            var childEntity = linkedGroup[i].Value;

            // Only toggle MaterialMeshInfo on entities that have it
            // (skips physics-only entities, transform-only entities, etc.)
            if (materialMeshInfoLookup.HasComponent(childEntity))
            {
                materialMeshInfoLookup.SetComponentEnabled(childEntity, enabled);
            }
        }
    }
}
