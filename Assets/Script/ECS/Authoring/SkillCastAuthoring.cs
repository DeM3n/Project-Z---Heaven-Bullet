using Unity.Entities;
using UnityEngine;

// Gắn component này lên chính GameObject Player (cùng với PlayerAuthoring)
public class SkillCastAuthoring : MonoBehaviour
{
    public GameObject ProjectilePrefab;
    public float Cooldown = 1f;
    public float ProjectileSpeed = 8f;
    public float ProjectileLifetime = 2f;
    public float ProjectileDamage = 10f;
    public float ProjectileHitRadius = 0.3f;

    class Baker : Baker<SkillCastAuthoring>
    {
        public override void Bake(SkillCastAuthoring authoring)
        {
           if (authoring.ProjectilePrefab == null)
            {
                UnityEngine.Debug.LogError($"[SkillCastAuthoring] Chưa gán ProjectilePrefab trên {authoring.name}", authoring);
                return;
            }
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SkillCastData
            {
                ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                Cooldown = authoring.Cooldown,
                Timer = 0f,
                ProjectileSpeed = authoring.ProjectileSpeed,
                ProjectileLifetime = authoring.ProjectileLifetime,
                ProjectileDamage = authoring.ProjectileDamage,
                ProjectileHitRadius = authoring.ProjectileHitRadius
            });
        }
    }
}