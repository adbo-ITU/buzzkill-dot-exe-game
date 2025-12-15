using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MonoBehaviour that bridges the main camera position to the ECS world.
/// Attach to an empty GameObject in the scene or on the main camera itself.
/// </summary>
public class CameraPositionBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private Entity _singletonEntity;
    private bool _initialized;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Lazy initialization - wait for World to be ready
        if (!_initialized)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            _entityManager = world.EntityManager;

            // Create singleton entity if it doesn't exist
            var query = _entityManager.CreateEntityQuery(typeof(CameraPosition));
            if (query.IsEmpty)
            {
                _singletonEntity = _entityManager.CreateEntity(typeof(CameraPosition));
            }
            else
            {
                _singletonEntity = query.GetSingletonEntity();
            }
            _initialized = true;
        }

        // Update camera position singleton every frame
        if (_entityManager.Exists(_singletonEntity))
        {
            _entityManager.SetComponentData(_singletonEntity, new CameraPosition
            {
                Value = cam.transform.position
            });
        }
    }

    void OnDestroy()
    {
        // Cleanup if world still exists
        if (_initialized && World.DefaultGameObjectInjectionWorld != null
            && World.DefaultGameObjectInjectionWorld.IsCreated
            && _entityManager.Exists(_singletonEntity))
        {
            _entityManager.DestroyEntity(_singletonEntity);
        }
    }
}
