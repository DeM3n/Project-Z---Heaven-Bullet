using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct PlayerDeadTag : IComponentData {}

public partial struct PlayerDeathCheckSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var revivalLookup = SystemAPI.GetComponentLookup<PlayerRevivalData>(false);

        foreach (var (health, entity) in
                 SystemAPI.Query<RefRW<HealthData>>()
                     .WithAll<PlayerTag>()
                     .WithNone<PlayerDeadTag>()
                     .WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP > 0f) continue;

            // Kiểm tra khả năng Hồi Sinh từ Đạo Quả Cây Thiên Phú
            if (revivalLookup.HasComponent(entity))
            {
                var revival = revivalLookup[entity];
                if (revival.CanRevive && !revival.HasRevived)
                {
                    revival.HasRevived = true;
                    revivalLookup[entity] = revival;
                    health.ValueRW.CurrentHP = health.ValueRO.MaxHP * 0.5f;
                    Debug.Log("[PlayerDeathCheckSystem] PHOENIX REBIRTH! Player revived with 50% HP!");
                    continue; // Hồi sinh thành công, không gắn PlayerDeadTag!
                }
            }

            ecb.AddComponent<PlayerDeadTag>(entity); // đánh dấu đã chết
            Debug.Log("[PlayerDeathCheckSystem] GAME OVER - Player has died.");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
