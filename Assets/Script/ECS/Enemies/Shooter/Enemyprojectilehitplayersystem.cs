using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống kiểm tra va chạm giữa đạn của quái vật (EnemyProjectileTag) và người chơi.
/// Khi đạn chạm Player, viên đạn bị tiêu hủy ngay lập tức và đẩy gói sát thương vào IncomingDamage buffer.
/// Không xử lý logic trừ máu/khiên/i-frames (thuộc trách nhiệm của PlayerDamageResolutionSystem).
/// </summary>
[BurstCompile]
public partial struct EnemyProjectileHitPlayerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, dmg, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<DamageOnContactData>>()
                     .WithAll<EnemyProjectileTag>()
                     .WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float hitDistSq = dmg.ValueRO.HitRadius * dmg.ValueRO.HitRadius;
            if (distSq > hitDistSq) continue;

            // Đạn chạm người chơi luôn bị tiêu hủy (hấp thụ bởi cơ thể / khiên)
            ecb.DestroyEntity(entity);

            // Gửi gói sát thương vào IncomingDamage buffer của Player
            damageBuffer.Add(new IncomingDamage
            {
                Amount = dmg.ValueRO.Damage,
                SourceType = DamageSourceType.Projectile,
                SourcePosition = transform.ValueRO.Position
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}