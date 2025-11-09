using Unity.Burst;
using Unity.Entities;
using System.IO;
using UnityEngine;

partial struct ConfigLoaderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var filename = "simulation_config.json";
        var path = Path.Combine(Application.streamingAssetsPath, filename);
        
        if (!File.Exists(path))
        {
            Debug.LogError($"Config file not found at: {path}");
            throw new FileNotFoundException($"Config file not found at: {path}");
        }

        var jsonText = File.ReadAllText(path);
        var config = JsonUtility.FromJson<SimulationConfig>(jsonText);
        
        Debug.Log($"Config file loaded at: {path}: {jsonText}");
        
        state.EntityManager.CreateSingleton(new SimulationConfigComponent { Config = config });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

public struct SimulationConfigComponent : IComponentData
{
    public SimulationConfig Config;
}

[System.Serializable]
public struct SimulationConfig
{
    public int numBees;
    public int numHives;
    public int numFlowers;
}