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

            var behaviors = authoring.GetComponents<IEnemyBehaviorAuthoring>();
            if (behaviors.Length > 1)
            {
                Debug.LogWarning($"[EnemyAuthoring] {authoring.name} đang gắn {behaviors.Length} behavior cùng lúc — hiện chưa hỗ trợ kết hợp nhiều behavior trên 1 enemy.", authoring);
            }
           foreach (var behavior in behaviors)
           {
                DependsOn(behavior as Object); // đảm bảo incremental baking re-chạy khi số liệu trong behavior này đổi
                behavior.Bake(this, entity);
            }
        }
    }
}