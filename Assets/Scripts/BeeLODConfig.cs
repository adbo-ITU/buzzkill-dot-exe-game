using Unity.Entities;

public struct BeeLODConfig : IComponentData
{
    public float lodDistanceSq;
    public Entity lowPolyPrefab;
    public int lowPolyMesh;
    public int lowPolyMaterial;
    public int highPolyMesh;
    public int highPolyMaterial;
    public bool foundHighPoly;
    public bool spawnedTempEntity;
    public bool isInitialized;
}
