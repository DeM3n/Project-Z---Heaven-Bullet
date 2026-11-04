using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct SlashSwordSkillTag : IComponentData {}

public struct SlashSwordTag : IComponentData {}

public struct SlashSwordVisualOffset : IComponentData
{
    public float RadianOffset; // Góc bù thị giác (radian) để xoay sprite cho đúng hướng mũi kiếm
}

public struct SwordWaveTag : IComponentData {}

public struct SlashSwordSkillData : IComponentData
{
    public Entity SwordPrefab;
    public Entity SwordWavePrefab;
    public float BaseDamage;
    public float ApproachSpeed;
    public float ReturnSpeed;
    public float SlashRadius;
    public float SlashAngleDeg;
    public float DetectRange;
    public float WaveSpeed;
    public float WaveDuration;
    public float WaveDamage;
    public float WaveRadius;
}

public struct SlashSwordData : IComponentData
{
    public Entity SkillEntity;
    public byte State;              // 0 = IdleOrbit, 1 = Approaching, 2 = Slashing, 3 = Returning
    public float OrbitAngle;
    public float CooldownTimer;     // Đếm lùi thời gian hồi chiêu
    public float StateTimer;        // Timeout cho trạng thái hiện tại
    public float3 TargetPosition;
    public Entity TargetEnemy;
    public float3 SlashDirection;
    public int SwordIndex;
    public int TotalSwords;
    public float SlashProgress;     // 0 -> 1 trong pha chém
}

public struct SwordWaveData : IComponentData
{
    public float2 Direction;
    public float Speed;
    public float Damage;
    public float Radius;
    public float Lifetime;
    public float MaxLifetime;
}

// Lưu danh sách các mục tiêu đã bị Kiếm Khí chém trúng để không gây sát thương lặp (Piercing)
public struct SwordWaveHitTargetBuffer : IBufferElementData
{
    public Entity Target;
}

/// <summary>
/// Event thông báo nhát chém diện rộng để Hybrid Visual Bridge sinh hiệu ứng vệt chém bán nguyệt
/// </summary>
public struct SlashVisualEvent : IComponentData
{
    public float3 Position;
    public float3 Direction;
    public float Radius;
}

/// <summary>
/// Dữ liệu cân bằng theo từng cấp độ cho Kỹ năng Trảm Kiếm (Slash Sword).
/// Nạp trực tiếp từ ScriptableObject (SlashSwordSkillConfigSO) cho Pure ECS Burst System xử lý.
/// </summary>
public struct SlashSwordLevelBalanceData : IComponentData
{
    public float CooldownLv1;
    public float CooldownLv2;
    public float CooldownLv3;
    public float CooldownLv4;

    public float DamageLv1;
    public float DamageLv2;
    public float DamageLv3;
    public float DamageLv4;

    public float SlashRadiusLv1;
    public float SlashRadiusLv2;
    public float SlashRadiusLv3;
    public float SlashRadiusLv4;

    public int SwordCountLv1;
    public int SwordCountLv2;
    public int SwordCountLv3;
    public int SwordCountLv4;

    public float ApproachSpeedLv1;
    public float ApproachSpeedLv2;
    public float ApproachSpeedLv3;
    public float ApproachSpeedLv4;

    public float WaveDamageLv4;

    public float GetCooldown(int level) => level switch { 2 => CooldownLv2, 3 => CooldownLv3, 4 => CooldownLv4, _ => CooldownLv1 };
    public float GetDamage(int level) => level switch { 2 => DamageLv2, 3 => DamageLv3, 4 => DamageLv4, _ => DamageLv1 };
    public float GetSlashRadius(int level) => level switch { 2 => SlashRadiusLv2, 3 => SlashRadiusLv3, 4 => SlashRadiusLv4, _ => SlashRadiusLv1 };
    public int GetSwordCount(int level) => level switch { 1 => SwordCountLv1, 2 => SwordCountLv2, 3 => SwordCountLv3, 4 => SwordCountLv4, _ => (level > 4 ? SwordCountLv4 : 0) };
    public float GetApproachSpeed(int level) => level switch { 2 => ApproachSpeedLv2, 3 => ApproachSpeedLv3, 4 => ApproachSpeedLv4, _ => ApproachSpeedLv1 };
    public float GetWaveDamage(int level) => level >= 4 ? WaveDamageLv4 : 0f;
}
