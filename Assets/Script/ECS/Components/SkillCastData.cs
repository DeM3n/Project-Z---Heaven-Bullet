using Unity.Entities;

public struct SkillCastData : IComponentData
{
    public Entity ProjectilePrefab;
    public float Cooldown;
    public float Timer;
    public float ProjectileSpeed;
    public float ProjectileLifetime;
    public float ProjectileDamage;
   public float ProjectileHitRadius;
}