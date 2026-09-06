using Unity.Entities;
using UnityEngine;

public enum EnemyBehaviorType { Chaser, Tank, Shooter, Bomber }

public class EnemyAuthoring : MonoBehaviour
{
    [Header("Chỉ số cơ bản")]
    public float MoveSpeed = 2f;
    public float MaxHP = 30f;
    public GameObject ExpOrbPrefab;
    public int ExpReward = 5;

    [Header("Hành vi")]
    public EnemyBehaviorType Behavior = EnemyBehaviorType.Chaser;

    [Header("Va chạm với Player (áp dụng cho mọi behavior)")]
    public float ContactDamage = 5f;
    public float ContactRadius = 0.6f;
    public float ContactTickInterval = 1f;

    [Header("Shooter - chỉ dùng khi Behavior = Shooter")]
    public GameObject ProjectilePrefab;
    public float AttackCooldown = 1.5f;
    public float PreferredRange = 5f;
    public float ProjectileDamage = 10f;
    public float ProjectileSpeed = 6f;
    public float ProjectileHitRadius = 0.2f;

    [Header("Bomber - chỉ dùng khi Behavior = Bomber")]
    public float ExplodeRadius = 1.5f;
    public float ExplodeDamage = 25f;

    class Baker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            if (authoring.ExpOrbPrefab == null)
            {
                Debug.LogError($"[EnemyAuthoring] Chưa gán ExpOrbPrefab trên {authoring.name}", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyTag>(entity);
            AddComponent(entity, new HealthData { CurrentHP = authoring.MaxHP, MaxHP = authoring.MaxHP });
            AddComponent(entity, new MovementData { MoveSpeed = authoring.MoveSpeed });
            AddComponent(entity, new EnemyDeathDropData
            {
                ExpOrbPrefab = GetEntity(authoring.ExpOrbPrefab, TransformUsageFlags.Dynamic),
                ExpReward = authoring.ExpReward
            });
            AddComponent(entity, new EnemyContactDamageData
            {
                Damage = authoring.ContactDamage,
                ContactRadius = authoring.ContactRadius,
                TickInterval = authoring.ContactTickInterval,
                Timer = 0f
            });

            switch (authoring.Behavior)
            {
                case EnemyBehaviorType.Shooter:
                    if (authoring.ProjectilePrefab == null)
                    {
                        Debug.LogError($"[EnemyAuthoring] Behavior=Shooter nhưng chưa gán ProjectilePrefab trên {authoring.name}", authoring);
                        break;
                    }
                    AddComponent<ShooterTag>(entity);
                    AddComponent(entity, new ShooterAttackData
                    {
                        Cooldown = authoring.AttackCooldown,
                        Timer = 0f,
                        PreferredRange = authoring.PreferredRange,
                        Damage = authoring.ProjectileDamage,
                        ProjectileSpeed = authoring.ProjectileSpeed,
                        ProjectileHitRadius = authoring.ProjectileHitRadius,
                        ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic)
                    });
                    break;

                case EnemyBehaviorType.Bomber:
                    AddComponent<BomberTag>(entity);
                    AddComponent(entity, new BomberExplodeData
                    {
                        ExplodeRadius = authoring.ExplodeRadius,
                        ExplodeDamage = authoring.ExplodeDamage
                    });
                    break;

                // Chaser & Tank: không thêm component nào — dùng chung EnemyChaseJob,
                // khác biệt chỉ nằm ở MoveSpeed/MaxHP đặt trong Inspector của từng prefab.
            }
        }
    }
}