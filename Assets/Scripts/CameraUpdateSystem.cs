using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateBefore(typeof(BeeFlyingSystem))]
partial struct CameraUpdateSystem : ISystem
{
    private Entity _cameraEntity;

    public void OnCreate(ref SystemState state)
    {
        _cameraEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(_cameraEntity, new CameraData { position = float3.zero });
    }

    public void OnUpdate(ref SystemState state)
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            var cameraPos = (float3)mainCamera.transform.position;
            SystemAPI.SetSingleton(new CameraData { position = cameraPos });
        }
    }

    public void OnDestroy(ref SystemState state)
    {
    }
}
