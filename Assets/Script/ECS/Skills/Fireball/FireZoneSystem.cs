using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý Vùng Lửa Thiêu Đốt (Fire Zone / Scorched Earth) sinh ra ở Cấp độ 4.
/// Gây sát thương DOT định kỳ (mỗi 0.25s) lên toàn bộ quái vật dẫm vào vùng lửa.
/// </summary>
[UpdateAfter(typeof(SpatialGridBuildSystem))]
[BurstCompile]
public partial struct FireZoneSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<SpatialGridSingleton>()) return;

        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, zone, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<FireZoneData>>()
                     .WithAll<FireZoneTag>()
                     .WithEntityAccess())
        {
            // 1. Đếm lùi thời gian duy trì của vùng lửa
            zone.ValueRW.DurationRemaining -= dt;
            if (zone.ValueRO.DurationRemaining <= 0f)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            // 2. Đếm ngược nhịp gây sát thương DOT (TickInterval = 0.25s)
            zone.ValueRW.TickTimer -= dt;
            if (zone.ValueRO.TickTimer > 0f) continue;

            // Reset bộ đếm nhịp DOT
            zone.ValueRW.TickTimer = zone.ValueRO.TickInterval;

            // 3. Quét Spatial Grid tìm toàn bộ quái trong bán kính vùng lửa
            float2 center = zone.ValueRO.Center;
            float radius = zone.ValueRO.Radius;
            float radiusSq = radius * radius;
            float damage = zone.ValueRO.DamagePerTick;

            int2 centerCell = SpatialGridUtility.ToCell(center, cellSize);
            int cellRadius = (int)math.ceil(radius / cellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                int hash = SpatialGridUtility.HashCell(centerCell + new int2(dx, dy));
                if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                do
                {
                    if (!state.EntityManager.Exists(enemy) || !state.EntityManager.HasComponent<HealthData>(enemy))
                        continue;

                    float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                    if (math.distancesq(center, enemyPos.xy) <= radiusSq)
                    {
                        HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                        hp.CurrentHP -= damage;
                        ecb.SetComponent(enemy, hp);
                    }
                } while (grid.TryGetNextValue(out enemy, ref it));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
