using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-time system that configures LOD components on spawned bee entities.
/// Runs in SimulationSystemGroup after ECB playback creates the bee entities.
/// Uses SystemBase because RenderMeshUtility requires managed code (not Burst-compatible).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class BeeLodSetupSystem : SystemBase
{
    private bool _hasRun;
    private EntityQuery _beeQuery;

    protected override void OnCreate()
    {
        Debug.Log("BeeLodSetupSystem: OnCreate called");

        // Query for bee entities that don't have LOD setup yet
        _beeQuery = GetEntityQuery(
            ComponentType.ReadOnly<BeeData>(),
            ComponentType.Exclude<BeeLodTag>()
        );

        // Only run when we have config and unprocessed bees
        RequireForUpdate<BeeLodConfig>();

        Debug.Log("BeeLodSetupSystem: Waiting for BeeLodConfig singleton (add BeeLodBootstrap to scene)");
    }

    protected override void OnUpdate()
    {
        // Only run once
        if (_hasRun)
        {
            Enabled = false;
            return;
        }

        // Wait for bootstrap to initialize
        if (!BeeLodBootstrap.IsInitialized)
        {
            Debug.Log("BeeLodSetupSystem: Waiting for BeeLodBootstrap to initialize...");
            return;
        }

        // Check if there are bees to process
        if (_beeQuery.IsEmpty)
        {
            Debug.Log("BeeLodSetupSystem: No bees without BeeLodTag found yet...");
            return;
        }

        var lodConfig = SystemAPI.GetSingleton<BeeLodConfig>();
        float switchDistanceSq = lodConfig.SwitchDistance * lodConfig.SwitchDistance;

        // Create render mesh description for the entities
        var renderMeshDescription = new RenderMeshDescription(ShadowCastingMode.On, true);

        var renderMeshArray = BeeLodBootstrap.GetRenderMeshArray();

        // Get all bee entities that need LOD setup
        var entities = _beeQuery.ToEntityArray(Allocator.Temp);

        Debug.Log($"BeeLodSetupSystem: Configuring {entities.Length} bee entities with LOD");

        foreach (var entity in entities)
        {
            // Add LOD tracking component
            EntityManager.AddComponentData(entity, new DistanceLod
            {
                SwitchDistanceSq = switchDistanceSq,
                NearMeshIndex = 0,      // bee mesh
                NearMaterialIndex = 0,  // bee material
                FarMeshIndex = 1,       // cube mesh
                FarMaterialIndex = 1,   // yellow material
                State = 0               // Start as near (detailed)
            });

            // Add tag for LOD system query
            EntityManager.AddComponent<BeeLodTag>(entity);

            // Replace baked rendering with our multi-mesh RenderMeshArray setup
            RenderMeshUtility.AddComponents(
                entity,
                EntityManager,
                renderMeshDescription,
                renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0) // Start with bee mesh
            );
        }

        entities.Dispose();

        _hasRun = true;
        Debug.Log("BeeLodSetupSystem: LOD setup complete, disabling system");
    }
}
