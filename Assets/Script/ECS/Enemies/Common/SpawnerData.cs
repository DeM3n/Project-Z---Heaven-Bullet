using Unity.Entities;
using Unity.Mathematics;

public struct SpawnerData : IComponentData
{
    public float Interval;
    public float Timer;
    public float SpawnRadius;
    public int CountPerSpawn;
    public Random Rng;
    public float ElapsedGameTime; // Thời gian sinh tồn trận đấu đã trôi qua (giây)
}

[InternalBufferCapacity(8)]
public struct EnemySpawnEntry : IBufferElementData
{
    public Entity Prefab;
    public int Weight; // Trọng số xuất hiện theo tỉ lệ (ví dụ: Chaser 50, Shooter 25, Tank 15, Bomber 10)
}