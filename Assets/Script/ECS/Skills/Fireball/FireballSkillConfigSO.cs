using UnityEngine;

/// <summary>
/// Cấu hình thông số Cân Bằng (Balance) cho Kỹ năng Cầu Lửa (Fireball Skill).
/// Được thiết kế dưới dạng ScriptableObject (Single Source of Truth):
/// - Designer có thể chỉnh sửa trực tiếp thông số qua Unity Inspector tại: Assets/Resources/Skills/FireballSkillConfig.asset
/// - Baker tự động nạp thông số này vào ECS Component (FireballLevelBalanceData) cho Burst System xử lý.
/// - Hệ thống Giao diện UI (Level-Up cards, Codex compendium) tự động lấy số liệu từ đây để hiển thị.
/// </summary>
[CreateAssetMenu(fileName = "FireballSkillConfig", menuName = "Heaven Bullet/Skills/Fireball Config")]
public class FireballSkillConfigSO : ScriptableObject
{
    private static FireballSkillConfigSO _instance;
    public static FireballSkillConfigSO Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<FireballSkillConfigSO>("Skills/FireballSkillConfig");
            }
            return _instance;
        }
    }

    [Header("1. Thời Gian Hồi Chiêu Từng Cấp (Cooldowns in Seconds)")]
    [Tooltip("Thời gian hồi chiêu từng cấp: Lv1, Lv2, Lv3, Lv4")]
    public float[] Cooldowns = new float[] { 2.86f, 2.53f, 2.20f, 1.98f };

    [Header("2. Sát Thương Nổ Cầu Mẹ Từng Cấp (Base Explosion Damage)")]
    [Tooltip("Sát thương nổ của cầu lửa mẹ từng cấp: Lv1, Lv2, Lv3, Lv4")]
    public float[] BaseDamages = new float[] { 45f, 68f, 95f, 120f };

    [Header("3. Số Lượng Cầu Con Nổ Chùm (Cluster Sub-Fireballs)")]
    [Tooltip("Số lượng cầu lửa con văng ra khi cầu mẹ phát nổ (50% DMG mỗi quả)")]
    public int[] SubFireballCounts = new int[] { 1, 2, 3, 4 };

    [Header("4. Bán Kính Nổ AOE Từng Cấp (Explosion Radii in Meters)")]
    [Tooltip("Bán kính nổ AOE của cầu mẹ từng cấp: Lv1, Lv2, Lv3, Lv4")]
    public float[] ExplosionRadii = new float[] { 1.8f, 2.1f, 2.4f, 2.8f };

    [Header("5. Thông Số Vận Tốc & Tầm Quét Toàn Cục")]
    [Tooltip("Tốc độ bay của cầu lửa mẹ (~9.5 m/s)")]
    public float FireballSpeed = 9.5f;
    [Tooltip("Tốc độ văng của cầu lửa con (~11.0 m/s)")]
    public float SubFireballSpeed = 11.0f;
    [Tooltip("Thời gian bay tỏa ra của cầu lửa con (~0.45s)")]
    public float SubFireballLifetime = 0.45f;
    [Tooltip("Bán kính nổ AOE của cầu lửa con (~1.2m)")]
    public float SubExplosionRadius = 1.2f;
    [Tooltip("Tầm quét quái tự động tìm mục tiêu (~9.5m)")]
    public float DetectRange = 9.5f;

    [Header("6. Vùng Lửa Thiêu Đốt Cấp 4 (Scorched Fire Zone DOT)")]
    [Tooltip("Bán kính vùng lửa thiêu đốt Lv4 (~1.8m)")]
    public float FireZoneRadius = 1.8f;
    [Tooltip("Thời gian tồn tại của vùng lửa Lv4 (~3.5s)")]
    public float FireZoneDuration = 3.5f;
    [Tooltip("Chu kỳ gây sát thương DOT (~0.25s / tick)")]
    public float FireZoneTickInterval = 0.25f;
    [Tooltip("Sát thương mỗi tick DOT (~16 DMG)")]
    public float FireZoneDamagePerTick = 16f;

    public float GetCooldown(int level)
    {
        if (Cooldowns == null || Cooldowns.Length == 0) return 2.86f;
        int idx = Mathf.Clamp(level - 1, 0, Cooldowns.Length - 1);
        return Cooldowns[idx];
    }

    public float GetDamage(int level)
    {
        if (BaseDamages == null || BaseDamages.Length == 0) return 45f;
        int idx = Mathf.Clamp(level - 1, 0, BaseDamages.Length - 1);
        return BaseDamages[idx];
    }

    public int GetSubFireballCount(int level)
    {
        if (SubFireballCounts == null || SubFireballCounts.Length == 0) return level;
        int idx = Mathf.Clamp(level - 1, 0, SubFireballCounts.Length - 1);
        return SubFireballCounts[idx];
    }

    public float GetExplosionRadius(int level)
    {
        if (ExplosionRadii == null || ExplosionRadii.Length == 0) return 1.8f;
        int idx = Mathf.Clamp(level - 1, 0, ExplosionRadii.Length - 1);
        return ExplosionRadii[idx];
    }

    private void OnEnable()
    {
        _instance = this;
    }
}

