using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống kích hoạt tự phát nổ của quái tự sát (BomberTag).
/// Khi tiếp cận trong bán kính nổ của Player, quái tự sát lập tức chết (CurrentHP = 0)
/// và gửi gói sát thương nổ AOE vào IncomingDamage buffer của Player.
/// Không xử lý logic trừ máu/khiên/i-frames (thuộc trách nhiệm của PlayerDamageResolutionSystem).
/// </summary>
[BurstCompile]
public partial struct BomberExplodeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);

        foreach (var (transform, explode, health) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<BomberExplodeData>, RefRW<HealthData>>()
                     .WithAll<BomberTag>())
        {
            if (health.ValueRO.CurrentHP <= 0f) continue; // Đã chết, để DeathCleanupSystem xử lý

            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = explode.ValueRO.ExplodeRadius * explode.ValueRO.ExplodeRadius;
            if (distSq > radiusSq) continue;

            // Kích hoạt phát nổ tự chết
            health.ValueRW.CurrentHP = 0f;

            // Đẩy gói sát thương nổ vào IncomingDamage buffer của Player
            damageBuffer.Add(new IncomingDamage
            {
                Amount = explode.ValueRO.ExplodeDamage,
                SourceType = DamageSourceType.Explosion,
                SourcePosition = transform.ValueRO.Position
            });
        }
    }
}