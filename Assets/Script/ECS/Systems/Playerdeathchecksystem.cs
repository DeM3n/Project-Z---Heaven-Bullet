using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct PlayerDeadTag : IComponentData {}

[BurstCompile]
public partial struct PlayerDeathCheckSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (health, entity) in
                 SystemAPI.Query<RefRO<HealthData>>()
                     .WithAll<PlayerTag>()
                     .WithNone<PlayerDeadTag>()
                     .WithEntityAccess())
        {
            if (health.ValueRO.CurrentHP > 0f) continue;

            ecb.AddComponent<PlayerDeadTag>(entity); // đánh dấu đã chết, tránh log lặp lại mỗi frame
            Debug.Log("[PlayerDeathCheckSystem] GAME OVER - Player đã chết.");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}