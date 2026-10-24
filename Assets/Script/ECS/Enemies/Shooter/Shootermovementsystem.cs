using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct ShooterMovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        state.Dependency = new ShooterMovementJob
        {
            PlayerPosition = playerPos
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(ShooterTag))]
public partial struct ShooterMovementJob : IJobEntity
{
    public float3 PlayerPosition;
    private const float Tolerance = 0.5f;

    void Execute(in LocalTransform transform, ref MovementData movement, in ShooterAttackData attack)
    {
        float2 toPlayer = (PlayerPosition - transform.Position).xy;
        float dist = math.length(toPlayer);

        if (dist > attack.PreferredRange + Tolerance)
        {
            movement.Direction = math.normalizesafe(toPlayer);
        }
        else if (dist < attack.PreferredRange - Tolerance)
        {
            movement.Direction = -math.normalizesafe(toPlayer);
        }
        else
        {
            movement.Direction = float2.zero;
        }
    }
}