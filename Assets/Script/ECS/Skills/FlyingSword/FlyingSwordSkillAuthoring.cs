using Unity.Entities;
using UnityEngine;

public class FlyingSwordSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Cấu hình Cân Bằng (ScriptableObject - Single Source of Truth)")]
    [Tooltip("Kéo thả file FlyingSwordSkillConfig.asset vào đây (hoặc để trống để tự động nạp từ Resources/Skills/FlyingSwordSkillConfig)")]
    public FlyingSwordSkillConfigSO Config;

    [Header("Sword Prefab")]
    public GameObject SwordPrefab;

    [Header("Chỉ số Mặc định Fallback (Dùng khi không tìm thấy ScriptableObject)")]
    public float BaseDamage = 13.5f;
    public float RushSpeed = 9.45f;
    public float ReturnSpeed = 11.0f;
    public float OrbitRadius = 2.2f;
    public float OrbitSpeed = 3.5f;
    public float AttackInterval = 0.5f;
    public float HitRadius = 0.5f;

    [Header("Phạm vi hoạt động (Tầm nhìn Camera)")]
    public float DetectRange = 6.5f;
    public float MaxLeashRange = 7.5f;

    [Header("Chỉ số Ultimate Fallback (Level 4 AOE)")]
    public float UltimateCooldown = 6.0f;
    public float AoeRadius = 3.5f;
    public float AoeDamage = 85f;

    public void Bake(IBaker baker, Entity entity)
    {
        if (SwordPrefab == null)
        {
            Debug.LogError($"[FlyingSwordSkillAuthoring] Chưa gán SwordPrefab trên {name}", this);
            return;
        }

        var cfg = Config != null ? Config : FlyingSwordSkillConfigSO.Instance;

        baker.AddComponent<FlyingSwordSkillTag>(entity);
        baker.AddComponent(entity, new FlyingSwordSkillData
        {
            SwordPrefab = baker.GetEntity(SwordPrefab, TransformUsageFlags.Dynamic),
            BaseDamage = cfg != null ? cfg.GetDamage(1) : BaseDamage,
            RushSpeed = cfg != null ? cfg.GetRushSpeed(1) : RushSpeed,
            ReturnSpeed = cfg != null ? cfg.ReturnSpeed : ReturnSpeed,
            OrbitRadius = cfg != null ? cfg.OrbitRadius : OrbitRadius,
            OrbitSpeed = cfg != null ? cfg.OrbitSpeed : OrbitSpeed,
            AttackInterval = cfg != null ? cfg.AttackInterval : AttackInterval,
            HitRadius = cfg != null ? cfg.HitRadius : HitRadius,
            AoeRadius = cfg != null ? cfg.AoeRadius : AoeRadius,
            AoeDamage = cfg != null ? cfg.AoeDamage : AoeDamage,
            DetectRange = cfg != null ? cfg.DetectRange : DetectRange,
            MaxLeashRange = cfg != null ? cfg.MaxLeashRange : MaxLeashRange,
            UltimateCooldown = cfg != null ? cfg.UltimateCooldown : UltimateCooldown
        });

        baker.AddComponent(entity, new FlyingSwordLevelBalanceData
        {
            DamageLv1 = cfg != null ? cfg.GetDamage(1) : 13.5f,
            DamageLv2 = cfg != null ? cfg.GetDamage(2) : 18.2f,
            DamageLv3 = cfg != null ? cfg.GetDamage(3) : 23.0f,
            DamageLv4 = cfg != null ? cfg.GetDamage(4) : 27.7f,

            RushSpeedLv1 = cfg != null ? cfg.GetRushSpeed(1) : 9.45f,
            RushSpeedLv2 = cfg != null ? cfg.GetRushSpeed(2) : 11.34f,
            RushSpeedLv3 = cfg != null ? cfg.GetRushSpeed(3) : 13.23f,
            RushSpeedLv4 = cfg != null ? cfg.GetRushSpeed(4) : 15.12f,

            SwordCountLv1 = cfg != null ? cfg.GetSwordCount(1) : 1,
            SwordCountLv2 = cfg != null ? cfg.GetSwordCount(2) : 2,
            SwordCountLv3 = cfg != null ? cfg.GetSwordCount(3) : 3,
            SwordCountLv4 = cfg != null ? cfg.GetSwordCount(4) : 4
        });
    }
}
