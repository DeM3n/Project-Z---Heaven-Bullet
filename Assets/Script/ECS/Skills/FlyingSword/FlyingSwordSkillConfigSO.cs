using UnityEngine;

/// <summary>
/// Cấu hình thông số Cân Bằng (Balance) cho Kỹ năng Phi Kiếm Hộ Thể (Flying Sword).
/// Thiết kế chuẩn ScriptableObject (Single Source of Truth):
/// - Designer tinh chỉnh thông số qua Unity Inspector tại: Assets/Resources/Skills/FlyingSwordSkillConfig.asset
/// - Baker nạp vào ECS Component (FlyingSwordLevelBalanceData) cho Pure ECS Burst System xử lý.
/// - Hệ thống Giao diện UI (Level-Up cards, Codex compendium) tự động lấy số liệu từ đây để hiển thị.
/// - Hỗ trợ Live Tuning lúc Play Mode qua SkillConfigSyncBridge.
/// </summary>
[CreateAssetMenu(fileName = "FlyingSwordSkillConfig", menuName = "Heaven Bullet/Skills/Flying Sword Config")]
public class FlyingSwordSkillConfigSO : ScriptableObject
{
    private static FlyingSwordSkillConfigSO _instance;
    public static FlyingSwordSkillConfigSO Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<FlyingSwordSkillConfigSO>("Skills/FlyingSwordSkillConfig");
                if (_instance == null)
                {
                    _instance = CreateInstance<FlyingSwordSkillConfigSO>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Sát Thương Cơ Bản Từng Cấp (Base Damage)")]
    [Tooltip("Lv1: 13.5, Lv2: 18.2 (+35%), Lv3: 23.0 (+70%), Lv4: 27.7 (+105%)")]
    public float[] BaseDamages = new float[] { 13.5f, 18.2f, 23.0f, 27.7f };

    [Header("2. Tốc Độ Lướt Tấn Công Từng Cấp (Rush Speeds in m/s)")]
    [Tooltip("Lv1: 9.45 m/s, Lv2: 11.34 m/s (+20%), Lv3: 13.23 m/s (+40%), Lv4: 15.12 m/s (+60%)")]
    public float[] RushSpeeds = new float[] { 9.45f, 11.34f, 13.23f, 15.12f };

    [Header("3. Số Lượng Kiếm Từng Cấp (Sword Counts)")]
    [Tooltip("Lv1: 1 kiếm, Lv2: 2 kiếm, Lv3: 3 kiếm, Lv4: 4 kiếm")]
    public int[] SwordCounts = new int[] { 1, 2, 3, 4 };

    [Header("4. Thông Số Quỹ Đạo Bay & Hành Vi Toàn Cục")]
    [Tooltip("Vận tốc bay quay về khi quá xa hoặc chuyển trạng thái (~11.0 m/s)")]
    public float ReturnSpeed = 11.0f;

    [Tooltip("Bán kính quỹ đạo bay lượn quanh Player khi ở trạng thái IDLE (~2.2m)")]
    public float OrbitRadius = 2.2f;

    [Tooltip("Tốc độ quay quanh quỹ đạo IDLE (~3.5 rad/s)")]
    public float OrbitSpeed = 3.5f;

    [Tooltip("Khoảng dừng ngắn giữa các nhịp khi ở IDLE (~0.5s)")]
    public float AttackInterval = 0.5f;

    [Tooltip("Bán kính va chạm trực tiếp của mũi kiếm (~0.5m)")]
    public float HitRadius = 0.5f;

    [Tooltip("Tầm quét quái trong tầm nhìn camera quanh Player (~6.5m)")]
    public float DetectRange = 6.5f;

    [Tooltip("Khoảng cách tối đa kiếm được phép cách xa Player trước khi bị buộc quay về (~7.5m)")]
    public float MaxLeashRange = 7.5f;

    [Header("5. Thông Số Kỹ Năng Ultimate Cấp 4 (Ultimate Strike & AOE Blast)")]
    [Tooltip("Thời gian hồi chiêu riêng của đòn Ultimate Level 4 (~6.0s)")]
    public float UltimateCooldown = 6.0f;

    [Tooltip("Bán kính nổ AOE của cú lướt Ultimate (~3.5m)")]
    public float AoeRadius = 3.5f;

    [Tooltip("Sát thương nổ AOE cực đại của đòn Ultimate (~85 DMG)")]
    public float AoeDamage = 85.0f;

    public float GetDamage(int level)
    {
        if (BaseDamages == null || BaseDamages.Length == 0) return 13.5f;
        int idx = Mathf.Clamp(level - 1, 0, BaseDamages.Length - 1);
        return BaseDamages[idx];
    }

    public float GetRushSpeed(int level)
    {
        if (RushSpeeds == null || RushSpeeds.Length == 0) return 9.45f;
        int idx = Mathf.Clamp(level - 1, 0, RushSpeeds.Length - 1);
        return RushSpeeds[idx];
    }

    public int GetSwordCount(int level)
    {
        if (SwordCounts == null || SwordCounts.Length == 0) return Mathf.Clamp(level, 1, 4);
        int idx = Mathf.Clamp(level - 1, 0, SwordCounts.Length - 1);
        return SwordCounts[idx];
    }

    private void OnEnable()
    {
        _instance = this;
    }
}

