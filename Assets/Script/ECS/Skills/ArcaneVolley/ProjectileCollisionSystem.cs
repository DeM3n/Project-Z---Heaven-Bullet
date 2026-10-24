using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[BurstCompile]
public partial struct ProjectileCollisionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, dmg, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<DamageOnContactData>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            int2 cell = SpatialGridUtility.ToCell(transform.ValueRO.Position.xy, cellSize);
            float hitDistSq = dmg.ValueRO.HitRadius * dmg.ValueRO.HitRadius;
            bool hit = false;

            for (int dx = -1; dx <= 1 && !hit; dx++)
            for (int dy = -1; dy <= 1 && !hit; dy++)
            {
                int hash = SpatialGridUtility.HashCell(cell + new int2(dx, dy));
                if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                do
                {
                    if (!state.EntityManager.Exists(enemy)) continue;

                    LocalTransform enemyTransform = state.EntityManager.GetComponentData<LocalTransform>(enemy);
                    float distSq = math.distancesq(transform.ValueRO.Position, enemyTransform.Position);
                    if (distSq > hitDistSq) continue;

                    HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                    hp.CurrentHP -= dmg.ValueRO.Damage;
                    ecb.SetComponent(enemy, hp);

                    ecb.DestroyEntity(entity); // projectile biến mất sau khi trúng — chưa xử lý piercing (GDD 2.2 Lv5)
                    hit = true;
                    break;
                } while (grid.TryGetNextValue(out enemy, ref it));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}