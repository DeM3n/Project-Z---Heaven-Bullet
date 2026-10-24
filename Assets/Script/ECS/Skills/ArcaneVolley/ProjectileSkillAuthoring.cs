using Unity.Entities;
using UnityEngine;

public class ProjectileSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 8f;
    public float ProjectileLifetime = 2f;
    public float ProjectileDamage = 10f;
    public float ProjectileHitRadius = 0.3f;
    public int ProjectileCount = 1;
    public float SpreadAngleDegrees = 0f;

    public void Bake(IBaker baker, Entity entity)
    {
        if (ProjectilePrefab == null)
        {
            Debug.LogError($"[ProjectileSkillAuthoring] Chưa gán ProjectilePrefab trên {name}", this);
            return;
        }

        baker.AddComponent<ProjectileSkillTag>(entity);
        baker.AddComponent(entity, new ProjectileSkillData
        {
            ProjectilePrefab = baker.GetEntity(ProjectilePrefab, TransformUsageFlags.Dynamic),
            ProjectileSpeed = ProjectileSpeed,
            ProjectileLifetime = ProjectileLifetime,
            ProjectileDamage = ProjectileDamage,
            ProjectileHitRadius = ProjectileHitRadius,
            ProjectileCount = ProjectileCount,
            SpreadAngleDegrees = SpreadAngleDegrees
        });
    }
}