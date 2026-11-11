using UnityEngine;

/// <summary>
/// Cấu hình thông số Cân Bằng (Balance) cho Kỹ năng Tiên Nữ Tán Hoa (Fairy Petal Burst).
/// Chuẩn ScriptableObject (Single Source of Truth):
/// - Designer tinh chỉnh thông số qua Inspector tại: Assets/Resources/Skills/PetalBurstSkillConfig.asset
/// - Baker nạp vào ECS Component (PetalBurstLevelBalanceData) cho Burst System xử lý.
/// - UI và Codex tự động đồng bộ qua Singleton PetalBurstSkillConfigSO.Instance.
/// </summary>
[CreateAssetMenu(fileName = "PetalBurstSkillConfig", menuName = "Heaven Bullet/Skills/Petal Burst Config")]
public class PetalBurstSkillConfigSO : ScriptableObject
{
    private static PetalBurstSkillConfigSO _instance;
    public static PetalBurstSkillConfigSO Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<PetalBurstSkillConfigSO>("Skills/PetalBurstSkillConfig");
                if (_instance == null)
                {
                    _instance = ScriptableObject.CreateInstance<PetalBurstSkillConfigSO>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Thời Gian Hồi Chiêu Từng Cấp (Cooldowns in Seconds)")]
    [Tooltip("Lv1: 3.5s, Lv2: 3.0s, Lv3: 2.6s, Lv4: 2.2s")]
    public float[] Cooldowns = new float[] { 3.5f, 3.0f, 2.6f, 2.2f };

    [Header("2. Sát Thương Cơ Bản Từng Cấp (Base Damages)")]
    [Tooltip("Lv1: 22, Lv2: 32, Lv3: 48, Lv4: 70 (+46%)")]
    public float[] BaseDamages = new float[] { 22f, 32f, 48f, 70f };

    [Header("3. Số Lượng Hoa Tỏa Ra Từng Cấp (Petal Counts 360 Deg)")]
    [Tooltip("Lv1: 6 hoa, Lv2: 9 hoa, Lv3: 13 hoa, Lv4: 18 hoa")]
    public int[] PetalCounts = new int[] { 6, 9, 13, 18 };

    [Header("4. Thời Gian Tồn Tại Từng Cấp (Flight Durations in Seconds)")]
    [Tooltip("Thời gian tồn tại tăng nhẹ giúp hoa trôi bồng bềnh lâu hơn: Lv1: 0.85s, Lv2: 0.95s, Lv3: 1.10s, Lv4: 1.25s")]
    public float[] Durations = new float[] { 0.85f, 0.95f, 1.10f, 1.25f };

    [Header("5. Bán Kính Nổ AOE Từng Cấp (Explosion Radii in Meters)")]
    [Tooltip("Lv1: 0.85m, Lv2: 0.95m, Lv3: 1.10m, Lv4: 1.35m")]
    public float[] ExplosionRadii = new float[] { 0.85f, 0.95f, 1.10f, 1.35f };

    [Header("6. Thông Số Vận Tốc & Độ Phân Tán (Particle Burst Dynamics)")]
    [Tooltip("Vận tốc phóng trung bình ban đầu (~8.5 m/s)")]
    public float FlySpeed = 8.5f;

    [Tooltip("Tỉ lệ vận tốc tối thiểu so với FlySpeed (vd: 0.65 = 65% tốc độ gốc)")]
    public float MinSpeedMultiplier = 0.65f;

    [Tooltip("Tỉ lệ vận tốc tối đa so với FlySpeed (vd: 1.35 = 135% tốc độ gốc)")]
    public float MaxSpeedMultiplier = 1.35f;

    [Tooltip("Lực cản không khí nhẹ nhàng giúp hoa trôi chậm dần đều (~7.0 m/s^2)")]
    public float DragDeceleration = 7.0f;

    [Tooltip("Độ lệch ngẫu nhiên góc phóng (+/- radian) mô phỏng Particle Burst")]
    public float AngleSpreadJitter = 0.65f;

    [Tooltip("Bán kính va chạm trực tiếp với quái khi đang bay (~0.35m)")]
    public float HitRadius = 0.35f;

    [Header("7. Hiệu Ứng Cấp 4: Hoa Trận Hút Quái (Petal Implosion Vortex)")]
    [Tooltip("Bán kính hút quái vào tâm nổ ở Cấp 4 (~2.4m)")]
    public float VortexPullRadius = 2.4f;

    [Tooltip("Vận tốc kéo hút quái vật vào tâm nổ ở Cấp 4 (~8.5 m/s)")]
    public float VortexPullForce = 8.5f;

    public float GetCooldown(int level) => GetSafe(Cooldowns, level, 3.5f);
    public float GetDamage(int level) => GetSafe(BaseDamages, level, 22f);
    public int GetCount(int level) => GetSafe(PetalCounts, level, 6);
    public float GetDuration(int level) => GetSafe(Durations, level, 0.42f);
    public float GetExplosionRadius(int level) => GetSafe(ExplosionRadii, level, 0.85f);

    private static T GetSafe<T>(T[] array, int level, T fallback)
    {
        if (array == null || array.Length == 0) return fallback;
        int idx = Mathf.Clamp(level - 1, 0, array.Length - 1);
        return array[idx];
    }
}

