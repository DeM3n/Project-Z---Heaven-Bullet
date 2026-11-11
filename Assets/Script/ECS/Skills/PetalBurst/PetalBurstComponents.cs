using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Đánh dấu Thực thể Kỹ năng Tiên Nữ Tán Hoa (Fairy Petal Burst)
/// </summary>
public struct PetalBurstSkillTag : IComponentData { }

/// <summary>
/// Đánh dấu từng Bông hoa / Cánh hoa đang bay trên chiến trường
/// </summary>
public struct PetalProjectileTag : IComponentData { }

/// <summary>
/// Cấu hình dữ liệu của kỹ năng Tiên Nữ Tán Hoa trên Skill Entity
/// </summary>
public struct PetalBurstSkillData : IComponentData
{
    public float BaseDamage;
    public float FlySpeed;
    public float MinSpeedMultiplier;
    public float MaxSpeedMultiplier;
    public float DragDeceleration;
    public float AngleSpreadJitter;
    public float HitRadius;
    public float ExplosionRadius;
    public float Duration;
    public int PetalCount;
    public float VortexPullRadius;
    public float VortexPullForce;
}

/// <summary>
/// Cấu hình chỉ số theo từng cấp độ nạp từ ScriptableObject SSOT
/// </summary>
public struct PetalBurstLevelBalanceData : IComponentData
{
    public float DamageLv1;
    public float DamageLv2;
    public float DamageLv3;
    public float DamageLv4;

    public float CooldownLv1;
    public float CooldownLv2;
    public float CooldownLv3;
    public float CooldownLv4;

    public int CountLv1;
    public int CountLv2;
    public int CountLv3;
    public int CountLv4;

    public float DurationLv1;
    public float DurationLv2;
    public float DurationLv3;
    public float DurationLv4;

    public float ExplosionRadiusLv1;
    public float ExplosionRadiusLv2;
    public float ExplosionRadiusLv3;
    public float ExplosionRadiusLv4;
}

/// <summary>
/// Dữ liệu vận hành của từng bông hoa đang bay
/// </summary>
public struct PetalProjectileData : IComponentData
{
    public float2 Direction;              // Hướng bay tỏa 360 độ
    public float Speed;                   // Vận tốc tức thời (giảm dần theo thời gian)
    public float DragDeceleration;        // Lực cản không khí làm chậm tốc độ (m/s^2)
    public float Damage;                  // Sát thương khi nổ
    public float HitRadius;               // Bán kính va chạm trực tiếp với quái
    public float ExplosionRadius;         // Bán kính nổ AOE
    public float Duration;                // Thời gian bay tối đa (rất ngắn: 0.42s - 0.60s)
    public float LifeTimer;               // Thời gian đã bay
    public int SkillLevel;                // Cấp độ kỹ năng (1-4)
    public Entity Owner;                  // Player hoặc Clone sở hữu
    public float RotationAngle;           // Góc xoay hiển thị của bông hoa
    public float RotationSpeed;           // Vận tốc xoay cánh hoa (rad/s)
    public float VortexPullRadius;        // Bán kính hút quái (Cấp 4)
    public float VortexPullForce;         // Lực hút quái vào tâm (Cấp 4)
}

/// <summary>
/// Sự kiện điểm nổ của bông hoa để Hybrid Visual Bridge sinh hiệu ứng nổ và lốc xoáy cánh hoa
/// </summary>
public struct PetalDetonationEvent : IComponentData
{
    public float3 Position;
    public float ExplosionRadius;
    public int SkillLevel;
    public bool HasVortex;
}

