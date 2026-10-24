using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
[WithNone(typeof(ShooterTag))]
[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct EnemyChaseSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        new EnemyChaseJob { PlayerPosition = playerPos }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct EnemyChaseJob : IJobEntity
{
    public float3 PlayerPosition;

    void Execute(ref MovementData movement, in LocalTransform transform, in EnemyTag tag)
    {
        float3 dir = PlayerPosition - transform.Position;
        movement.Direction = math.normalizesafe(dir.xy);
    }
}