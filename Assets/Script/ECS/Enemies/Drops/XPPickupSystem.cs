using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(DeathCleanupSystem))]
public partial struct XPPickupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float pickupRange = SystemAPI.GetComponent<PickupRangeData>(playerEntity).Range;
        float pickupRangeSq = pickupRange * pickupRange;
        float dt = SystemAPI.Time.DeltaTime;

        var playerLevel = SystemAPI.GetComponent<PlayerLevelData>(playerEntity);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool expChanged = false;

        foreach (var (transform, orb, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<ExpOrbData>>()
                     .WithAll<ExpOrbTag>()
                     .WithEntityAccess())
        {
            float3 toPlayer = playerPos - transform.ValueRO.Position;
            toPlayer.z = 0f;
            float distSq = math.lengthsq(toPlayer);

            // Nếu ngọc chưa bị hút và nằm ngoài tầm nhặt -> bỏ qua
            if (orb.ValueRO.FlySpeed <= 0f)
            {
                if (distSq <= pickupRangeSq)
                {
                    // Kích hoạt hút: vận tốc bay khởi đầu (7 m/s)
                    orb.ValueRW.FlySpeed = 7f;
                }
                else
                {
                    continue;
                }
            }

            // Ngọc đang bị hút: tăng tốc dần về phía Player (gia tốc 26 m/s^2, tối đa 32 m/s)
            orb.ValueRW.FlySpeed = math.min(32f, orb.ValueRO.FlySpeed + 26f * dt);

            float dist = math.sqrt(distSq);
            float step = orb.ValueRO.FlySpeed * dt;

            // Kiểm tra va chạm hấp thụ: khi ngọc tiếp cận sát tâm người chơi (<= 0.4m) hoặc bước di chuyển frame này chạm tới Player
            if (dist <= 0.4f || dist <= step)
            {
                playerLevel.CurrentExp += orb.ValueRO.ExpValue;
                expChanged = true;
                ecb.DestroyEntity(entity);
            }
            else
            {
                // Bay mượt mà hướng thẳng vào Player
                float3 dir = toPlayer / math.max(0.0001f, dist);
                transform.ValueRW.Position += dir * step;
            }
        }

        if (expChanged) SystemAPI.SetComponent(playerEntity, playerLevel);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}