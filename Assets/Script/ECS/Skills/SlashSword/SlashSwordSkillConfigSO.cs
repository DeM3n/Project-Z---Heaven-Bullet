using UnityEngine;

/// <summary>
/// Cấu hình thông số Cân Bằng (Balance) cho Kỹ năng Trảm Kiếm (Slash Sword / Sentinel Blades).
/// Thiết kế chuẩn ScriptableObject (Single Source of Truth):
/// - Designer tinh chỉnh thông số qua Unity Inspector tại: Assets/Resources/Skills/SlashSwordSkillConfig.asset
/// - Baker nạp vào ECS Component (SlashSwordLevelBalanceData) cho Pure ECS Burst System xử lý.
/// - Hệ thống Giao diện UI (Level-Up cards, Codex compendium) tự động lấy số liệu từ đây để hiển thị.
/// - Hỗ trợ Live Tuning lúc Play Mode qua SkillConfigSyncBridge.
/// </summary>
[CreateAssetMenu(fileName = "SlashSwordSkillConfig", menuName = "Heaven Bullet/Skills/Slash Sword Config")]
public class SlashSwordSkillConfigSO : ScriptableObject
{
    private static SlashSwordSkillConfigSO _instance;
    public static SlashSwordSkillConfigSO Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<SlashSwordSkillConfigSO>("Skills/SlashSwordSkillConfig");
                if (_instance == null)
                {
                    _instance = CreateInstance<SlashSwordSkillConfigSO>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Thời Gian Hồi Chiêu Từng Cấp (Cooldowns in Seconds)")]
    [Tooltip("Lv1: 4.0s, Lv2: 3.3s, Lv3: 2.6s, Lv4: 2.0s")]
    public float[] Cooldowns = new float[] { 3.6f, 2.97f, 2.34f, 1.8f };

    [Header("2. Sát Thương Cơ Bản Từng Cấp (Base Damage)")]
    [Tooltip("Lv1: 38 (hoặc 65 DMG thực tế), Lv2: 70, Lv3: 102, Lv4: 134 (+32 mỗi cấp)")]
    public float[] BaseDamages = new float[] { 38f, 70f, 102f, 134f };

    [Header("3. Bán Kính Chém AOE Từng Cấp (Slash Radii in Meters)")]
    [Tooltip("Lv1: 3.2m, Lv2: 3.55m, Lv3: 3.9m, Lv4: 4.25m (+0.35m mỗi cấp)")]
    public float[] SlashRadii = new float[] { 3.2f, 3.55f, 3.9f, 4.25f };

    [Header("4. Số Lượng Kiếm Từng Cấp (Sword Counts)")]
    [Tooltip("Lv1: 2 kiếm, Lv2: 3 kiếm, Lv3: 4 kiếm, Lv4: 5 kiếm")]
    public int[] SwordCounts = new int[] { 2, 3, 4, 5 };

    [Header("5. Tốc Độ Áp Sát Chém Quái Từng Cấp (Approach Speeds)")]
    [Tooltip("Lv1: 13 m/s, Lv2: 16 m/s, Lv3: 19 m/s, Lv4: 22 m/s (+3 m/s mỗi cấp)")]
    public float[] ApproachSpeeds = new float[] { 13f, 16f, 19f, 22f };

    [Header("6. Thông Số Toàn Cục & Cơ Chế Hoạt Động")]
    [Tooltip("Tốc độ bay lượn quay về của kiếm sau khi chém (~14 m/s)")]
    public float ReturnSpeed = 14f;

    [Tooltip("Góc quét hình quạt chém bán nguyệt (~155 độ)")]
    public float SlashAngleDeg = 155f;

    [Tooltip("Tầm quét quái tự động tìm mục tiêu (~8.5m)")]
    public float DetectRange = 8.5f;

    [Header("7. Thông Số Kiếm Khí Cấp 4 (Piercing Sword Wave Lv4)")]
    [Tooltip("Sát thương luồng Kiếm Khí xuyên thấu ở Cấp 4 (~65 DMG)")]
    public float WaveDamage = 65f;

    [Tooltip("Vận tốc bay của Kiếm Khí (~15 m/s)")]
    public float WaveSpeed = 15f;

    [Tooltip("Thời gian tồn tại của Kiếm Khí (~1.6s)")]
    public float WaveDuration = 1.6f;

    [Tooltip("Bán kính va chạm của luồng Kiếm Khí (~1.2m)")]
    public float WaveRadius = 1.2f;

    public float GetCooldown(int level)
    {
        if (Cooldowns == null || Cooldowns.Length == 0) return 4.0f;
        int idx = Mathf.Clamp(level - 1, 0, Cooldowns.Length - 1);
        return Cooldowns[idx];
    }

    public float GetDamage(int level)
    {
        if (BaseDamages == null || BaseDamages.Length == 0) return 38f;
        int idx = Mathf.Clamp(level - 1, 0, BaseDamages.Length - 1);
        return BaseDamages[idx];
    }

    public float GetSlashRadius(int level)
    {
        if (SlashRadii == null || SlashRadii.Length == 0) return 3.2f;
        int idx = Mathf.Clamp(level - 1, 0, SlashRadii.Length - 1);
        return SlashRadii[idx];
    }

    public int GetSwordCount(int level)
    {
        if (SwordCounts == null || SwordCounts.Length == 0) return Mathf.Clamp(level + 1, 2, 5);
        int idx = Mathf.Clamp(level - 1, 0, SwordCounts.Length - 1);
        return SwordCounts[idx];
    }

    public float GetApproachSpeed(int level)
    {
        if (ApproachSpeeds == null || ApproachSpeeds.Length == 0) return 13f;
        int idx = Mathf.Clamp(level - 1, 0, ApproachSpeeds.Length - 1);
        return ApproachSpeeds[idx];
    }

    public float GetWaveDamage(int level)
    {
        return level >= 4 ? WaveDamage : 0f;
    }

    private void OnEnable()
    {
        _instance = this;
    }
}

