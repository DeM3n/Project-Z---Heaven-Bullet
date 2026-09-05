using Unity.Entities;
using UnityEngine;

public class EnemyAuthoring : MonoBehaviour
{
    public float MoveSpeed = 2f;
    public float MaxHP = 30f;
    public GameObject ExpOrbPrefab;
    public int ExpReward = 5;

    class Baker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
        if (authoring.ExpOrbPrefab == null)
            {
                UnityEngine.Debug.LogError($"[EnemyAuthoring] Chưa gán ExpOrbPrefab trên {authoring.name}", authoring);
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
        }
    }
}