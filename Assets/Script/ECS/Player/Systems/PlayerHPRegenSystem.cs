using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Hệ thống xử lý tự hồi phục máu mỗi giây (HP Regen) cho nhân vật từ Cây Thiên Phú
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
            if (health.ValueRO.CurrentHP >= health.ValueRO.MaxHP) continue;

            regen.ValueRW.Timer += dt;
            if (regen.ValueRO.Timer >= 1.0f)
            {
                regen.ValueRW.Timer -= 1.0f;
                health.ValueRW.CurrentHP = math.min(health.ValueRO.MaxHP, health.ValueRO.CurrentHP + regen.ValueRO.RegenPerSecond);
            }
        }
    }
}
