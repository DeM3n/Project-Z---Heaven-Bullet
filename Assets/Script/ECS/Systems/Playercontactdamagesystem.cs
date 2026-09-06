using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
[BurstCompile]
public partial struct PlayerContactDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        HealthData playerHealth = SystemAPI.GetComponent<HealthData>(playerEntity);
        float dt = SystemAPI.Time.DeltaTime;
        float totalDamage = 0f;

        // Cộng dồn damage từ mọi enemy đang chạm rồi apply 1 lần cuối,
        // tránh nhiều enemy cùng ghi đè HealthData của Player trong 1 frame.
        foreach (var (transform, contact) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyContactDamageData>>()
                     .WithAll<EnemyTag>())
        {
            contact.ValueRW.Timer -= dt;

            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = contact.ValueRO.ContactRadius * contact.ValueRO.ContactRadius;
            if (distSq > radiusSq) continue;
            if (contact.ValueRO.Timer > 0f) continue;

            contact.ValueRW.Timer = contact.ValueRO.TickInterval;
            totalDamage += contact.ValueRO.Damage;
        }

        if (totalDamage > 0f)
        {
            playerHealth.CurrentHP -= totalDamage;
            SystemAPI.SetComponent(playerEntity, playerHealth);
            Debug.Log($"[PlayerContactDamageSystem] Player bị trừ {totalDamage} HP do va chạm Enemy (còn lại {playerHealth.CurrentHP}/{playerHealth.MaxHP})");
        }
    }
}