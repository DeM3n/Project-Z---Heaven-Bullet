using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý va chạm và kích hoạt hiệu ứng của các vật phẩm rơi (Pickups):
/// - HealthHeart: Hồi máu trực tiếp cho người chơi.
/// - ExpMagnet: Hút toàn bộ ngọc EXP trên toàn bản đồ về phía người chơi.
/// - TreasureChest: Mở Rương Cổ Vật (tạo ChestOpenEvent cho GameHUDBridge hiển thị Popup).
/// </summary>
[UpdateAfter(typeof(ApplyMovementSystem))]
public partial struct PickupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float pickupRange = 2f;
        if (SystemAPI.HasComponent<PickupRangeData>(playerEntity))
        {
            pickupRange = SystemAPI.GetComponent<PickupRangeData>(playerEntity).Range;
        }
        float pickupRangeSq = pickupRange * pickupRange;
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool magnetTriggered = false;

        foreach (var (transform, pickup, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PickupData>>()
                     .WithAll<PickupItemTag>()
                     .WithEntityAccess())
        {
            float3 toPlayer = playerPos - transform.ValueRO.Position;
            toPlayer.z = 0f;
            float distSq = math.lengthsq(toPlayer);

            // Rương Cổ Vật: Người chơi phải chạm trực tiếp (cự ly <= 1.0m), không tự bay
            if (pickup.ValueRO.Type == PickupType.TreasureChest)
            {
                if (distSq <= 1.0f * 1.0f)
                {
                    Entity chestEvent = ecb.CreateEntity();
                    ecb.AddComponent(chestEvent, new ChestOpenEvent { PlayerEntity = playerEntity });
                    ecb.DestroyEntity(entity);
                }
                continue;
            }

            // Đối với Trái Tim & Nam Châm: bị hút khi vào tầm nhặt (PickupRange)
            if (pickup.ValueRO.FlySpeed <= 0f)
            {
                if (distSq <= pickupRangeSq)
                {
                    pickup.ValueRW.FlySpeed = 7f; // Bắt đầu tăng tốc bay
                }
                else
                {
                    continue;
                }
            }

            // Tăng tốc dần về phía Player (tối đa 30 m/s)
            pickup.ValueRW.FlySpeed = math.min(30f, pickup.ValueRO.FlySpeed + 24f * dt);
            float dist = math.sqrt(distSq);
            float step = pickup.ValueRO.FlySpeed * dt;

            // Kiểm tra va chạm hấp thụ
            if (dist <= 0.5f || dist <= step)
            {
                if (pickup.ValueRO.Type == PickupType.HealthHeart)
                {
                    if (SystemAPI.HasComponent<HealthData>(playerEntity))
                    {
                        var hp = SystemAPI.GetComponent<HealthData>(playerEntity);
                        hp.CurrentHP = math.min(hp.MaxHP, hp.CurrentHP + pickup.ValueRO.Value);
                        SystemAPI.SetComponent(playerEntity, hp);
                    }
                }
                else if (pickup.ValueRO.Type == PickupType.ExpMagnet)
                {
                    magnetTriggered = true;
                }

                ecb.DestroyEntity(entity);
            }
            else
            {
                float3 dir = toPlayer / math.max(0.0001f, dist);
                transform.ValueRW.Position += dir * step;
            }
        }

        // KHI NAM CHÂM ĐƯỢC KÍCH HOẠT:
        // Đánh thức TOÀN BỘ ExpOrb trên bản đồ bay thẳng về người chơi với tốc độ cao!
        if (magnetTriggered)
        {
            foreach (var orb in SystemAPI.Query<RefRW<ExpOrbData>>().WithAll<ExpOrbTag>())
            {
                if (orb.ValueRO.FlySpeed <= 0f)
                {
                    orb.ValueRW.FlySpeed = 8f; // Kích hoạt bay ngay lập tức
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
