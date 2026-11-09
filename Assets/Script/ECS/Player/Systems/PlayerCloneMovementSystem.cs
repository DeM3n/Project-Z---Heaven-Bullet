using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều phối chuyển động của các Phân Thân Ảo Ảnh (Player Clones):
/// - Cố định vị trí 2 bên sườn Player (Option A: -1.6m bên trái và +1.6m bên phải).
/// - Bám theo vị trí người chơi mượt mà với độ phản hồi cao (Lerp damping).
/// - Đồng bộ hướng quay mặt (Facing Direction) theo hướng di chuyển của người chơi.
/// </summary>
[UpdateAfter(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct PlayerCloneMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        float2 moveInput = float2.zero;
        if (SystemAPI.TryGetSingleton<PlayerInputSingleton>(out var input))
        {
            moveInput = input.MoveInput;
        }

        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, cloneData) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PlayerCloneData>>()
                     .WithAll<PlayerCloneTag>())
        {
            // Cập nhật hướng quay mặt
            if (math.abs(moveInput.x) > 0.05f)
            {
                cloneData.ValueRW.FacingDirection = math.sign(moveInput.x);
            }

            // Vị trí mục tiêu: Player + BaseOffset
            float3 targetPos = playerPos + cloneData.ValueRO.BaseOffset;

            // Di chuyển bám theo mượt mà
            float followSpeed = cloneData.ValueRO.FollowSpeed > 0f ? cloneData.ValueRO.FollowSpeed : 22f;
            float t = math.clamp(dt * followSpeed, 0f, 1f);
            float3 newPos = math.lerp(transform.ValueRO.Position, targetPos, t);

            transform.ValueRW.Position = newPos;
        }
    }
}
