using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[UpdateAfter(typeof(BeeFlyingSystem))]
public partial struct BeeDistanceLodSystem : ISystem
{
    private const float HysteresisMargin = 5f;
    private const float LodDistanceThreshold = 500f;
    private const float NearToFarThresholdSq = (LodDistanceThreshold + HysteresisMargin) * (LodDistanceThreshold + HysteresisMargin);
    private const float FarToNearThresholdSq = (LodDistanceThreshold - HysteresisMargin) * (LodDistanceThreshold - HysteresisMargin);

    private ComponentLookup<MaterialMeshInfo> _materialMeshInfoLookup;
    private BufferLookup<LinkedEntityGroup> _linkedGroupLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraPosition>();
        state.RequireForUpdate<RenderLodLink>();
        state.RequireForUpdate<SimulationConfig>();

        _materialMeshInfoLookup = state.GetComponentLookup<MaterialMeshInfo>(false);
        _linkedGroupLookup = state.GetBufferLookup<LinkedEntityGroup>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        var cameraPosition = SystemAPI.GetSingleton<CameraPosition>().Value;

        _materialMeshInfoLookup.Update(ref state);
        _linkedGroupLookup.Update(ref state);

        switch (config.executionMode)
        {
            case ExecutionMode.Scheduled:
            {
                new LodInitJob
                {
                    materialMeshInfoLookup = _materialMeshInfoLookup
                }.Schedule(state.Dependency).Complete();

                new LodUpdateJob
                {
                    cameraPosition = cameraPosition,
                    nearToFarThresholdSq = NearToFarThresholdSq,
                    farToNearThresholdSq = FarToNearThresholdSq,
                    materialMeshInfoLookup = _materialMeshInfoLookup,
                    linkedGroupLookup = _linkedGroupLookup
                }.Schedule(state.Dependency).Complete();
            }
            break;

            case ExecutionMode.ScheduledParallel:
            {
                new LodInitJob
                {
                    materialMeshInfoLookup = _materialMeshInfoLookup
                }.ScheduleParallel(state.Dependency).Complete();

                new LodUpdateJob
                {
                    cameraPosition = cameraPosition,
                    nearToFarThresholdSq = NearToFarThresholdSq,
                    farToNearThresholdSq = FarToNearThresholdSq,
                    materialMeshInfoLookup = _materialMeshInfoLookup,
                    linkedGroupLookup = _linkedGroupLookup
                }.ScheduleParallel(state.Dependency).Complete();
            }
            break;

            case ExecutionMode.MainThread:
            {
                foreach (var (lodLink, needsInit) in
                    SystemAPI.Query<RefRO<RenderLodLink>, EnabledRefRW<RenderLodNeedsInit>>())
                {
                    var cubeEntity = lodLink.ValueRO.CubeVisual;
                    if (_materialMeshInfoLookup.HasComponent(cubeEntity))
                        _materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
                    needsInit.ValueRW = false;
                }

                foreach (var (lodLink, transform, beeRootEntity) in
                    SystemAPI.Query<RefRW<RenderLodLink>, RefRO<LocalTransform>>()
                        .WithDisabled<RenderLodNeedsInit>()
                        .WithEntityAccess())
                {
                    var distanceSq = math.distancesq(transform.ValueRO.Position, cameraPosition);
                    var currentlyFar = lodLink.ValueRO.IsFar != 0;
                    var shouldBeFar = currentlyFar ? distanceSq >= FarToNearThresholdSq : distanceSq >= NearToFarThresholdSq;

                    if (currentlyFar == shouldBeFar) continue;

                    lodLink.ValueRW.IsFar = shouldBeFar ? (byte)1 : (byte)0;
                    var cubeEntity = lodLink.ValueRO.CubeVisual;

                    if (shouldBeFar)
                    {
                        SetChildVisualsEnabled(beeRootEntity, false, ref _linkedGroupLookup, ref _materialMeshInfoLookup);
                        if (_materialMeshInfoLookup.HasComponent(cubeEntity))
                            _materialMeshInfoLookup.SetComponentEnabled(cubeEntity, true);
                    }
                    else
                    {
                        SetChildVisualsEnabled(beeRootEntity, true, ref _linkedGroupLookup, ref _materialMeshInfoLookup);
                        if (_materialMeshInfoLookup.HasComponent(cubeEntity))
                            _materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
                    }
                }
            }
            break;
        }
    }

    [BurstCompile]
    private static void SetChildVisualsEnabled(
        in Entity rootEntity,
        bool enabled,
        ref BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        ref ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup)
    {
        if (!linkedGroupLookup.HasBuffer(rootEntity))
        {
            if (materialMeshInfoLookup.HasComponent(rootEntity))
                materialMeshInfoLookup.SetComponentEnabled(rootEntity, enabled);
            return;
        }

        var linkedGroup = linkedGroupLookup[rootEntity];
        for (int i = 0; i < linkedGroup.Length; i++)
        {
            var childEntity = linkedGroup[i].Value;
            if (materialMeshInfoLookup.HasComponent(childEntity))
                materialMeshInfoLookup.SetComponentEnabled(childEntity, enabled);
        }
    }
}

[BurstCompile]
public partial struct LodInitJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup;

    void Execute(in RenderLodLink lodLink, EnabledRefRW<RenderLodNeedsInit> needsInit)
    {
        var cubeEntity = lodLink.CubeVisual;
        if (materialMeshInfoLookup.HasComponent(cubeEntity))
            materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
        needsInit.ValueRW = false;
    }
}

[BurstCompile]
[WithDisabled(typeof(RenderLodNeedsInit))]
public partial struct LodUpdateJob : IJobEntity
{
    public float3 cameraPosition;
    public float nearToFarThresholdSq;
    public float farToNearThresholdSq;

    [NativeDisableParallelForRestriction]
    public ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup;
    [ReadOnly] public BufferLookup<LinkedEntityGroup> linkedGroupLookup;

    void Execute(Entity entity, ref RenderLodLink lodLink, in LocalTransform transform)
    {
        var distanceSq = math.distancesq(transform.Position, cameraPosition);
        var currentlyFar = lodLink.IsFar != 0;
        var shouldBeFar = currentlyFar ? distanceSq >= farToNearThresholdSq : distanceSq >= nearToFarThresholdSq;

        if (currentlyFar == shouldBeFar) return;

        lodLink.IsFar = shouldBeFar ? (byte)1 : (byte)0;
        var cubeEntity = lodLink.CubeVisual;

        if (shouldBeFar)
        {
            SetChildVisualsEnabled(entity, false);
            if (materialMeshInfoLookup.HasComponent(cubeEntity))
                materialMeshInfoLookup.SetComponentEnabled(cubeEntity, true);
        }
        else
        {
            SetChildVisualsEnabled(entity, true);
            if (materialMeshInfoLookup.HasComponent(cubeEntity))
                materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
        }
    }

    private void SetChildVisualsEnabled(Entity rootEntity, bool enabled)
    {
        if (!linkedGroupLookup.HasBuffer(rootEntity))
        {
            if (materialMeshInfoLookup.HasComponent(rootEntity))
                materialMeshInfoLookup.SetComponentEnabled(rootEntity, enabled);
            return;
        }

        var linkedGroup = linkedGroupLookup[rootEntity];
        for (int i = 0; i < linkedGroup.Length; i++)
        {
            var childEntity = linkedGroup[i].Value;
            if (materialMeshInfoLookup.HasComponent(childEntity))
                materialMeshInfoLookup.SetComponentEnabled(childEntity, enabled);
        }
    }
}
