using Unity.Entities;
using Unity.Mathematics;

public struct SpawnerData : IComponentData
{
    public Entity EnemyPrefab;
    public float Interval;
    public float Timer;
    public float SpawnRadius;
    public Random Rng;
}