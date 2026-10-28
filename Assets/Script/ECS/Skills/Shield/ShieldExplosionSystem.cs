using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý vụ nổ phản pháo (Shatter Nova) khi khiên bị vỡ ở Cấp 4.
/// Gây sát thương nổ AOE, hất văng quái vật cận chiến (Knockback) và tiêu hủy 100% đạn địch trong phạm vi.
/// </summary>
[UpdateAfter(typeof(PlayerContactDamageSystem))]
[UpdateBefore(typeof(DeathCleanupSystem))]
public partial struct ShieldExplosionSystem : ISystem
{
    private EntityQuery _breakEventQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();

        _breakEventQuery = SystemAPI.QueryBuilder()
            .WithAll<ShieldBreakEvent>()
            .WithNone<ShieldExplosionProcessedTag>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_breakEventQuery.IsEmpty) return;

        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (breakEvent, eventEntity) in
                 SystemAPI.Query<RefRO<ShieldBreakEvent>>()
                     .WithNone<ShieldExplosionProcessedTag>()
                     .WithEntityAccess())
        {
            // Đánh dấu event đã được xử lý chiến đấu
            ecb.AddComponent<ShieldExplosionProcessedTag>(eventEntity);

            // Nếu không phải vụ nổ Cấp 4, bỏ qua logic sát thương (chỉ dành cho visual bridge)
            if (!breakEvent.ValueRO.IsExplosion) continue;

            float3 centerPos = breakEvent.ValueRO.Position;
            float radius = breakEvent.ValueRO.Radius;
            float radiusSq = radius * radius;
            float damage = breakEvent.ValueRO.Damage;
            float knockback = breakEvent.ValueRO.KnockbackForce;

            int2 centerCell = SpatialGridUtility.ToCell(centerPos.xy, cellSize);
            int cellRadius = (int)math.ceil(radius / cellSize);

            // 1. Quét lưới Spatial Grid gây sát thương và đẩy lùi quái vật
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                int hash = SpatialGridUtility.HashCell(centerCell + new int2(dx, dy));
                if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                do
                {
                    if (!state.EntityManager.Exists(enemy)) continue;
                    if (!state.EntityManager.HasComponent<LocalTransform>(enemy) ||
                        !state.EntityManager.HasComponent<HealthData>(enemy)) continue;

                    LocalTransform enemyTransform = state.EntityManager.GetComponentData<LocalTransform>(enemy);
                    float distSq = math.distancesq(centerPos, enemyTransform.Position);

                    if (distSq <= radiusSq)
                    {
                        // Gây sát thương
                        HealthData enemyHealth = state.EntityManager.GetComponentData<HealthData>(enemy);
                        enemyHealth.CurrentHP -= damage;
                        ecb.SetComponent(enemy, enemyHealth);

                        // Đẩy lùi (Knockback) hất văng quái ra khỏi tâm vụ nổ
                        float2 dir = enemyTransform.Position.xy - centerPos.xy;
                        float dist = math.length(dir);
                        if (dist > 0.001f)
                        {
                            dir /= dist;
                        }
                        else
                        {
                            dir = new float2(1f, 0f);
                        }

                        enemyTransform.Position.xy += dir * knockback;
                        ecb.SetComponent(enemy, enemyTransform);
                    }
                } while (grid.TryGetNextValue(out enemy, ref it));
            }

            // 2. Tiêu hủy 100% đạn địch (EnemyProjectileTag) trong bán kính nổ
            foreach (var (projTransform, projEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<EnemyProjectileTag>()
                         .WithEntityAccess())
            {
                if (math.distancesq(centerPos, projTransform.ValueRO.Position) <= radiusSq)
                {
                    ecb.DestroyEntity(projEntity);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
