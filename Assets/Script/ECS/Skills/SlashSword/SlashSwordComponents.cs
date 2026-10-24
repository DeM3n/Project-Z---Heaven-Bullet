using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct SlashSwordSkillTag : IComponentData {}

public struct SlashSwordTag : IComponentData {}

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
