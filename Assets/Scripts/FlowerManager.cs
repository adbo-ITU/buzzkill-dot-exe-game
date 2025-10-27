using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Random = Unity.Mathematics.Random;


public struct FlowerManager : IComponentData
{
    public NativeHashMap<Entity, FlowerData> flowerMap;
    public Random random;
    
    public (Entity, FlowerData) RandomFlower
    {
        [BurstCompile]
        get
        {
            if (flowerMap.Count == 0)
                throw new Exception("Flower not found");
            
            int randomIndex = random.NextInt(0, flowerMap.Count);
            int currentIndex = 0;
            
            foreach (var kvp in flowerMap)
            {
                if (currentIndex == randomIndex)
                    return (kvp.Key, kvp.Value);
                currentIndex++;
            }
            throw new Exception("Flower not found");
        }
    }
}