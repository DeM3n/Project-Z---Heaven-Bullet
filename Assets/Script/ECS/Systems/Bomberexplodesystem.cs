
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

        foreach (var (transform, explode, health) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<BomberExplodeData>, RefRW<HealthData>>()
                     .WithAll<BomberTag>())
        {
            if (health.ValueRO.CurrentHP <= 0f)
            { 
                Debug.Log("[BomberExplodeSystem] Bomber đã chết TRƯỚC khi kịp nổ (có thể bị Player bắn chết trước đó).");
                continue; // đã chết, để DeathCleanupSystem xử lý
            }
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = explode.ValueRO.ExplodeRadius * explode.ValueRO.ExplodeRadius;
            

            if (distSq > radiusSq) continue;
            Debug.Log($"[BomberExplodeSystem] NỔ! Gây {explode.ValueRO.ExplodeDamage} damage lên Player.");
            totalDamage += explode.ValueRO.ExplodeDamage;
            health.ValueRW.CurrentHP = 0f; // "tự chết" — DeathCleanupSystem lo phần rớt EXP/loot/destroy
        }

        if (totalDamage > 0f)
        {
            playerHealth.CurrentHP -= totalDamage;
            SystemAPI.SetComponent(playerEntity, playerHealth);
        }
    }
}