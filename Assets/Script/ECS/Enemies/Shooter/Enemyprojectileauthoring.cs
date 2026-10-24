using Unity.Entities;
using UnityEngine;

public class EnemyProjectileAuthoring : MonoBehaviour
{
    public float Lifetime = 3f;
    class Baker : Baker<EnemyProjectileAuthoring>
    {
        
        public override void Bake(EnemyProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyProjectileTag>(entity);
            AddComponent(entity, new MovementData());       // giá trị thật được set lúc spawn trong ShooterAttackSystem
            AddComponent(entity, new DamageOnContactData()); // giá trị thật được set lúc spawn trong ShooterAttackSystem
            AddComponent(entity, new ProjectileLifetimeData { TimeRemaining = authoring.Lifetime });
        }
    }
}   