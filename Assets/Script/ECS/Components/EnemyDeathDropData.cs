using Unity.Entities;

public struct EnemyDeathDropData : IComponentData
{
    public Entity ExpOrbPrefab;
    public int ExpReward;
}