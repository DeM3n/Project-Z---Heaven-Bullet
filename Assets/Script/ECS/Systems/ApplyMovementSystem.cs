using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ApplyMovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, movement) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<MovementData>>())
        {
            float3 dir = new float3(movement.ValueRO.Direction.x, movement.ValueRO.Direction.y, 0);
            transform.ValueRW.Position += dir * movement.ValueRO.MoveSpeed * dt;
        }
    }
}