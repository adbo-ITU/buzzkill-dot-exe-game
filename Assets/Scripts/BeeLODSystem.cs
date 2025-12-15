using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct BeeLODSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeeLODConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var camera = Camera.main;
        if (camera == null) return;

        var cameraPos = (float3)camera.transform.position;

        if (!SystemAPI.TryGetSingletonEntity<BeeLODConfig>(out var configEntity))
            return;

        var config = SystemAPI.GetComponent<BeeLODConfig>(configEntity);

        // Skip if no low-poly prefab configured
        if (config.lowPolyPrefab == Entity.Null)
        {
            Debug.LogWarning("BeeLODSystem: No low-poly prefab assigned in BeeSpawner!");
            state.Enabled = false;
            return;
        }

        // Multi-frame initialization
        if (!config.isInitialized)
        {
            // Step 1: Find high-poly mesh from existing bee render entities
            // Use the most common mesh among entities with LocalToWorld (actual rendered objects we can process)
            if (!config.foundHighPoly)
            {
                var meshCounts = new Unity.Collections.NativeHashMap<int, int>(64, Unity.Collections.Allocator.Temp);
                foreach (var (meshInfo, ltw) in SystemAPI.Query<RefRO<MaterialMeshInfo>, RefRO<LocalToWorld>>())
                {
                    var m = meshInfo.ValueRO.Mesh;
                    if (meshCounts.ContainsKey(m))
                        meshCounts[m] = meshCounts[m] + 1;
                    else
                        meshCounts[m] = 1;
                }

                int maxCount = 0;
                int mostCommonMesh = 0;
                foreach (var kvp in meshCounts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        mostCommonMesh = kvp.Key;
                    }
                }

                // Debug: show all mesh counts
                string meshList = "";
                foreach (var kvp in meshCounts)
                {
                    meshList += $"[{kvp.Key}:{kvp.Value}] ";
                }
                Debug.Log($"BeeLODSystem: Meshes with LocalToWorld: {meshList}");

                meshCounts.Dispose();

                if (maxCount > 0)
                {
                    config.highPolyMesh = mostCommonMesh;
                    // Find material for this mesh
                    foreach (var (meshInfo, ltw) in SystemAPI.Query<RefRO<MaterialMeshInfo>, RefRO<LocalToWorld>>())
                    {
                        if (meshInfo.ValueRO.Mesh == mostCommonMesh)
                        {
                            config.highPolyMaterial = meshInfo.ValueRO.Material;
                            break;
                        }
                    }
                    config.foundHighPoly = true;
                    SystemAPI.SetComponent(configEntity, config);
                    Debug.Log($"BeeLODSystem: Picked high-poly mesh={config.highPolyMesh}, material={config.highPolyMaterial} (count={maxCount})");
                }
                return; // Continue next frame
            }

            // Step 2: Count meshes before spawning, then spawn temp low-poly entity
            if (!config.spawnedTempEntity)
            {
                // Count existing meshes
                var meshCountsBefore = new Unity.Collections.NativeHashMap<int, int>(64, Unity.Collections.Allocator.Temp);
                foreach (var meshInfo in SystemAPI.Query<RefRO<MaterialMeshInfo>>())
                {
                    var m = meshInfo.ValueRO.Mesh;
                    if (meshCountsBefore.ContainsKey(m))
                        meshCountsBefore[m] = meshCountsBefore[m] + 1;
                    else
                        meshCountsBefore[m] = 1;
                }

                // Store counts in a temporary way - use lowPolyMesh to store count of meshes
                config.lowPolyMesh = meshCountsBefore.Count;
                meshCountsBefore.Dispose();

                var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
                ecb.Instantiate(config.lowPolyPrefab);
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                config.spawnedTempEntity = true;
                SystemAPI.SetComponent(configEntity, config);
                Debug.Log($"BeeLODSystem: Spawned temp low-poly entity, had {config.lowPolyMesh} unique meshes before");
                return; // Continue next frame
            }

            // Step 3: Find the NEW mesh that appeared after spawning
            var meshCountsAfter = new Unity.Collections.NativeHashMap<int, int>(64, Unity.Collections.Allocator.Temp);
            foreach (var meshInfo in SystemAPI.Query<RefRO<MaterialMeshInfo>>())
            {
                var m = meshInfo.ValueRO.Mesh;
                if (meshCountsAfter.ContainsKey(m))
                    meshCountsAfter[m] = meshCountsAfter[m] + 1;
                else
                    meshCountsAfter[m] = 1;
            }

            Debug.Log($"BeeLODSystem: Now have {meshCountsAfter.Count} unique meshes");

            // Find any mesh with count=1 that's not the high-poly mesh (likely our new temp entity)
            int newMesh = 0;
            bool foundNew = false;
            foreach (var kvp in meshCountsAfter)
            {
                if (kvp.Value == 1 && kvp.Key != config.highPolyMesh)
                {
                    newMesh = kvp.Key;
                    foundNew = true;
                    Debug.Log($"BeeLODSystem: Found new mesh {kvp.Key} with count {kvp.Value}");
                    break;
                }
            }
            meshCountsAfter.Dispose();

            if (foundNew)
            {
                config.lowPolyMesh = newMesh;
                // Find material and entity for this mesh
                Entity entityToDestroy = Entity.Null;
                foreach (var (meshInfo, entity) in SystemAPI.Query<RefRO<MaterialMeshInfo>>().WithEntityAccess())
                {
                    if (meshInfo.ValueRO.Mesh == newMesh)
                    {
                        config.lowPolyMaterial = meshInfo.ValueRO.Material;
                        entityToDestroy = entity;
                        break;
                    }
                }

                if (entityToDestroy != Entity.Null)
                {
                    state.EntityManager.DestroyEntity(entityToDestroy);
                }

                config.isInitialized = true;
                SystemAPI.SetComponent(configEntity, config);
                Debug.Log($"BeeLODSystem: Initialized! highPoly=({config.highPolyMesh},{config.highPolyMaterial}), lowPoly=({config.lowPolyMesh},{config.lowPolyMaterial})");
            }
            else
            {
                // Debug: show all meshes and counts
                var debugCounts = new Unity.Collections.NativeHashMap<int, int>(64, Unity.Collections.Allocator.Temp);
                foreach (var meshInfo in SystemAPI.Query<RefRO<MaterialMeshInfo>>())
                {
                    var m = meshInfo.ValueRO.Mesh;
                    if (debugCounts.ContainsKey(m))
                        debugCounts[m] = debugCounts[m] + 1;
                    else
                        debugCounts[m] = 1;
                }

                string meshList = "";
                foreach (var kvp in debugCounts)
                {
                    meshList += $"[{kvp.Key}:{kvp.Value}] ";
                }
                debugCounts.Dispose();

                Debug.Log($"BeeLODSystem: Still waiting... All meshes: {meshList}");
            }
            return;
        }

        // Debug: count meshes and distances
        int highPolyCount = 0, lowPolyCount = 0, otherCount = 0;
        int nearCount = 0, farCount = 0;
        foreach (var (meshInfo, ltw) in SystemAPI.Query<RefRO<MaterialMeshInfo>, RefRO<LocalToWorld>>())
        {
            var m = meshInfo.ValueRO.Mesh;
            if (m == config.highPolyMesh) highPolyCount++;
            else if (m == config.lowPolyMesh) lowPolyCount++;
            else otherCount++;

            // Count near/far for bee meshes only
            if (m == config.highPolyMesh || m == config.lowPolyMesh)
            {
                var distSq = math.lengthsq(ltw.ValueRO.Position - cameraPos);
                if (distSq < config.lodDistanceSq) nearCount++;
                else farCount++;
            }
        }

        if (Time.frameCount % 60 == 0) // Log every 60 frames
        {
            Debug.Log($"BeeLODSystem: highPolyID={config.highPolyMesh}, lowPolyID={config.lowPolyMesh}");
            Debug.Log($"BeeLODSystem: counts - highPoly={highPolyCount}, lowPoly={lowPolyCount}, other={otherCount}");
            Debug.Log($"BeeLODSystem: distance - near={nearCount}, far={farCount}, camPos={cameraPos}");
        }

        // Process all render entities with MaterialMeshInfo + LocalTransform
        new BeeLODJob
        {
            cameraPos = cameraPos,
            lodDistanceSq = config.lodDistanceSq,
            lowPolyMesh = config.lowPolyMesh,
            lowPolyMaterial = config.lowPolyMaterial,
            highPolyMesh = config.highPolyMesh,
            highPolyMaterial = config.highPolyMaterial
        }.ScheduleParallel(state.Dependency).Complete();
    }
}

[BurstCompile]
public partial struct BeeLODJob : IJobEntity
{
    [ReadOnly] public float3 cameraPos;
    [ReadOnly] public float lodDistanceSq;
    [ReadOnly] public int lowPolyMesh;
    [ReadOnly] public int lowPolyMaterial;
    [ReadOnly] public int highPolyMesh;
    [ReadOnly] public int highPolyMaterial;

    public void Execute(ref MaterialMeshInfo meshInfo, in LocalToWorld ltw)
    {
        // Only process bee meshes (skip other rendered objects like flowers, hives, etc.)
        var currentMesh = meshInfo.Mesh;
        if (currentMesh != highPolyMesh && currentMesh != lowPolyMesh)
            return;

        var distanceSq = math.lengthsq(ltw.Position - cameraPos);
        var shouldBeLowPoly = distanceSq >= lodDistanceSq;
        var isCurrentlyLowPoly = currentMesh == lowPolyMesh;

        if (shouldBeLowPoly && !isCurrentlyLowPoly)
        {
            meshInfo.Mesh = lowPolyMesh;
            meshInfo.Material = lowPolyMaterial;
        }
        else if (!shouldBeLowPoly && isCurrentlyLowPoly)
        {
            meshInfo.Mesh = highPolyMesh;
            meshInfo.Material = highPolyMaterial;
        }
    }
}
