
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct BomberExplodeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        HealthData playerHealth = SystemAPI.GetComponent<HealthData>(playerEntity);
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

        foreach (var (transform, explode, health) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<BomberExplodeData>, RefRW<HealthData>>()
                     .WithAll<BomberTag>())
        {
            if (health.ValueRO.CurrentHP <= 0f)
            { 
                Debug.Log("[BomberExplodeSystem] Bomber died BEFORE detonating.");
                continue; // đã chết, để DeathCleanupSystem xử lý
            }
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = explode.ValueRO.ExplodeRadius * explode.ValueRO.ExplodeRadius;

            if (distSq > radiusSq) continue;

            Debug.Log($"[BomberExplodeSystem] BOOM! Bomber detonated near Player. (Invulnerable: {isInvulnerable})");
            if (!isInvulnerable)
            {
                totalDamage += explode.ValueRO.ExplodeDamage;
            }
            health.ValueRW.CurrentHP = 0f; // "tự chết" — DeathCleanupSystem lo phần rớt EXP/loot/destroy
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

            Debug.Log($"[BomberExplodeSystem] Player took {totalDamage} damage from Bomber explosion (Remaining HP: {playerHealth.CurrentHP}/{playerHealth.MaxHP}) - i-frames activated!");
        }
    }
}