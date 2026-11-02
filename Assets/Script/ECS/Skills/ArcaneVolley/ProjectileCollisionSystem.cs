using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[BurstCompile]
public partial struct ProjectileCollisionSystem : ISystem
{
    private BufferLookup<EnemyIncomingDamage> _incomingDamageLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();
        _incomingDamageLookup = state.GetBufferLookup<EnemyIncomingDamage>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<SpatialGridSingleton>()) return;

        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        _incomingDamageLookup.Update(ref state);

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

                    ushort skillId = dmg.ValueRO.SkillId != 0 ? dmg.ValueRO.SkillId : SkillIds.ProjectileSkill;
                    EnemyDamage.Add(ref _incomingDamageLookup, enemy, skillId, dmg.ValueRO.Damage);

                    ecb.DestroyEntity(entity);
                    hit = true;
                    break;
                } while (grid.TryGetNextValue(out enemy, ref it));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
