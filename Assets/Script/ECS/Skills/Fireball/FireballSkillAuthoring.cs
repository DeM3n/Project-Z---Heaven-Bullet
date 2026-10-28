using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(SkillAuthoring))]
public class FireballSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Prefabs (Optional - Để trống nếu dùng Procedural Visual Bridge)")]
    [Tooltip("Prefab quả cầu lửa mẹ")]
    public GameObject FireballPrefab;
    [Tooltip("Prefab quả cầu lửa con")]
    public GameObject SubFireballPrefab;

    [Header("Chỉ số Cầu Lửa Mẹ (Level 1)")]
    public float BaseDamage = 45f;
    public float FireballSpeed = 9.5f;
    public float ExplosionRadius = 1.8f;
    public float DetectRange = 9.5f;

    [Header("Chỉ số Cầu Lửa Con Nổ Chùm (Cluster Sub-Fireballs)")]
    public float SubFireballSpeed = 11.0f;
    public float SubFireballLifetime = 0.45f;
    public float SubExplosionRadius = 1.2f;

    [Header("Chỉ số Vùng Lửa Thiêu Đốt Cấp 4 (Scorched Fire Zone DOT)")]
    public float FireZoneRadius = 1.8f;
    public float FireZoneDuration = 3.5f;
    public float FireZoneTickInterval = 0.25f;
    public float FireZoneDamagePerTick = 16f;

    public void Bake(IBaker baker, Entity entity)
    {
        Entity fbPrefabEntity = Entity.Null;
        if (FireballPrefab != null)
        {
            fbPrefabEntity = baker.GetEntity(FireballPrefab, TransformUsageFlags.Dynamic);
        }

        Entity subFbPrefabEntity = Entity.Null;
        if (SubFireballPrefab != null)
        {
            subFbPrefabEntity = baker.GetEntity(SubFireballPrefab, TransformUsageFlags.Dynamic);
        }

        baker.AddComponent<FireballSkillTag>(entity);
        baker.AddComponent(entity, new FireballSkillData
        {
            FireballPrefab = fbPrefabEntity,
            SubFireballPrefab = subFbPrefabEntity,
            BaseDamage = BaseDamage,
            FireballSpeed = FireballSpeed,
            ExplosionRadius = ExplosionRadius,
            SubFireballSpeed = SubFireballSpeed,
            SubFireballLifetime = SubFireballLifetime,
            SubExplosionRadius = SubExplosionRadius,
            FireZoneRadius = FireZoneRadius,
            FireZoneDuration = FireZoneDuration,
            FireZoneTickInterval = FireZoneTickInterval,
            FireZoneDamagePerTick = FireZoneDamagePerTick,
            DetectRange = DetectRange
        });
    }
}
