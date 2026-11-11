using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý tự nổ & bộc tốc cảm tử của quái tự sát (BomberTag):
/// - Khi tiếp cận trong bán kính áp sát, kích hoạt Kamikaze Rush (tăng tốc lao thẳng vào Player).
/// - Khi chạm bán kính nổ, Bomber KHÔNG nổ tức thì mà bắt đầu châm ngòi nổ đếm ngược (Fuse Delay ~0.25s),
///   đồng thời hãm phanh chậm lại và chớp sáng đỏ cảnh báo (Warning Telegraph).
/// - Giúp người chơi có cửa sổ phản xạ (0.2s - 0.3s) để bước lùi, né tránh hoặc tiêu diệt kịp thời.
/// - Hết thời gian ngòi nổ: Quái tự hủy (CurrentHP = 0), nếu Player vẫn trong tầm nổ sẽ nhận sát thương,
///   và gửi sự kiện nổ BomberExplosionEvent cho BomberVisualBridge.
/// </summary>
[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct BomberExplodeSystem : ISystem
{
    private ComponentLookup<EnemyHitReactionData> _hitReactionLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _hitReactionLookup = state.GetComponentLookup<EnemyHitReactionData>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);
        _hitReactionLookup.Update(ref state);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, explode, health, movement, rush, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<BomberExplodeData>, RefRW<HealthData>, RefRW<MovementData>, RefRW<BomberRushData>>()
                     .WithAll<BomberTag>()
                     .WithNone<EnemyStunData>()
                     .WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP <= 0f)
            {
                // Nếu Bomber bị tiêu diệt khi đang châm ngòi nổ dở dang, kích nổ ngay tại vị trí tử trận
                if (explode.ValueRO.IsPrimed)
                {
                    Entity eventEntity = ecb.CreateEntity();
                    ecb.AddComponent(eventEntity, new BomberExplosionEvent
                    {
                        Position = transform.ValueRO.Position,
                        Radius = explode.ValueRO.ExplodeRadius,
                        IsSuicide = false
                    });
                    explode.ValueRW.IsPrimed = false;
                }
                continue;
            }

            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = explode.ValueRO.ExplodeRadius * explode.ValueRO.ExplodeRadius;

            // 1. Kích hoạt bộc tốc cảm tử khi vào cự ly áp sát (nếu chưa châm ngòi nổ)
            float rushDistSq = rush.ValueRO.RushTriggerDistance * rush.ValueRO.RushTriggerDistance;
            if (distSq <= rushDistSq && !explode.ValueRO.IsPrimed)
            {
                rush.ValueRW.IsRushing = true;
                float2 toPlayer = (playerTransform.Position - transform.ValueRO.Position).xy;
                movement.ValueRW.Direction = math.normalizesafe(toPlayer) * rush.ValueRO.SpeedMultiplier;
            }

            // 2. Chạm bán kính nổ -> Bắt đầu đếm ngược ngòi nổ (Fuse Priming: 0.25s)
            if (!explode.ValueRO.IsPrimed)
            {
                if (distSq <= radiusSq)
                {
                    explode.ValueRW.IsPrimed = true;
                    float delay = explode.ValueRO.FuseDelay > 0f ? explode.ValueRO.FuseDelay : 0.25f;
                    explode.ValueRW.FuseTimer = delay;

                    // Hãm đà giảm tốc ngay khi bắt đầu châm ngòi nổ
                    movement.ValueRW.Direction *= 0.25f;

                    // Kích hoạt chớp sáng đỏ cảnh báo (Minecraft-Style Alert Flash)
                    if (_hitReactionLookup.HasComponent(entity))
                    {
                        var hitReaction = _hitReactionLookup[entity];
                        hitReaction.Timer = delay;
                        hitReaction.Duration = delay;
                        _hitReactionLookup[entity] = hitReaction;
                        _hitReactionLookup.SetComponentEnabled(entity, true);
                    }
                }
            }
            else
            {
                // Đang trong thời gian đếm ngòi nổ (Fuse delay ~0.25s)
                explode.ValueRW.FuseTimer -= dt;

                // Hãm phanh dần về 0 để Bomber dừng lại chuẩn bị nổ
                movement.ValueRW.Direction *= math.max(0f, 1f - dt * 6f);

                // Duy trì chớp sáng báo hiệu nguy hiểm sắp nổ
                if (_hitReactionLookup.HasComponent(entity))
                {
                    var hitReaction = _hitReactionLookup[entity];
                    if (hitReaction.Timer < explode.ValueRO.FuseTimer)
                    {
                        hitReaction.Timer = explode.ValueRO.FuseTimer;
                        _hitReactionLookup[entity] = hitReaction;
                        _hitReactionLookup.SetComponentEnabled(entity, true);
                    }
                }

                // Hết thời gian ngòi nổ -> PHÁT NỔ TỰ SÁT!
                if (explode.ValueRW.FuseTimer <= 0f)
                {
                    health.ValueRW.CurrentHP = 0f;
                    explode.ValueRW.IsPrimed = false;

                    // Chỉ gây sát thương nếu người chơi vẫn còn trong bán kính nổ tại thời điểm nổ!
                    float finalDistSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
                    if (finalDistSq <= radiusSq)
                    {
                        damageBuffer.Add(new IncomingDamage
                        {
                            Amount = explode.ValueRO.ExplodeDamage,
                            SourceType = DamageSourceType.Explosion,
                            SourcePosition = transform.ValueRO.Position
                        });
                    }

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
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
