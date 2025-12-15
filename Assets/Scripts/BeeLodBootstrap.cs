using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Bootstrap MonoBehaviour that initializes the LOD system.
/// Creates and stores the shared RenderMeshArray containing both near (bee) and far (cube) meshes.
/// Must be present in the scene before bees spawn.
/// </summary>
public class BeeLodBootstrap : MonoBehaviour
{
    [Header("Near LOD (detailed bee)")]
    [Tooltip("The high-poly bee mesh")]
    public Mesh beeMesh;

    [Tooltip("The bee's material")]
    public Material beeMaterial;

    [Header("Settings")]
    [Tooltip("Distance in meters at which bees switch to cube LOD")]
    public float switchDistance = 100f;

    // Static storage for access from ECS systems
    private static RenderMeshArray s_RenderMeshArray;
    private static bool s_Initialized;

    /// <summary>
    /// Gets the shared RenderMeshArray containing both LOD variants.
    /// Index 0 = bee mesh/material (near), Index 1 = cube mesh/yellow material (far)
    /// </summary>
    public static RenderMeshArray GetRenderMeshArray()
    {
        return s_RenderMeshArray;
    }

    /// <summary>
    /// Returns true if the bootstrap has completed initialization.
    /// </summary>
    public static bool IsInitialized => s_Initialized;

    void Awake()
    {
        if (beeMesh == null)
        {
            Debug.LogError("BeeLodBootstrap: beeMesh is not assigned!");
            return;
        }

        if (beeMaterial == null)
        {
            Debug.LogError("BeeLodBootstrap: beeMaterial is not assigned!");
            return;
        }

        // Get Unity's built-in cube mesh
        var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (cubeMesh == null)
        {
            // Fallback: create a temporary cube and grab its mesh
            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);
        }

        // Create yellow material programmatically using URP Unlit shader
        Material yellowMaterial;
        var urpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlitShader != null)
        {
            yellowMaterial = new Material(urpUnlitShader);
            yellowMaterial.SetColor("_BaseColor", Color.yellow);
        }
        else
        {
            // Fallback to standard unlit if URP not found
            var standardUnlit = Shader.Find("Unlit/Color");
            if (standardUnlit != null)
            {
                yellowMaterial = new Material(standardUnlit);
                yellowMaterial.SetColor("_Color", Color.yellow);
            }
            else
            {
                Debug.LogError("BeeLodBootstrap: Could not find suitable shader for yellow material!");
                return;
            }
        }
        yellowMaterial.name = "LOD_Yellow_Cube";

        // Create RenderMeshArray with both LOD variants
        // Index 0: bee material, Index 1: yellow cube material
        // Mesh Index 0: bee mesh, Mesh Index 1: cube mesh
        s_RenderMeshArray = new RenderMeshArray(
            new Material[] { beeMaterial, yellowMaterial },
            new Mesh[] { beeMesh, cubeMesh }
        );

        // Create singleton entity with LOD config
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var em = world.EntityManager;

            // Create LOD config singleton
            var configEntity = em.CreateEntity();
            em.AddComponentData(configEntity, new BeeLodConfig
            {
                SwitchDistance = switchDistance
            });
#if UNITY_EDITOR
            em.SetName(configEntity, "BeeLodConfig");
#endif
        }

        s_Initialized = true;
        Debug.Log($"BeeLodBootstrap initialized: switchDistance={switchDistance}m, beeMesh={beeMesh.name}");
    }

    void OnDestroy()
    {
        s_Initialized = false;
        s_RenderMeshArray = default;
    }
}
