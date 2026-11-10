using Unity.Entities;
using Unity.Mathematics;

public struct ChaserTag : IComponentData {}
public struct TankTag : IComponentData {}
public struct ShooterTag : IComponentData {}
public struct BomberTag : IComponentData {}
public struct EnemyProjectileTag : IComponentData {}

public struct ShooterAttackData : IComponentData
{
    public float Cooldown;
    public float Timer;
    public float PreferredRange;
    public float Damage;
    public float ProjectileSpeed;
    public float ProjectileHitRadius;
    public Entity ProjectilePrefab;
}

public struct BomberExplodeData : IComponentData
{
    public float ExplodeRadius;
    public float ExplodeDamage;
}

// Áp dụng cho MỌI enemy (Chaser/Tank/Shooter/Bomber) — va chạm trực tiếp với Player
public struct EnemyContactDamageData : IComponentData
{
    public float Damage;
    public float ContactRadius;
    public float TickInterval;
    public float Timer;
}

/// <summary>
/// Dữ liệu AI Flocking: Tách đàn (Separation) chống đè chồng quái và Bao vây (Flanking / Encirclement).
/// Giúp đàn quái dàn hàng ngang tự nhiên và mở rộng sang hai cánh khi áp sát Player.
/// </summary>
public struct EnemyFlockingData : IComponentData
{
    public float SeparationRadius; // Bán kính đẩy tách nhau (vd: 0.75m - 1.1m)
    public float SeparationWeight; // Trọng số lực đẩy tách nhau (vd: 0.6f)
    public float FlankAngleOffset;  // Góc lệch cánh bọc sườn (+/- 0.6f rad)
    public float TangentialWeight;  // Trọng số lực lượn vòng bao vây khi ở gần Player (< 6m)
}

/// <summary>
/// Dữ liệu chiến thuật của Xạ Thủ (Shooter):
/// Khi trong tầm bắn lý tưởng, Shooter di chuyển theo hình tròn (Circle-Strafe) quanh Player
/// thay vì đứng yên một chỗ, và chủ động giữ cự ly né tránh (Kiting).
/// </summary>
public struct ShooterTacticsData : IComponentData
{
    public float StrafeDirection;  // +1.0f (theo chiều kim đồng hồ) hoặc -1.0f (ngược chiều)
    public float StrafeTimer;      // Bộ đếm đổi hướng quay
    public float CircleStrafeSpeed;// Hệ số tốc độ lượn vòng (vd: 0.85f)
}

/// <summary>
/// Dữ liệu Cuồng Bạo (Kamikaze Rush) của Bomber:
/// Khi áp sát trong cự ly 4.2m, Bomber tăng tốc 1.6x lao thẳng vào Player để tự nổ.
/// </summary>
public struct BomberRushData : IComponentData
{
    public float RushTriggerDistance;
    public float SpeedMultiplier;
    public bool IsRushing;
}

/// <summary>
/// Sự kiện vụ nổ cảm tử của Bomber khi áp sát người chơi (gửi từ ECS sang BomberVisualBridge).
/// </summary>
public struct BomberExplosionEvent : IComponentData
{
    public float3 Position;
    public float Radius;
    public bool IsSuicide;
}
