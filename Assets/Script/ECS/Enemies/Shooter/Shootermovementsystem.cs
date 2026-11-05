using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct ShooterMovementSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();
        state.RequireForUpdate<PlayerTag>();
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        _transformLookup.Update(ref state);
        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        float dt = SystemAPI.Time.DeltaTime;

        state.Dependency = new SmartShooterMovementJob
        {
            PlayerPosition = playerPos,
            Grid = grid,
            TransformLookup = _transformLookup,
            CellSize = cellSize,
            DeltaTime = dt
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(ShooterTag))]
[WithNone(typeof(EnemyStunData))]
public partial struct SmartShooterMovementJob : IJobEntity
{
    public float3 PlayerPosition;
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Grid;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    public float CellSize;
    public float DeltaTime;

    private const float RangeTolerance = 0.8f;
    private const float SeparationRadius = 1.2f;

    void Execute(
        Entity entity,
        in LocalTransform transform,
        ref MovementData movement,
        in ShooterAttackData attack,
        ref ShooterTacticsData tactics)
    {
        float2 myPos = transform.Position.xy;
        float2 toPlayer = (PlayerPosition - transform.Position).xy;
        float dist = math.length(toPlayer);

        // 1. Tính lực tách xạ thủ (Separation) từ SpatialGrid để không đứng chen chúc
        float2 separationForce = float2.zero;
        int2 myCell = SpatialGridUtility.ToCell(myPos, CellSize);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int hash = SpatialGridUtility.HashCell(myCell + new int2(dx, dy));
                if (!Grid.TryGetFirstValue(hash, out Entity otherEntity, out var it)) continue;

                do
                {
                    if (otherEntity == entity) continue;
                    if (!TransformLookup.HasComponent(otherEntity)) continue;

                    float2 otherPos = TransformLookup[otherEntity].Position.xy;
                    float2 diff = myPos - otherPos;
                    float distSq = math.lengthsq(diff);
                    if (distSq < SeparationRadius * SeparationRadius && distSq > 0.0001f)
                    {
                        float d = math.sqrt(distSq);
                        float strength = 1.0f - (d / SeparationRadius);
                        separationForce += (diff / d) * strength;
                    }
                } while (Grid.TryGetNextValue(out otherEntity, ref it));
            }
        }

        // 2. Cập nhật đổi hướng lượn vòng định kỳ
        tactics.StrafeTimer -= DeltaTime;
        if (tactics.StrafeTimer <= 0f)
        {
            tactics.StrafeDirection = -tactics.StrafeDirection;
            tactics.StrafeTimer = 3.5f;
        }

        // 3. Điều khiển di chuyển chiến thuật
        if (dist > attack.PreferredRange + RangeTolerance)
        {
            // Ngoài tầm bắn -> Tiến lại gần Player
            float2 dir = math.normalizesafe(toPlayer);
            movement.Direction = math.normalizesafe(dir + separationForce * 0.4f);
        }
        else if (dist < attack.PreferredRange - RangeTolerance)
        {
            // Bị Player áp sát -> Thả diều lùi lại (Kiting) kết hợp né ngang
            float2 away = -math.normalizesafe(toPlayer);
            float2 sidestep = new float2(-away.y, away.x) * tactics.StrafeDirection * 0.4f;
            movement.Direction = math.normalizesafe(away + sidestep + separationForce * 0.5f);
        }
        else
        {
            // Ở trong cự ly lý tưởng -> Lượn vòng (Circle-Strafe) quanh Player
            float2 dirToPlayer = math.normalizesafe(toPlayer);
            float2 circleStrafe = new float2(-dirToPlayer.y, dirToPlayer.x) * tactics.StrafeDirection;
            float2 combined = circleStrafe + separationForce * 0.6f;
            movement.Direction = math.normalizesafe(combined) * tactics.CircleStrafeSpeed;
        }
    }
}
