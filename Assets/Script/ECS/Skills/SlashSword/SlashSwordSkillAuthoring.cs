using Unity.Entities;
using UnityEngine;

public class SlashSwordSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Cấu hình Cân Bằng (ScriptableObject - Single Source of Truth)")]
    [Tooltip("Kéo thả file SlashSwordSkillConfig.asset vào đây (hoặc để trống để tự động nạp từ Resources/Skills/SlashSwordSkillConfig)")]
    public SlashSwordSkillConfigSO Config;

    [Header("Prefabs")]
    [Tooltip("Prefab thanh kiếm chém")]
    public GameObject SwordPrefab;
    [Tooltip("Prefab kiếm khí phóng ra ở Cấp 4")]
    public GameObject SwordWavePrefab;

    [Header("Chỉ số Mặc định Fallback (Dùng khi không tìm thấy ScriptableObject)")]
    public float BaseDamage = 38f;
    public float ApproachSpeed = 13f;
    public float ReturnSpeed = 14f;
    public float SlashRadius = 3.2f;
    public float SlashAngleDeg = 155f;
    public float DetectRange = 8.5f;

    [Header("Chỉ số Kiếm Khí Fallback (Level 4 Piercing Wave)")]
    public float WaveSpeed = 15f;
    public float WaveDuration = 1.6f;
    public float WaveDamage = 65f;
    public float WaveRadius = 1.2f;

    public void Bake(IBaker baker, Entity entity)
    {
        var cfg = Config != null ? Config : SlashSwordSkillConfigSO.Instance;

        Entity swordPrefabEntity = Entity.Null;
        if (SwordPrefab != null)
        {
            swordPrefabEntity = baker.GetEntity(SwordPrefab, TransformUsageFlags.Dynamic);
        }
        else
        {
            Debug.LogError($"[SlashSwordSkillAuthoring] Chưa gán SwordPrefab trên {name}", this);
        }

        Entity wavePrefabEntity = Entity.Null;
        if (SwordWavePrefab != null)
        {
            wavePrefabEntity = baker.GetEntity(SwordWavePrefab, TransformUsageFlags.Dynamic);
        }

        baker.AddComponent<SlashSwordSkillTag>(entity);
        baker.AddComponent(entity, new SlashSwordSkillData
        {
            SwordPrefab = swordPrefabEntity,
            SwordWavePrefab = wavePrefabEntity,
            BaseDamage = cfg != null ? cfg.GetDamage(1) : BaseDamage,
            ApproachSpeed = cfg != null ? cfg.GetApproachSpeed(1) : ApproachSpeed,
            ReturnSpeed = cfg != null ? cfg.ReturnSpeed : ReturnSpeed,
            SlashRadius = cfg != null ? cfg.GetSlashRadius(1) : SlashRadius,
            SlashAngleDeg = cfg != null ? cfg.SlashAngleDeg : SlashAngleDeg,
            DetectRange = cfg != null ? cfg.DetectRange : DetectRange,
            WaveSpeed = cfg != null ? cfg.WaveSpeed : WaveSpeed,
            WaveDuration = cfg != null ? cfg.WaveDuration : WaveDuration,
            WaveDamage = cfg != null ? cfg.WaveDamage : WaveDamage,
            WaveRadius = cfg != null ? cfg.WaveRadius : WaveRadius
        });

        baker.AddComponent(entity, new SlashSwordLevelBalanceData
        {
            CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 3.6f,
            CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 2.97f,
            CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 2.34f,
            CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 1.8f,

            DamageLv1 = cfg != null ? cfg.GetDamage(1) : 38f,
            DamageLv2 = cfg != null ? cfg.GetDamage(2) : 70f,
            DamageLv3 = cfg != null ? cfg.GetDamage(3) : 102f,
            DamageLv4 = cfg != null ? cfg.GetDamage(4) : 134f,

            SlashRadiusLv1 = cfg != null ? cfg.GetSlashRadius(1) : 3.2f,
            SlashRadiusLv2 = cfg != null ? cfg.GetSlashRadius(2) : 3.55f,
            SlashRadiusLv3 = cfg != null ? cfg.GetSlashRadius(3) : 3.9f,
            SlashRadiusLv4 = cfg != null ? cfg.GetSlashRadius(4) : 4.25f,

            SwordCountLv1 = cfg != null ? cfg.GetSwordCount(1) : 2,
            SwordCountLv2 = cfg != null ? cfg.GetSwordCount(2) : 3,
            SwordCountLv3 = cfg != null ? cfg.GetSwordCount(3) : 4,
            SwordCountLv4 = cfg != null ? cfg.GetSwordCount(4) : 5,

            ApproachSpeedLv1 = cfg != null ? cfg.GetApproachSpeed(1) : 13f,
            ApproachSpeedLv2 = cfg != null ? cfg.GetApproachSpeed(2) : 16f,
            ApproachSpeedLv3 = cfg != null ? cfg.GetApproachSpeed(3) : 19f,
            ApproachSpeedLv4 = cfg != null ? cfg.GetApproachSpeed(4) : 22f,

            WaveDamageLv4 = cfg != null ? cfg.WaveDamage : 65f
        });
    }
}
