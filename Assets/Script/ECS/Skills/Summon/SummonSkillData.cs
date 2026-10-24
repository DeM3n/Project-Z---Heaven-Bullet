using Unity.Entities;

public struct SummonSkillTag : IComponentData {}

public struct SummonSkillData : IComponentData
{
    public Entity SummonPrefab;
    public float SummonLifetime; // con summon tự hủy sau chừng này giây (dùng chung ProjectileLifetimeData)
}