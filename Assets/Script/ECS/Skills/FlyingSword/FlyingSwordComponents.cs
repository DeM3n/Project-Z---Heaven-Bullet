using Unity.Entities;
using Unity.Mathematics;

public struct FlyingSwordSkillTag : IComponentData {}

public struct FlyingSwordTag : IComponentData {}

public struct FlyingSwordVisualOffset : IComponentData
{
    public float RadianOffset; // Góc bù thị giác (radian) để xoay sprite cho đúng hướng mũi kiếm
}

public struct FlyingSwordSkillData : IComponentData
{
    public Entity SwordPrefab;
    public float BaseDamage;
    public float RushSpeed;
    public float ReturnSpeed;
    public float OrbitRadius;
    public float OrbitSpeed;
    public float AttackInterval;
    public float HitRadius;
    public float AoeRadius;
    public float AoeDamage;
    public float DetectRange;       // Giới hạn tìm quái quanh Player (tầm nhìn camera ~6.5m)
    public float MaxLeashRange;      // Cự ly tối đa kiếm được phép rời xa Player (~7.5m)
    public float UltimateCooldown;   // Cooldown riêng cho chiêu Ultimate AOE Level 4 (~6s)
}

public struct FlyingSwordData : IComponentData
{
    public Entity SkillEntity;
    public byte State;              // 0 = Orbiting (IDLE), 1 = Rushing (COMBAT liên tục), 2 = Returning
    public float CurrentAngle;
    public float Timer;             // Timeout cho cú lướt hiện tại
    public float3 TargetPosition;
    public int SwordIndex;
    public int TotalSwords;
    public float UltimateTimer;      // Đếm lùi cooldown của chiêu Ultimate Level 4
    public bool IsUltimate;          // Cú lướt hiện tại có phải Ultimate quét cụm đông + phá đạn không
    public Entity LastTarget;        // Mục tiêu vừa bị đâm trúng gần nhất để tránh lặp lại liên tục
    public int LoopDirection;        // Hướng bay vòng (+1 hoặc -1) khi lượn quanh 1 mục tiêu duy nhất
}

// Lưu danh sách các target đã bị chém trong đợt lướt hiện tại để tránh gây sát thương lặp nhiều lần
public struct SwordHitTargetBuffer : IBufferElementData
{
    public Entity Target;
}

/// <summary>
/// Event thông báo vụ nổ AOE Ultimate Level 4 của Phi Kiếm để Hybrid Bridge render hiệu ứng Hitbox Shockwave
/// </summary>
public struct FlyingSwordAoeBlastEvent : IComponentData
{
    public float3 Position;
    public float Radius;
}

/// <summary>
/// Dữ liệu cân bằng theo từng cấp độ cho Kỹ năng Phi Kiếm (Flying Sword).
/// Nạp trực tiếp từ ScriptableObject (FlyingSwordSkillConfigSO) cho Pure ECS Burst System xử lý.
/// </summary>
public struct FlyingSwordLevelBalanceData : IComponentData
{
    public float DamageLv1;
    public float DamageLv2;
    public float DamageLv3;
    public float DamageLv4;

    public float RushSpeedLv1;
    public float RushSpeedLv2;
    public float RushSpeedLv3;
    public float RushSpeedLv4;

    public int SwordCountLv1;
    public int SwordCountLv2;
    public int SwordCountLv3;
    public int SwordCountLv4;

    public float GetDamage(int level) => level switch { 2 => DamageLv2, 3 => DamageLv3, 4 => DamageLv4, _ => DamageLv1 };
    public float GetRushSpeed(int level) => level switch { 2 => RushSpeedLv2, 3 => RushSpeedLv3, 4 => RushSpeedLv4, _ => RushSpeedLv1 };
    public int GetSwordCount(int level) => level switch { 1 => SwordCountLv1, 2 => SwordCountLv2, 3 => SwordCountLv3, 4 => SwordCountLv4, _ => (level > 4 ? SwordCountLv4 : 0) };
}
