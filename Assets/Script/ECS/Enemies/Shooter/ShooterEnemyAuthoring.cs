using Unity.Entities;
using UnityEngine;

public class ShooterEnemyAuthoring : MonoBehaviour, IEnemyBehaviorAuthoring
{
    public GameObject ProjectilePrefab;
    public float AttackCooldown = 1.5f;
    public float PreferredRange = 5f;
    public float ProjectileDamage = 10f;
    public float ProjectileSpeed = 6f;
    public float ProjectileHitRadius = 0.2f;

    public void Bake(IBaker baker, Entity entity)
    {
        if (ProjectilePrefab == null)
        {
            Debug.LogError($"[ShooterEnemyAuthoring] Chưa gán ProjectilePrefab trên {name}", this);
            return;
        }

        baker.AddComponent<ShooterTag>(entity);
        baker.AddComponent(entity, new ShooterAttackData
        {
            Cooldown = AttackCooldown,
            Timer = 0f,
            PreferredRange = PreferredRange,
            Damage = ProjectileDamage,
            ProjectileSpeed = ProjectileSpeed,
            ProjectileHitRadius = ProjectileHitRadius,
            ProjectilePrefab = baker.GetEntity(ProjectilePrefab, TransformUsageFlags.Dynamic)
        });
    }
}