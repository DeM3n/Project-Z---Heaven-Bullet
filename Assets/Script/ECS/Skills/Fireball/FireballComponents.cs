using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Đánh dấu entity chứa cấu hình và bộ đếm thời gian của kỹ năng Cầu Lửa (Fireball Skill)
/// </summary>
public struct FireballSkillTag : IComponentData {}

/// <summary>
/// Đánh dấu thực thể Cầu Lửa Mẹ (Main Fireball)
/// </summary>
public struct FireballTag : IComponentData {}

/// <summary>
/// Đánh dấu thực thể Cầu Lửa Con nổ chùm (Sub-Fireball / Cluster Fireball)
/// </summary>
public struct SubFireballTag : IComponentData {}

/// <summary>
/// Đánh dấu Vùng Lửa thiêu đốt DOT trên mặt đất (Scorched Earth / Fire Zone - Level 4)
/// </summary>
public struct FireZoneTag : IComponentData {}

/// <summary>
/// Cấu hình thông số kỹ năng Cầu Lửa gắn trên Skill Entity
/// </summary>
public struct FireballSkillData : IComponentData
{
    public Entity FireballPrefab;          // Prefab cầu lửa (nếu có, Entity.Null nếu procedural)
    public Entity SubFireballPrefab;       // Prefab cầu lửa con (nếu có, Entity.Null nếu procedural)
    public float BaseDamage;               // Sát thương nổ cơ bản (45 ở Lv1)
    public float FireballSpeed;            // Tốc độ bay của cầu lửa mẹ (~9.5m/s)
    public float ExplosionRadius;          // Bán kính nổ AOE của cầu lửa mẹ (~1.8m ở Lv1)
    public float SubFireballSpeed;         // Tốc độ văng của cầu lửa con (~11.0m/s)
    public float SubFireballLifetime;      // Thời gian bay tỏa ra của cầu lửa con (~0.45s)
    public float SubExplosionRadius;       // Bán kính nổ AOE của cầu lửa con (~1.2m)
    public float FireZoneRadius;           // Bán kính vùng lửa thiêu đốt Lv4 (~1.8m)
    public float FireZoneDuration;         // Thời gian duy trì vùng lửa Lv4 (~3.5s)
    public float FireZoneTickInterval;     // Nhịp gây sát thương DOT (~0.25s)
    public float FireZoneDamagePerTick;    // Sát thương mỗi tick DOT (~16 DMG)
    public float DetectRange;              // Tầm quét tìm mục tiêu quái (~9.5m)
}

/// <summary>
/// Dữ liệu vận hành gắn trên từng viên đạn Cầu Lửa đang bay
/// </summary>
public struct FireballProjectileData : IComponentData
{
    public float2 Direction;              // Hướng bay (dành cho Cầu Lửa Mẹ)
    public float Speed;                    // Vận tốc bay
    public float Damage;                   // Sát thương nổ
    public float ExplosionRadius;          // Bán kính phát nổ AOE
    public float Lifetime;                 // Thời gian sống còn lại
    public float HitRadius;                // Bán kính quét va chạm quái (~0.35m)
    public int SubFireballCount;           // Số lượng cầu lửa con nổ chùm văng ra (1 ở Lv1, 2 ở Lv2, 3 ở Lv3, 4 ở Lv4)
    public int SkillLevel;                 // Cấp độ kỹ năng khi bắn
    public bool IsSubFireball;             // true = cầu lửa con (bay vòng cung lên trời rồi rớt), false = cầu lửa mẹ

    // --- Quỹ đạo hình Parabol (Mortar Lob / Bay lên trời rồi rơi xuống) ---
    public float2 StartPosition;           // Tọa độ mặt đất xuất phát
    public float2 TargetPosition;          // Tọa độ mục tiêu tiếp đất
    public float ArcHeight;                // Độ cao vút lên không trung (~2.5m - 3.2m)
    public float TotalDuration;            // Tổng thời gian bay trên không (~0.65s)
    public float ElapsedTime;              // Thời gian đã bay
}

/// <summary>
/// Dữ liệu Vùng Lửa thiêu đốt gây sát thương DOT theo thời gian (Level 4)
/// </summary>
public struct FireZoneData : IComponentData
{
    public float2 Center;                  // Vị trí tâm vùng lửa
    public float Radius;                   // Bán kính vùng lửa
    public float DurationRemaining;        // Thời gian tồn tại còn lại
    public float TickInterval;             // Khoảng cách giữa các đợt gây sát thương (0.25s)
    public float TickTimer;                // Đếm ngược đến đợt gây sát thương tiếp theo
    public float DamagePerTick;            // Sát thương trừ máu mỗi nhịp
}

/// <summary>
/// Event thông báo vụ nổ cầu lửa (được Visual Bridge lắng nghe để sinh hiệu ứng nổ & rung camera)
/// </summary>
public struct FireballExplosionEvent : IComponentData
{
    public float3 Position;
    public float Radius;
    public bool IsMainExplosion;           // true = nổ lớn cầu mẹ, false = nổ chùm cầu con
}
