using UnityEngine;

/// <summary>
/// Cấu hình thông số Cân Bằng (Balance) cho Kỹ năng Boomerang (Phi Tiêu Hồi Toàn).
/// Thiết kế chuẩn ScriptableObject (Single Source of Truth):
/// - Designer tinh chỉnh thông số qua Inspector tại: Assets/Resources/Skills/BoomerangSkillConfig.asset
/// - Baker nạp vào ECS Component (BoomerangLevelBalanceData) cho Burst System xử lý.
/// - UI và Codex tự động đồng bộ qua Singleton BoomerangSkillConfigSO.Instance.
/// </summary>
[CreateAssetMenu(fileName = "BoomerangSkillConfig", menuName = "Heaven Bullet/Skills/Boomerang Config")]
public class BoomerangSkillConfigSO : ScriptableObject
{
    private static BoomerangSkillConfigSO _instance;
    public static BoomerangSkillConfigSO Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<BoomerangSkillConfigSO>("Skills/BoomerangSkillConfig");
                if (_instance == null)
                {
                    _instance = ScriptableObject.CreateInstance<BoomerangSkillConfigSO>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Thời Gian Hồi Chiêu Từng Cấp (Cooldowns in Seconds)")]
    [Tooltip("Lv1: 8.0s, Lv2: 8.0s, Lv3: 6.0s, Lv4: 5.5s")]
    public float[] Cooldowns = new float[] { 8.0f, 8.0f, 6.0f, 5.5f };

    [Header("2. Sát Thương Cơ Bản Từng Cấp (Base Damage)")]
    [Tooltip("Lv1: 35 (Trung bình), Lv2: 55 (+57%), Lv3: 55, Lv4: 80 (+45%)")]
    public float[] BaseDamages = new float[] { 35f, 55f, 55f, 80f };

    [Header("3. Bán Kính Hitbox Từng Cấp (Hitbox Radii in Meters)")]
    [Tooltip("Lv1: 0.55m, Lv2: 0.85m (To rõ rệt), Lv3: 0.85m, Lv4: 0.95m")]
    public float[] HitboxRadii = new float[] { 0.55f, 0.85f, 0.85f, 0.95f };

    [Header("4. Số Lượng Phi Tiêu Từng Cấp (Boomerang Counts)")]
    [Tooltip("Lv1: 1, Lv2: 1, Lv3: 2 (+1 Song Tiêu Đa Mục Tiêu), Lv4: 2")]
    public int[] BoomerangCounts = new int[] { 1, 1, 2, 2 };

    [Header("5. Thông Số Vận Tốc & Cơ Chế Bay")]
    [Tooltip("Tốc độ bay của phi tiêu (~12.5 m/s)")]
    public float FlySpeed = 12.5f;

    [Tooltip("Cự ly bay tối đa trước khi đổi chiều quay về (~7.5m)")]
    public float MaxDistance = 7.5f;

    [Tooltip("Bán kính vùng người chơi chụp dính phi tiêu (~1.4m)")]
    public float CatchRadius = 1.4f;

    [Tooltip("Tầm quét quái tự động tìm mục tiêu (~9.5m)")]
    public float DetectRange = 9.5f;

    [Header("6. Hiệu Ứng Cấp 4 (Empowered Rebound)")]
    [Tooltip("Hệ số nhân sát thương đòn bắn phản hồi khi chụp dính ở Cấp 4 (x1.6 = +60% DMG)")]
    public float EmpoweredMultiplier = 1.6f;

    public float GetCooldown(int level)
    {
        if (Cooldowns == null || Cooldowns.Length == 0) return 8.0f;
        int idx = Mathf.Clamp(level - 1, 0, Cooldowns.Length - 1);
        return Cooldowns[idx];
    }

    public float GetDamage(int level)
    {
        if (BaseDamages == null || BaseDamages.Length == 0) return 35f;
        int idx = Mathf.Clamp(level - 1, 0, BaseDamages.Length - 1);
        return BaseDamages[idx];
    }

    public float GetHitRadius(int level)
    {
        if (HitboxRadii == null || HitboxRadii.Length == 0) return 0.55f;
        int idx = Mathf.Clamp(level - 1, 0, HitboxRadii.Length - 1);
        return HitboxRadii[idx];
    }

    public int GetCount(int level)
    {
        if (BoomerangCounts == null || BoomerangCounts.Length == 0) return 1;
        int idx = Mathf.Clamp(level - 1, 0, BoomerangCounts.Length - 1);
        return BoomerangCounts[idx];
    }

    private void OnEnable()
    {
        _instance = this;
    }
}
