using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct SwordFusionTag : IComponentData {}

public struct SwordFusionSkillData : IComponentData
{
    public Entity GiantSwordPrefab;
    public Entity SwordWavePrefab;
    public float Cooldown;
    public float Timer;
    public float ImpactDamage;
    public float ImpactRadius;
    public float WaveDamage;
    public float WaveSpeed;
    public float WaveLifetime;
    public float DetectRange;
}

public struct GiantSwordTag : IComponentData {}

public struct GiantSwordData : IComponentData
{
    public float3 TargetPosition;
    public float3 CurrentPosition;
    public float FallSpeed;
    public float StateTimer;
    public byte State;               // 0 = Falling, 1 = Embedded, 2 = Completed
    public Entity SwordWavePrefab;
    public float ImpactDamage;
    public float ImpactRadius;
    public float WaveDamage;
    public float WaveSpeed;
    public float WaveLifetime;
}

/// <summary>
/// Event thông báo đòn cắm kiếm khổng lồ để Hybrid Bridge render hiệu ứng Shockwave chấn động
/// </summary>
public struct GiantSwordImpactEvent : IComponentData
{
    public float3 Position;
    public float Radius;
}
