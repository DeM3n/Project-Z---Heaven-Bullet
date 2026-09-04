using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct PlayerInputToMovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<PlayerInputSingleton>()) return;
        var input = SystemAPI.GetSingleton<PlayerInputSingleton>();

        foreach (var movement in
                 SystemAPI.Query<RefRW<MovementData>>().WithAll<PlayerTag>())
        {
            movement.ValueRW.Direction = input.MoveInput;
        }
    }
}