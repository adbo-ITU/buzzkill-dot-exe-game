using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(BeeFlyingSystem))] 
partial struct BeeAtHiveSystem : ISystem
{
    private ComponentLookup<HiveData> _hiveLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowerManager>();
        _hiveLookup = state.GetComponentLookup<HiveData>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        
        _hiveLookup.Update(ref state);
        
        var flowerManager = SystemAPI.GetSingleton<FlowerManager>();
        
        var atHiveJob = new BeeAtHiveJob
        {
            time = SystemAPI.Time.ElapsedTime,
            deltaTime = SystemAPI.Time.DeltaTime,
            hiveLookup = _hiveLookup,
            ecb = ecb,
            flowerManager = flowerManager
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
    public double time;
    public float deltaTime;
    public ComponentLookup<HiveData> hiveLookup;
    public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public FlowerManager flowerManager;

    void Execute([ChunkIndexInQuery] int chunkKey, Entity entity, ref LocalTransform trans, in AtHive atHive, ref BeeData bee)
    {
        var hive = bee.homeHive;
        if (!hiveLookup.HasComponent(hive)) return;
        var hiveData = hiveLookup[hive];
        
        var maxNectarToGive = 2f * deltaTime;
        var nectarGiven = math.max(bee.nectarCarried, maxNectarToGive);
        bee.nectarCarried -= nectarGiven;
        hiveData.nectarAmount += nectarGiven;
        hiveLookup[hive] = hiveData;

        var beeIsDepleted = bee.nectarCarried <= 0.01;
        if (!beeIsDepleted) return;
        
        ecb.RemoveComponent<AtHive>(chunkKey, entity);

        var rng = BeeData.GetRng(time, entity);
        var (flowerEntity, flowerData) = flowerManager.GetRandomFlower(ref rng);
        bee.targetFlower = flowerEntity;
        ecb.AddComponent(chunkKey, entity, new TravellingToFlower());
        ecb.AddComponent(chunkKey, entity, new FlightPath()
        {
            time = 0,
            from = trans.Position,
            to = flowerData.position,
            position = trans.Position
        });
    }
}