using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(SkillAuthoring))]
public class BoomerangSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Cấu hình Cân Bằng (ScriptableObject - Single Source of Truth)")]
    [Tooltip("Kéo thả file BoomerangSkillConfig.asset vào đây (hoặc để trống để tự nạp từ Resources/Skills/BoomerangSkillConfig)")]
    public BoomerangSkillConfigSO Config;

    [Header("Prefabs (Optional - Để trống nếu dùng Procedural Visual Bridge)")]
    public GameObject BoomerangPrefab;

    [Header("Chỉ số Mặc định Fallback (Dùng khi không tìm thấy ScriptableObject)")]
    public float BaseDamage = 35f;
    public float FlySpeed = 12.5f;
    public float MaxDistance = 7.5f;
    public float HitRadius = 0.55f;
    public float CatchRadius = 1.4f;
    public float DetectRange = 9.5f;
    public float EmpoweredMultiplier = 1.6f;

    public void Bake(IBaker baker, Entity entity)
    {
        var cfg = Config != null ? Config : BoomerangSkillConfigSO.Instance;

        Entity prefabEntity = Entity.Null;
        if (BoomerangPrefab != null)
        {
            prefabEntity = baker.GetEntity(BoomerangPrefab, TransformUsageFlags.Dynamic);
        }

        baker.AddComponent<BoomerangSkillTag>(entity);
        baker.AddComponent(entity, new BoomerangSkillData
        {
            BoomerangPrefab = prefabEntity,
            BaseDamage = cfg != null ? cfg.GetDamage(1) : BaseDamage,
            FlySpeed = cfg != null ? cfg.FlySpeed : FlySpeed,
            MaxDistance = cfg != null ? cfg.MaxDistance : MaxDistance,
            HitRadius = cfg != null ? cfg.GetHitRadius(1) : HitRadius,
            CatchRadius = cfg != null ? cfg.CatchRadius : CatchRadius,
            DetectRange = cfg != null ? cfg.DetectRange : DetectRange,
            EmpoweredMultiplier = cfg != null ? cfg.EmpoweredMultiplier : EmpoweredMultiplier,
            BoomerangCount = cfg != null ? cfg.GetCount(1) : 1
        });

        // Bake toàn bộ bảng thông số cân bằng 4 cấp độ
        baker.AddComponent(entity, new BoomerangLevelBalanceData
        {
            CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 8.1f,
            CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 7.2f,
            CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 6.3f,
            CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 5.4f,

            DamageLv1 = cfg != null ? cfg.GetDamage(1) : 35f,
            DamageLv2 = cfg != null ? cfg.GetDamage(2) : 55f,
            DamageLv3 = cfg != null ? cfg.GetDamage(3) : 55f,
            DamageLv4 = cfg != null ? cfg.GetDamage(4) : 80f,

            HitRadiusLv1 = cfg != null ? cfg.GetHitRadius(1) : 0.55f,
            HitRadiusLv2 = cfg != null ? cfg.GetHitRadius(2) : 0.85f,
            HitRadiusLv3 = cfg != null ? cfg.GetHitRadius(3) : 0.85f,
            HitRadiusLv4 = cfg != null ? cfg.GetHitRadius(4) : 0.95f,

            CountLv1 = cfg != null ? cfg.GetCount(1) : 1,
            CountLv2 = cfg != null ? cfg.GetCount(2) : 1,
            CountLv3 = cfg != null ? cfg.GetCount(3) : 2,
            CountLv4 = cfg != null ? cfg.GetCount(4) : 2
        });
    }
}

