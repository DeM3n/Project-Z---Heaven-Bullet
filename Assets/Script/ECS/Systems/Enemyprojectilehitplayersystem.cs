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

        foreach (var (transform, dmg, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<DamageOnContactData>>()
                     .WithAll<EnemyProjectileTag>()
                     .WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float hitDistSq = dmg.ValueRO.HitRadius * dmg.ValueRO.HitRadius;
            if (distSq > hitDistSq) continue;

            totalDamage += dmg.ValueRO.Damage;
            ecb.DestroyEntity(entity);
        }

        if (totalDamage > 0f)
        {
            playerHealth.CurrentHP -= totalDamage;
            SystemAPI.SetComponent(playerEntity, playerHealth);
            Debug.Log($"[EnemyProjectileHitPlayerSystem] Player bị trừ {totalDamage} HP do trúng đạn Enemy (còn lại {playerHealth.CurrentHP}/{playerHealth.MaxHP})");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}