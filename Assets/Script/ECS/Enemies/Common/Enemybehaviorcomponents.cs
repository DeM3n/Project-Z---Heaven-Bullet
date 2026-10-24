using Unity.Entities;

public struct ShooterTag : IComponentData {}
public struct BomberTag : IComponentData {}
public struct EnemyProjectileTag : IComponentData {}

public struct ShooterAttackData : IComponentData
{
    public float Cooldown;
    public float Timer;
    public float PreferredRange;
    public float Damage;
    public float ProjectileSpeed;
    public float ProjectileHitRadius;
    public Entity ProjectilePrefab;
}

public struct BomberExplodeData : IComponentData
{
    public float ExplodeRadius;
    public float ExplodeDamage;
}

// Áp dụng cho MỌI enemy (Chaser/Tank/Shooter/Bomber) — va chạm trực tiếp với Player
public struct EnemyContactDamageData : IComponentData
{
    public float Damage;
    public float ContactRadius;
    public float TickInterval;
    public float Timer;
}