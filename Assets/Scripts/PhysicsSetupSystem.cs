using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

public partial struct PhysicsSetupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // state.Enabled = true;
        //
        // Entity stepEntity;
        // if (!SystemAPI.TryGetSingletonEntity<PhysicsStep>(out stepEntity))
        // {
        //     stepEntity = state.EntityManager.CreateEntity();
        // }
        //
        // var step = PhysicsStep.Default;
        // step.Gravity        = new float3(0, -9.81f, 0);
        // step.SimulationType = SimulationType.UnityPhysics;
        //
        // state.EntityManager.AddComponentData(stepEntity, step);
    }

    public void OnUpdate(ref SystemState state) { }
}