using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct EnemyProjectileHitPlayerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        HealthData playerHealth = SystemAPI.GetComponent<HealthData>(playerEntity);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float totalDamage = 0f;

        // Kiểm tra xem người chơi có đang trong trạng thái i-frames không
        bool isInvulnerable = false;
        if (SystemAPI.HasComponent<PlayerInvulnerabilityData>(playerEntity))
        {
            var invuln = SystemAPI.GetComponent<PlayerInvulnerabilityData>(playerEntity);
            if (invuln.CurrentTimer > 0f)
            {
                isInvulnerable = true;
            }
        }

        foreach (var (transform, dmg, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<DamageOnContactData>>()
                     .WithAll<EnemyProjectileTag>()
                     .WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float hitDistSq = dmg.ValueRO.HitRadius * dmg.ValueRO.HitRadius;
            if (distSq > hitDistSq) continue;

            // Đạn chạm người chơi luôn bị tiêu hủy (hấp thụ bởi lớp bảo vệ)
            ecb.DestroyEntity(entity);

            // Chỉ nhận sát thương nếu không trong trạng thái i-frames
            if (!isInvulnerable)
            {
                totalDamage += dmg.ValueRO.Damage;
            }
        }

        if (totalDamage > 0f)
        {
            playerHealth.CurrentHP -= totalDamage;
            SystemAPI.SetComponent(playerEntity, playerHealth);

            // Kích hoạt thời gian miễn sát thương (i-frames)
            if (SystemAPI.HasComponent<PlayerInvulnerabilityData>(playerEntity))
            {
                var invuln = SystemAPI.GetComponent<PlayerInvulnerabilityData>(playerEntity);
                invuln.CurrentTimer = invuln.Duration;
                SystemAPI.SetComponent(playerEntity, invuln);
            }

            Debug.Log($"[EnemyProjectileHitPlayerSystem] Player took {totalDamage} damage from Enemy projectile (Remaining HP: {playerHealth.CurrentHP}/{playerHealth.MaxHP}) - i-frames activated!");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}