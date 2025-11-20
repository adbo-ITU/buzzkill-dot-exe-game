using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;

public partial struct DebugSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Check bees that SHOULD be dynamic
        foreach (var (mass, vel, transform) 
                 in SystemAPI.Query<RefRO<PhysicsMass>, RefRO<PhysicsVelocity>, RefRO<LocalTransform>>()
                     .WithAll<BeeData>())
        {
            UnityEngine.Debug.Log(
                $"Bee dynamic: invMass={mass.ValueRO.InverseMass}, vel={vel.ValueRO.Linear}, pos={transform.ValueRO.Position}");
            break; // spam just one
        }
    }
}