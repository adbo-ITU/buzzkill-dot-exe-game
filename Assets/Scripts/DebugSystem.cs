using System.Text;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

public partial struct DebugSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HiveData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        Entity debugEntity;
        if (!SystemAPI.TryGetSingletonEntity<DebugData>(out debugEntity))
        {
            debugEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<DebugData>(debugEntity);
        }

        var dbg = state.EntityManager.GetComponentData<DebugData>(debugEntity);

        var now = SystemAPI.Time.ElapsedTime;
        var elapsed = now - dbg.lastOutputTime;

        if (elapsed < 1)
        {
            return;
        }

        var dbgStr = new StringBuilder();
        var spacer = "    ";

        foreach (var (hive, entity) in SystemAPI.Query<RefRO<HiveData>>().WithEntityAccess())
        {
            dbgStr.Append($"Hive {entity.Index}: <color=#fcce03>{hive.ValueRO.nectarAmount:0.#} nectar</color>");
            dbgStr.Append(spacer);
        }

        double totalNectar = 0;
        double totalFlowerPct = 0;
        var numEmpty = 0;
        var numFlowers = 0;
        foreach (var (flower, entity) in SystemAPI.Query<RefRO<FlowerData>>().WithEntityAccess())
        {
            totalNectar += flower.ValueRO.nectarAmount;
            totalFlowerPct += flower.ValueRO.nectarAmount / flower.ValueRO.nectarCapacity;
            if (flower.ValueRO.nectarAmount < 1) numEmpty++;
            numFlowers++;
        }
        
        dbgStr.Append($"Avg. flower nectar: <color=#03fc6f>{totalFlowerPct/numFlowers*100:0.#}%</color>");
        dbgStr.Append($", #empty: <color=#03fc6f>{numEmpty/numFlowers*100:0.#}%</color>");
        dbgStr.Append(spacer);
        
        double totalCarried = 0;
        double totalCarriedPct = 0;
        var numBees = 0;
        foreach (var (bee, entity) in SystemAPI.Query<RefRO<BeeData>>().WithEntityAccess())
        {
            var beeCpy = bee.ValueRO;
            totalCarried += beeCpy.nectarCarried;
            var load = beeCpy.nectarCarried / beeCpy.nectarCapacity;
            totalCarriedPct += load;
            numBees++;
        }

        dbgStr.Append($"Avg. bee nectar: <color=#e3fc03>{totalCarriedPct/numBees*100:0.#}%</color>");

        Debug.Log(dbgStr);
        
        dbg.lastOutputTime = now;
        state.EntityManager.SetComponentData(debugEntity, dbg);
    }
}

struct DebugData : IComponentData
{
    public double lastOutputTime;
}