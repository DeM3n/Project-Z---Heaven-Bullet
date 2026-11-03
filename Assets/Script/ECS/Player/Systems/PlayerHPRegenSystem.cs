using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Hệ thống xử lý tự hồi phục máu mỗi giây (HP Regen) cho nhân vật chính
/// </summary>
[UpdateAfter(typeof(PlayerDamageResolutionSystem))]
[BurstCompile]
public partial struct PlayerHPRegenSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (health, regen) in
                 SystemAPI.Query<RefRW<HealthData>, RefRW<PlayerHPRegenData>>()
                     .WithAll<PlayerTag>()
                     .WithNone<PlayerDeadTag>())
        {
            if (regen.ValueRO.RegenPerSecond <= 0f) continue;

            if (health.ValueRO.CurrentHP >= health.ValueRO.MaxHP)
            {
                regen.ValueRW.Timer = 0f;
                continue;
            }

            regen.ValueRW.Timer += dt;
            if (regen.ValueRO.Timer >= 1.0f)
            {
                float ticks = math.floor(regen.ValueRO.Timer);
                regen.ValueRW.Timer -= ticks;
                health.ValueRW.CurrentHP = math.min(health.ValueRO.MaxHP, health.ValueRO.CurrentHP + regen.ValueRO.RegenPerSecond * ticks);
            }
        }
    }
}

