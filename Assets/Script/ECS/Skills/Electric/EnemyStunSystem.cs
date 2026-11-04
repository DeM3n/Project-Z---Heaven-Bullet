using Unity.Burst;
using Unity.Entities;

/// <summary>
/// Hệ thống xử lý thời gian Choáng (Stun Duration Countdown) cho toàn bộ quái vật.
/// Chạy trên nền C# Jobs + Burst Compiler với Enableable Component:
/// - Chỉ quét đúng những quái đang mang EnemyStunData ở trạng thái BẬT.
/// - Đếm lùi thời gian StunTimer theo deltaTime.
/// - Khi hết giờ: Tắt component qua SetComponentEnabled(entity, false), ZERO Structural Changes!
/// </summary>
[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct EnemyStunSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        // Query mặc định trong Unity Entities chỉ duyệt qua các Entity có EnemyStunData đang ENABLED
        foreach (var (stun, entity) in
                 SystemAPI.Query<RefRW<EnemyStunData>>()
                     .WithEntityAccess())
        {
            stun.ValueRW.StunTimer -= dt;
            if (stun.ValueRO.StunTimer <= 0f)
            {
                // Hết thời gian choáng -> Tắt bitmask, quái trở lại hoạt động bình thường
                SystemAPI.SetComponentEnabled<EnemyStunData>(entity, false);
            }
        }
    }
}

