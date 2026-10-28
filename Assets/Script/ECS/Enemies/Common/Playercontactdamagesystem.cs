using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống kiểm tra va chạm cận chiến giữa quái vật và người chơi.
/// Khi quái chạm Player và đủ nhịp cắn (TickInterval), đẩy gói sát thương vào IncomingDamage buffer trên Player.
/// Không xử lý logic trừ máu/khiên/i-frames (thuộc trách nhiệm của PlayerDamageResolutionSystem).
/// </summary>
[BurstCompile]
public partial struct PlayerContactDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, contact) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyContactDamageData>>()
                     .WithAll<EnemyTag>()
                     .WithNone<TreasureGoblinTag>())
        {
            contact.ValueRW.Timer -= dt;

            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = contact.ValueRO.ContactRadius * contact.ValueRO.ContactRadius;
            if (distSq > radiusSq) continue;
            if (contact.ValueRO.Timer > 0f) continue;

            // Reset nhịp gây sát thương của quái
            contact.ValueRW.Timer = contact.ValueRO.TickInterval;

            // Đẩy cú đánh vào IncomingDamage buffer của Player
            damageBuffer.Add(new IncomingDamage
            {
                Amount = contact.ValueRO.Damage,
                SourceType = DamageSourceType.Contact,
                SourcePosition = transform.ValueRO.Position
            });
        }
    }
}