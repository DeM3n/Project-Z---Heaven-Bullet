using Unity.Entities;

public struct EnemyDeathDropData : IComponentData
{
    public Entity ExpOrbPrefab;
    public int ExpReward;
    public float DropChance; // Tỉ lệ rớt ngọc (0.0f = 0% đến 1.0f = 100%)
}