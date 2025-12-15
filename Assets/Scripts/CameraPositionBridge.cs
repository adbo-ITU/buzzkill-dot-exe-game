using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MonoBehaviour that bridges Unity's Camera position to the ECS world.
/// Creates and updates a CameraPosition singleton each frame.
/// Attach this to the Main Camera or any GameObject in the scene.
/// </summary>
public class CameraPositionBridge : MonoBehaviour
{
    private Entity _cameraPositionEntity;
    private EntityManager _entityManager;
    private bool _initialized;
    private Camera _cachedCamera;

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("CameraPositionBridge: No default World found!");
            return;
        }

        _entityManager = world.EntityManager;

        // Cache camera reference
        _cachedCamera = Camera.main;
        if (_cachedCamera == null)
        {
            Debug.LogWarning("CameraPositionBridge: Camera.main not found, will try again in LateUpdate");
        }

        // Create singleton entity
        _cameraPositionEntity = _entityManager.CreateEntity();
        _entityManager.AddComponentData(_cameraPositionEntity, new CameraPosition
        {
            Value = _cachedCamera != null ? (float3)_cachedCamera.transform.position : float3.zero
        });
#if UNITY_EDITOR
        _entityManager.SetName(_cameraPositionEntity, "CameraPosition");
#endif

        _initialized = true;
    }

    void LateUpdate()
    {
        if (!_initialized) return;

        // Try to get camera if not cached
        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;
        }

        // Update singleton with current camera position
        _entityManager.SetComponentData(_cameraPositionEntity, new CameraPosition
        {
            Value = _cachedCamera.transform.position
        });
    }

    void OnDestroy()
    {
        if (_initialized && World.DefaultGameObjectInjectionWorld != null &&
            _entityManager.Exists(_cameraPositionEntity))
        {
            _entityManager.DestroyEntity(_cameraPositionEntity);
        }
        _initialized = false;
    }
}
