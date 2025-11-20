using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct StepDebug : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        UnityEngine.Debug.Log($"[FixedStep] tick t={SystemAPI.Time.ElapsedTime}");
    }
}