using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct PlayerInputToMovementSystem : ISystem
{
    private EntityQuery _inputQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _inputQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerInputSingleton>());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_inputQuery.IsEmpty) return;

        // Truy vấn an toàn: lấy phần tử đầu tiên tránh HasSingleton crash nếu có frame chuyển cảnh tồn tại duplicate
        var inputArray = _inputQuery.ToComponentDataArray<PlayerInputSingleton>(Allocator.Temp);
        var input = inputArray[0];
        inputArray.Dispose();

        foreach (var movement in
                 SystemAPI.Query<RefRW<MovementData>>().WithAll<PlayerTag>())
        {
            movement.ValueRW.Direction = input.MoveInput;
        }
    }
}
