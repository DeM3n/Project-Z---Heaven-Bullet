using UnityEngine;

/// <summary>
/// Cấu hình Cân Bằng (Balance) cho Kỹ năng Lôi Điện (Electric / Chain Lightning).
/// Chuẩn ScriptableObject Single Source of Truth (SSOT):
/// - Designer tinh chỉnh thông số trực tiếp qua Inspector tại: Assets/Resources/Skills/ElectricSkillConfig.asset
/// - Baker nạp vào ECS Component (ElectricLevelBalanceData) cho Burst System xử lý.
/// - UI và Codex đồng bộ thông qua Singleton ElectricSkillConfigSO.Instance.
/// </summary>
[CreateAssetMenu(fileName = "ElectricSkillConfig", menuName = "Heaven Bullet/Skills/Electric Config")]
public class ElectricSkillConfigSO : ScriptableObject
{
    private static ElectricSkillConfigSO _instance;
    public static ElectricSkillConfigSO Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ElectricSkillConfigSO>("Skills/ElectricSkillConfig");
                if (_instance == null)
                {
                    _instance = ScriptableObject.CreateInstance<ElectricSkillConfigSO>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Thời Gian Hồi Chiêu Từng Cấp (Cooldowns in Seconds)")]
    [Tooltip("Lv1: 6.0s, Lv2: 5.4s (-10%), Lv3: 4.86s (-10%), Lv4: 4.86s")]
    public float[] Cooldowns = new float[] { 5.4f, 4.86f, 4.374f, 4.374f };

    [Header("2. Sát Thương Cơ Bản Tia Cha Từng Cấp (Base Damage)")]
    [Tooltip("Lv1: 60, Lv2: 90 (+50%), Lv3: 90, Lv4: 120 (+33%)")]
    public float[] BaseDamages = new float[] { 60f, 90f, 90f, 120f };

    [Header("3. Tỉ Lệ Sát Thương Tia Lan (Chain Damage Ratio)")]
    [Tooltip("Tỉ lệ sát thương tia lan so với tia sét gốc (Mặc định 0.30 = 30%)")]
    public float ChainDamageRatio = 0.30f;

    [Header("4. Số Lần Lan Sang Mục Tiêu Kế Tiếp (Chain Jumps Count)")]
    [Tooltip("Lv1: 1 lần, Lv2: 2 lần (+1), Lv3: 2 lần, Lv4: 4 lần (+2)")]
    public int[] ChainCounts = new int[] { 1, 2, 2, 4 };

    [Header("5. Số Lượng Tia Sét Cha Bắn Cùng Lúc (Primary Bolt Counts)")]
    [Tooltip("Lv1: 1 tia, Lv2: 1 tia, Lv3: 2 tia (+1 Song Lôi Đa Mục Tiêu), Lv4: 2 tia")]
    public int[] PrimaryBoltCounts = new int[] { 1, 1, 2, 2 };

    [Header("6. Cự Ly Tìm Mục Tiêu & Lan Truyền (Detection & Chain Range)")]
    [Tooltip("Tầm quét quái tự động của tia sét cha quanh Player (~9.5m)")]
    public float DetectRange = 9.5f;

    [Tooltip("Bán kính tối đa để tia sét phóng lan sang quái tiếp theo (~6.0m)")]
    public float ChainRange = 6.0f;

    [Header("7. Hiệu Ứng Khống Chế Cấp 4 (Level 4 Special Effects)")]
    [Tooltip("Thời gian làm choáng (Stun) quái khi dính sét ở Cấp 4 (0.5s)")]
    public float StunDuration = 0.5f;

    public float GetCooldown(int level)
    {
        if (Cooldowns == null || Cooldowns.Length == 0) return 6.0f;
        int idx = Mathf.Clamp(level - 1, 0, Cooldowns.Length - 1);
        return Cooldowns[idx];
    }

    public float GetDamage(int level)
    {
        if (BaseDamages == null || BaseDamages.Length == 0) return 60f;
        int idx = Mathf.Clamp(level - 1, 0, BaseDamages.Length - 1);
        return BaseDamages[idx];
    }

    public int GetChainCount(int level)
    {
        if (ChainCounts == null || ChainCounts.Length == 0) return 1;
        int idx = Mathf.Clamp(level - 1, 0, ChainCounts.Length - 1);
        return ChainCounts[idx];
    }

    public int GetPrimaryCount(int level)
    {
        if (PrimaryBoltCounts == null || PrimaryBoltCounts.Length == 0) return 1;
        int idx = Mathf.Clamp(level - 1, 0, PrimaryBoltCounts.Length - 1);
        return PrimaryBoltCounts[idx];
    }

    private void OnEnable()
    {
        _instance = this;
    }
}

