using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(BeeFlyingSystem))] 
partial struct BeeAtHiveSystem : ISystem
{
    private ComponentLookup<HiveData> _hiveLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _hiveLookup = state.GetComponentLookup<HiveData>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        _hiveLookup.Update(ref state);
        
        var atHiveJob = new BeeAtHiveJob
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            hiveLookup = _hiveLookup,
            ecb = ecb
        }.Schedule(state.Dependency);

        atHiveJob.Complete();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct BeeAtHiveJob : IJobEntity
{
    public float deltaTime;
    public ComponentLookup<HiveData> hiveLookup;
    public EntityCommandBuffer.ParallelWriter ecb;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, in AtHive atHive, ref BeeData bee)
    {
        
        if (bee.homeHive == null) return; // TODO: handle no hive case
        var hive = (Entity) bee.homeHive;
        if (!hiveLookup.HasComponent(hive)) return;
        var hiveData = hiveLookup[hive];
        var rot = math.mul(quaternion.RotateZ(2f * deltaTime), quaternion.RotateY(2f * deltaTime));
        
        var maxNectarToGive = 2f * deltaTime;
        var nectarGiven = math.min(bee.nectarCarried, maxNectarToGive);
        bee.nectarCarried -= nectarGiven;
        hiveData.nectarAmount += nectarGiven;
        hiveLookup[hive] = hiveData;
        
        var beeIsDepleted = bee.nectarCarried <= 0.01;
        if (!beeIsDepleted) return;
        ecb.RemoveComponent<AtHive>(chunkKey, entity);
        ecb.AddComponent(chunkKey, entity, new TravellingToFlower());
        
    }
}