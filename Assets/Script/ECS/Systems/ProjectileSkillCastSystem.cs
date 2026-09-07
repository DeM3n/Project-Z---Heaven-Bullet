using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct ProjectileSkillCastSystem : ISystem
{
    private EntityQuery _enemyQuery;

    public void OnCreate(ref SystemState state)
    {
        _enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;

        foreach (var (runtime, owner, skill) in
                 SystemAPI.Query<RefRW<SkillRuntimeData>, RefRO<SkillOwnerData>, RefRO<ProjectileSkillData>>()
                     .WithAll<ProjectileSkillTag>())
        {
            runtime.ValueRW.Timer -= dt;
            if (runtime.ValueRO.Timer > 0f) continue;
            runtime.ValueRW.Timer = runtime.ValueRO.Cooldown;

            if (!state.EntityManager.Exists(owner.ValueRO.Owner)) continue;
            float3 casterPos = state.EntityManager.GetComponentData<LocalTransform>(owner.ValueRO.Owner).Position;

            if (!TryFindNearestEnemy(ref state, grid, cellSize, casterPos, out float3 targetPos))
                continue; // hết cooldown nhưng chưa có mục tiêu → bỏ lượt bắn này

            float2 baseDir = math.normalizesafe((targetPos - casterPos).xy);
            int count = math.max(1, skill.ValueRO.ProjectileCount);
            float totalSpread = math.radians(skill.ValueRO.SpreadAngleDegrees);
            float startAngle = -totalSpread * 0.5f;
            float angleStep = count > 1 ? totalSpread / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float2 dir = RotateVector(baseDir, startAngle + angleStep * i);

                Entity projectile = ecb.Instantiate(skill.ValueRO.ProjectilePrefab);
                ecb.SetComponent(projectile, LocalTransform.FromPosition(casterPos));
                ecb.SetComponent(projectile, new MovementData { Direction = dir, MoveSpeed = skill.ValueRO.ProjectileSpeed });
                ecb.SetComponent(projectile, new ProjectileLifetimeData { TimeRemaining = skill.ValueRO.ProjectileLifetime });
                ecb.SetComponent(projectile, new DamageOnContactData { Damage = skill.ValueRO.ProjectileDamage, HitRadius = skill.ValueRO.ProjectileHitRadius });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private bool TryFindNearestEnemy(ref SystemState state, NativeParallelMultiHashMap<int, Entity> grid, float cellSize, float3 casterPos, out float3 targetPos)
    {
        int2 casterCell = SpatialGridUtility.ToCell(casterPos.xy, cellSize);
        float closestDistSq = float.MaxValue;
        targetPos = casterPos;
        bool found = false;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int hash = SpatialGridUtility.HashCell(casterCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy)) continue;
                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                float distSq = math.distancesq(casterPos, enemyPos);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    targetPos = enemyPos;
                    found = true;
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        if (found) return true;

        // Fallback: 3x3 ô quanh caster trống (map thưa enemy) → quét toàn bộ 1 lần cho chắc chắn có mục tiêu
        if (!_enemyQuery.IsEmpty)
        {
            var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
            {
                float distSq = math.distancesq(casterPos, transforms[i].Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    targetPos = transforms[i].Position;
                    found = true;
                }
            }
            transforms.Dispose();
        }

        return found;
    }

    private static float2 RotateVector(float2 v, float radians)
    {
        float cos = math.cos(radians);
        float sin = math.sin(radians);
        return new float2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}