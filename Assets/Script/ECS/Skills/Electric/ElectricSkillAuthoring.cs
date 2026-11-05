using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(SkillAuthoring))]
public class ElectricSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Cấu hình Cân Bằng (ScriptableObject - Single Source of Truth)")]
    [Tooltip("Kéo thả file ElectricSkillConfig.asset vào đây (hoặc để trống để tự nạp từ Resources/Skills/ElectricSkillConfig)")]
    public ElectricSkillConfigSO Config;

    [Header("Chỉ số Mặc định Fallback (Dùng khi không tìm thấy ScriptableObject)")]
    public float BaseDamage = 60f;
    public float DetectRange = 9.5f;
    public float ChainRange = 6.0f;
    public float ChainDamageRatio = 0.30f;
    public float StunDuration = 0.5f;

    public void Bake(IBaker baker, Entity entity)
    {
        var cfg = Config != null ? Config : ElectricSkillConfigSO.Instance;

        baker.AddComponent<ElectricSkillTag>(entity);
        baker.AddComponent(entity, new ElectricSkillData
        {
            BaseDamage = cfg != null ? cfg.GetDamage(1) : BaseDamage,
            DetectRange = cfg != null ? cfg.DetectRange : DetectRange,
            ChainRange = cfg != null ? cfg.ChainRange : ChainRange,
            ChainDamageRatio = cfg != null ? cfg.ChainDamageRatio : ChainDamageRatio,
            PrimaryBoltCount = cfg != null ? cfg.GetPrimaryCount(1) : 1,
            ChainCount = cfg != null ? cfg.GetChainCount(1) : 1,
            CanBounceBack = false,
            StunDuration = 0f
        });

        // Nạp bảng cân bằng 4 cấp độ
        baker.AddComponent(entity, new ElectricLevelBalanceData
        {
            CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 5.4f,
            CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 4.86f,
            CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 4.374f,
            CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 4.374f,

            DamageLv1 = cfg != null ? cfg.GetDamage(1) : 60f,
            DamageLv2 = cfg != null ? cfg.GetDamage(2) : 90f,
            DamageLv3 = cfg != null ? cfg.GetDamage(3) : 90f,
            DamageLv4 = cfg != null ? cfg.GetDamage(4) : 120f,

            PrimaryCountLv1 = cfg != null ? cfg.GetPrimaryCount(1) : 1,
            PrimaryCountLv2 = cfg != null ? cfg.GetPrimaryCount(2) : 1,
            PrimaryCountLv3 = cfg != null ? cfg.GetPrimaryCount(3) : 2,
            PrimaryCountLv4 = cfg != null ? cfg.GetPrimaryCount(4) : 2,

            ChainCountLv1 = cfg != null ? cfg.GetChainCount(1) : 1,
            ChainCountLv2 = cfg != null ? cfg.GetChainCount(2) : 2,
            ChainCountLv3 = cfg != null ? cfg.GetChainCount(3) : 2,
            ChainCountLv4 = cfg != null ? cfg.GetChainCount(4) : 4
        });
    }
}

