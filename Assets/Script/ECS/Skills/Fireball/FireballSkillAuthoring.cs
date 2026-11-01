using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(SkillAuthoring))]
public class FireballSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Cấu hình Cân Bằng (ScriptableObject - Single Source of Truth)")]
    [Tooltip("Kéo thả file FireballSkillConfig.asset vào đây (hoặc để trống để tự động nạp từ Resources/Skills/FireballSkillConfig)")]
    public FireballSkillConfigSO Config;

    [Header("Prefabs (Optional - Để trống nếu dùng Procedural Visual Bridge)")]
    [Tooltip("Prefab quả cầu lửa mẹ")]
    public GameObject FireballPrefab;
    [Tooltip("Prefab quả cầu lửa con")]
    public GameObject SubFireballPrefab;

    [Header("Chỉ số Mặc định Fallback (Dùng khi không tìm thấy ScriptableObject)")]
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
        var cfg = Config != null ? Config : FireballSkillConfigSO.Instance;

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
            BaseDamage = cfg != null ? cfg.GetDamage(1) : BaseDamage,
            FireballSpeed = cfg != null ? cfg.FireballSpeed : FireballSpeed,
            ExplosionRadius = cfg != null ? cfg.GetExplosionRadius(1) : ExplosionRadius,
            SubFireballSpeed = cfg != null ? cfg.SubFireballSpeed : SubFireballSpeed,
            SubFireballLifetime = cfg != null ? cfg.SubFireballLifetime : SubFireballLifetime,
            SubExplosionRadius = cfg != null ? cfg.SubExplosionRadius : SubExplosionRadius,
            FireZoneRadius = cfg != null ? cfg.FireZoneRadius : FireZoneRadius,
            FireZoneDuration = cfg != null ? cfg.FireZoneDuration : FireZoneDuration,
            FireZoneTickInterval = cfg != null ? cfg.FireZoneTickInterval : FireZoneTickInterval,
            FireZoneDamagePerTick = cfg != null ? cfg.FireZoneDamagePerTick : FireZoneDamagePerTick,
            DetectRange = cfg != null ? cfg.DetectRange : DetectRange
        });

        // Bake toàn bộ bảng thông số Cooldown & Sát thương 4 cấp độ từ ScriptableObject vào ECS Component
        baker.AddComponent(entity, new FireballLevelBalanceData
        {
            CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 2.86f,
            CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 2.53f,
            CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 2.20f,
            CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 1.98f,

            DamageLv1 = cfg != null ? cfg.GetDamage(1) : 45f,
            DamageLv2 = cfg != null ? cfg.GetDamage(2) : 68f,
            DamageLv3 = cfg != null ? cfg.GetDamage(3) : 95f,
            DamageLv4 = cfg != null ? cfg.GetDamage(4) : 120f,

            SubCountLv1 = cfg != null ? cfg.GetSubFireballCount(1) : 1,
            SubCountLv2 = cfg != null ? cfg.GetSubFireballCount(2) : 2,
            SubCountLv3 = cfg != null ? cfg.GetSubFireballCount(3) : 3,
            SubCountLv4 = cfg != null ? cfg.GetSubFireballCount(4) : 4,

            RadiusLv1 = cfg != null ? cfg.GetExplosionRadius(1) : 1.8f,
            RadiusLv2 = cfg != null ? cfg.GetExplosionRadius(2) : 2.1f,
            RadiusLv3 = cfg != null ? cfg.GetExplosionRadius(3) : 2.4f,
            RadiusLv4 = cfg != null ? cfg.GetExplosionRadius(4) : 2.8f
        });
    }
}
