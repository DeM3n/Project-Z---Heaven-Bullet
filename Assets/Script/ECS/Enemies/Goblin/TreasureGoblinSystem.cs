using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều khiển hành vi hoảng loạn chạy trốn của Treasure Goblin:
/// - Chạy ngược hướng người chơi với tốc độ cao.
/// - Lượn lách (Zigzag) để né tránh đòn đánh.
/// - Đếm ngược thời gian: Nếu quá thời hạn mà người chơi chưa tiêu diệt, Goblin sẽ tẩu thoát biến mất.
/// </summary>
[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct TreasureGoblinSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, movement, goblin, health, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<MovementData>, RefRW<TreasureGoblinData>, RefRO<HealthData>>()
                     .WithAll<TreasureGoblinTag>()
                     .WithEntityAccess())
        {
            // 1. Kiểm tra phát hiện Player
            float2 awayDir = (transform.ValueRO.Position - playerPos).xy;
            float dist = math.length(awayDir);

            if (!goblin.ValueRO.HasDetectedPlayer)
            {
                // Bị đánh thức nếu: Player tiến vào cự ly phát hiện (<= DetectionRange, mặc định 7.5m)
                // HOẶC bị tấn công từ xa (CurrentHP < MaxHP)
                if (dist <= goblin.ValueRO.DetectionRange || health.ValueRO.CurrentHP < health.ValueRO.MaxHP)
                {
                    goblin.ValueRW.HasDetectedPlayer = true;
                }
                else
                {
                    // Khi chưa phát hiện: Đứng yên đào bới kho báu, không di chuyển và CHƯA đếm ngược tẩu thoát
                    movement.ValueRW.Direction = float2.zero;
                    movement.ValueRW.MoveSpeed = 0f;
                    continue;
                }
            }

            // 2. Một khi đã phát hiện Player: Bắt đầu đếm ngược thời gian tẩu thoát (22s)
            goblin.ValueRW.EscapeTimer -= dt;
            if (goblin.ValueRO.EscapeTimer <= 0f)
            {
                // Goblin đào hang tẩu thoát thành công -> biến mất không để lại loot
                ecb.DestroyEntity(entity);
                continue;
            }

            // 3. Chạy trốn khỏi Player (Vector ngược hướng)
            float2 normAway = (dist > 0.001f) ? (awayDir / dist) : new float2(1f, 0f);

            // 4. Hiệu ứng chạy hoảng loạn (Zigzag / Panicked wiggling)
            goblin.ValueRW.ZigzagTimer -= dt;
            if (goblin.ValueRO.ZigzagTimer <= 0f)
            {
                goblin.ValueRW.ZigzagTimer = 0.3f;
                // Đổi góc zigzag ngẫu nhiên ±30 độ dựa vào index và thời gian
                goblin.ValueRW.ZigzagAngle = ((entity.Index * 37) % 60 - 30) * (math.PI / 180f);
            }

            float cosA = math.cos(goblin.ValueRO.ZigzagAngle);
            float sinA = math.sin(goblin.ValueRO.ZigzagAngle);
            float2 finalDir = new float2(
                normAway.x * cosA - normAway.y * sinA,
                normAway.x * sinA + normAway.y * cosA
            );

            movement.ValueRW.Direction = math.normalizesafe(finalDir);
            movement.ValueRW.MoveSpeed = goblin.ValueRO.FleeSpeed;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
