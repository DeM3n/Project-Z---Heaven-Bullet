using Unity.Entities;
 
public struct ProjectileSkillTag : IComponentData {}
 
public struct ProjectileSkillData : IComponentData
{
    public Entity ProjectilePrefab;
    public float ProjectileSpeed;
    public float ProjectileLifetime;
    public float ProjectileDamage;
    public float ProjectileHitRadius;
    public int ProjectileCount;       // 1 = bắn thẳng; >1 = tỏa hình quạt
    public float SpreadAngleDegrees;  // tổng góc tỏa giữa viên đầu và viên cuối
}
 