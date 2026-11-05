using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WithNone(typeof(ShooterTag), typeof(TreasureGoblinTag), typeof(EnemyStunData), typeof(ChargerTag))]
[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct EnemyChaseSystem : ISystem
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

        state.Dependency = new SmartEnemyChaseJob
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
[WithNone(typeof(ShooterTag), typeof(TreasureGoblinTag), typeof(EnemyStunData), typeof(ChargerTag))]
public partial struct SmartEnemyChaseJob : IJobEntity
{
    public float3 PlayerPosition;
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Grid;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    public float CellSize;
    public float DeltaTime;

    void Execute(
        Entity entity,
        ref MovementData movement,
        ref EnemyFlockingData flocking,
        in LocalTransform transform,
        in EnemyTag tag)
    {
        float2 myPos = transform.Position.xy;
        float2 toPlayer = (PlayerPosition - transform.Position).xy;
        float distToPlayer = math.length(toPlayer);
        float2 dirToPlayer = distToPlayer > 0.001f ? (toPlayer / distToPlayer) : float2.zero;

        // 1. Tính lực Tách Đàn (Separation) từ SpatialGrid chống quái đè chồng lên nhau
        float2 separationForce = float2.zero;
        float sepRadius = flocking.SeparationRadius;
        float sepRadiusSq = sepRadius * sepRadius;

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
                    if (distSq < sepRadiusSq && distSq > 0.0001f)
                    {
                        float dist = math.sqrt(distSq);
                        float strength = 1.0f - (dist / sepRadius);
                        separationForce += (diff / dist) * strength;
                    }
                } while (Grid.TryGetNextValue(out otherEntity, ref it));
            }
        }

        // 2. Tính lực Bao Vây & Bọc Sườn (Flanking & Encirclement) khi ở gần Player (< 6.0m)
        float2 flankForce = float2.zero;
        if (distToPlayer < 6.0f)
        {
            // Phân cánh trái/phải dựa theo Entity.Index bit 0 (50% cánh trái, 50% cánh phải)
            float flankSign = (entity.Index & 1) == 0 ? 1f : -1f;
            float2 tangent = new float2(-dirToPlayer.y, dirToPlayer.x) * flankSign;
            float flankBlend = math.clamp((6.0f - distToPlayer) / 3.0f, 0f, 1f);
            flankForce = tangent * flankBlend * flocking.TangentialWeight;
        }

        // 3. Tổng hợp vector chuyển động bầy quái cơ bản (chase + flank + separation)
        float2 finalDir = dirToPlayer * 0.70f + flankForce * 0.30f + separationForce * flocking.SeparationWeight;
        movement.Direction = math.normalizesafe(finalDir);
    }
}
