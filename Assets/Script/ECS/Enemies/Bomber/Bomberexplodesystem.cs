using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý tự nổ & bộc tốc cảm tử của quái tự sát (BomberTag).
/// - Khi tiếp cận trong bán kính áp sát, kích hoạt Kamikaze Rush (tăng tốc lao thẳng vào Player).
/// - Khi tiếp cận trong bán kính nổ, quái lập tức tự hủy (CurrentHP = 0),
///   gửi gói sát thương nổ AOE vào IncomingDamage buffer của Player,
///   và sinh sự kiện nổ BomberExplosionEvent cho BomberVisualBridge.
/// </summary>
[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct BomberExplodeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, explode, health, movement, rush, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<BomberExplodeData>, RefRW<HealthData>, RefRW<MovementData>, RefRW<BomberRushData>>()
                     .WithAll<BomberTag>()
                     .WithNone<EnemyStunData>()
                     .WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP <= 0f) continue; // Đã chết, để DeathCleanupSystem xử lý

            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = explode.ValueRO.ExplodeRadius * explode.ValueRO.ExplodeRadius;

            // 1. Kích hoạt bộc tốc cảm tử khi vào cự ly áp sát
            float rushDistSq = rush.ValueRO.RushTriggerDistance * rush.ValueRO.RushTriggerDistance;
            if (distSq <= rushDistSq)
            {
                rush.ValueRW.IsRushing = true;
                float2 toPlayer = (playerTransform.Position - transform.ValueRO.Position).xy;
                movement.ValueRW.Direction = math.normalizesafe(toPlayer) * rush.ValueRO.SpeedMultiplier;
            }

            // 2. Chạm bán kính nổ -> Phát nổ tự sát
            if (distSq <= radiusSq)
            {
                health.ValueRW.CurrentHP = 0f;

                // Gửi gói sát thương vào IncomingDamage buffer của Player
                damageBuffer.Add(new IncomingDamage
                {
                    Amount = explode.ValueRO.ExplodeDamage,
                    SourceType = DamageSourceType.Explosion,
                    SourcePosition = transform.ValueRO.Position
                });

                // Sinh sự kiện nổ cho Visual Bridge và Audio
                Entity eventEntity = ecb.CreateEntity();
                ecb.AddComponent(eventEntity, new BomberExplosionEvent
                {
                    Position = transform.ValueRO.Position,
                    Radius = explode.ValueRO.ExplodeRadius,
                    IsSuicide = true
                });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
