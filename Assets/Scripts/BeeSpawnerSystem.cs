using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Material = Unity.Physics.Material;

[UpdateAfter(typeof(FlowerSpawnerSystem))] 
[UpdateAfter(typeof(HiveSpawnerSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))] 
public partial struct BeeSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeeSpawner>();
        state.RequireForUpdate<HiveData>();
        state.RequireForUpdate<HiveManager>();
        state.RequireForUpdate<SimulationConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;
        
        var hiveManager = SystemAPI.GetSingleton<HiveManager>();
        var config = SystemAPI.GetSingleton<SimulationConfig>().config;
        
        EntityCommandBuffer.ParallelWriter ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var handle = new BeeSpawnJob
        {
            time = SystemAPI.Time.ElapsedTime,
            ecb = ecb,
            hiveManager = hiveManager,
            config = config,
        }.Schedule(state.Dependency);

        handle.Complete();
    }
}

[BurstCompile]
public partial struct BeeSpawnJob : IJobEntity
{
    public double time;
    public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public HiveManager  hiveManager;
    [ReadOnly] public SimulationConfigValues config;

    public void Execute([ChunkIndexInQuery] int chunkKey, ref BeeSpawner spawner, Entity entity)
    {
        var rng = BeeData.GetRng(time, entity);
        for (int i = 0; i < config.numBees; i++)
        {
            var (hiveEntity, hiveData) = hiveManager.GetRandomHive(ref rng);
            var spawnPos = hiveData.position;

            var e = ecb.Instantiate(chunkKey, spawner.beePrefab);
            ecb.AddComponent(chunkKey, e, new BeeData
            {
                speed = rng.NextFloat(4f, 7f),
                nectarCapacity = 40,
                nectarCarried = 0,
                homeHive = hiveEntity,
                targetFlower = Entity.Null,
            });
            ecb.AddComponent(chunkKey, e, new AtHive());
            ecb.AddComponent(chunkKey, e, LocalTransform.FromPosition(spawnPos));

            var collider = Unity.Physics.BoxCollider.Create(
                new BoxGeometry
                {
                    Center = float3.zero,
                    Size = 1f,
                    Orientation = quaternion.identity,
                },
                new CollisionFilter
                {
                    BelongsTo = ~0u,      // everything, adjust if you want layers
                    CollidesWith = ~0u,   // collide with everything
                    GroupIndex = 0
                },
                new Material
                {
                    Friction      = 0.1f,
                    Restitution   = 1f, // bounciness
                    FrictionCombinePolicy    = Material.CombinePolicy.GeometricMean,
                    RestitutionCombinePolicy = Material.CombinePolicy.Maximum
                }
            );

            ecb.AddComponent(chunkKey, e, new PhysicsCollider { Value = collider });
            
            var mass = PhysicsMass.CreateDynamic(
                new MassProperties
                {
                    MassDistribution = new MassDistribution
                    {
                        Transform = RigidTransform.identity,
                        InertiaTensor = new float3(1f)
                    },
                    Volume = 1f,
                    AngularExpansionFactor = 0f
                },
                5
            );
            ecb.AddComponent(chunkKey, e, mass);

            ecb.AddComponent(chunkKey, e, new PhysicsVelocity
            {
                Linear  = float3.zero,
                Angular = float3.zero
            });
            
            ecb.AddComponent(chunkKey, e, new PhysicsDamping
            {
                Linear  = 0.0f,
                Angular = 0.0f
            });
        }
    }
}
