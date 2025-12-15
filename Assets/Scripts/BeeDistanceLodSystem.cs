using Unity.Burst;
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

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraPosition>();
        state.RequireForUpdate<RenderLodLink>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var cameraPosition = SystemAPI.GetSingleton<CameraPosition>().Value;

        foreach (var (lodLink, needsInit, entity) in
            SystemAPI.Query<RefRO<RenderLodLink>, EnabledRefRW<RenderLodNeedsInit>>()
                .WithEntityAccess())
        {
            var cubeEntity = lodLink.ValueRO.CubeVisual;
            if (SystemAPI.HasComponent<MaterialMeshInfo>(cubeEntity))
                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(cubeEntity, false);
            needsInit.ValueRW = false;
        }

        var linkedGroupLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
        var materialMeshInfoLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);

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
                SetChildVisualsEnabled(beeRootEntity, false, ref linkedGroupLookup, ref materialMeshInfoLookup);
                if (materialMeshInfoLookup.HasComponent(cubeEntity))
                    materialMeshInfoLookup.SetComponentEnabled(cubeEntity, true);
            }
            else
            {
                SetChildVisualsEnabled(beeRootEntity, true, ref linkedGroupLookup, ref materialMeshInfoLookup);
                if (materialMeshInfoLookup.HasComponent(cubeEntity))
                    materialMeshInfoLookup.SetComponentEnabled(cubeEntity, false);
            }
        }
    }

    [BurstCompile]
    private static void SetChildVisualsEnabled(
        in Entity rootEntity,
        bool enabled,
        ref BufferLookup<LinkedEntityGroup> linkedGroupLookup,
        ref ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup)
    {
        if (!linkedGroupLookup.HasBuffer(rootEntity)) return;

        var linkedGroup = linkedGroupLookup[rootEntity];
        for (int i = 0; i < linkedGroup.Length; i++)
        {
            var childEntity = linkedGroup[i].Value;
            if (childEntity == rootEntity) continue;
            if (materialMeshInfoLookup.HasComponent(childEntity))
                materialMeshInfoLookup.SetComponentEnabled(childEntity, enabled);
        }
    }
}
