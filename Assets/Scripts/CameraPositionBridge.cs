using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CameraPositionBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private Entity _singletonEntity;
    private bool _initialized;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        if (!_initialized)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            _entityManager = world.EntityManager;
            var query = _entityManager.CreateEntityQuery(typeof(CameraPosition));
            _singletonEntity = query.IsEmpty
                ? _entityManager.CreateEntity(typeof(CameraPosition))
                : query.GetSingletonEntity();
            _initialized = true;
        }

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
        if (_initialized && World.DefaultGameObjectInjectionWorld != null
            && World.DefaultGameObjectInjectionWorld.IsCreated
            && _entityManager.Exists(_singletonEntity))
        {
            _entityManager.DestroyEntity(_singletonEntity);
        }
    }
}
