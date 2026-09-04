using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ProjectileCollisionSystem : ISystem
{
    private EntityQuery _enemyQuery;

    public void OnCreate(ref SystemState state)
    {
        _enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, LocalTransform, HealthData>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_enemyQuery.IsEmpty) return;

        var enemyEntities = _enemyQuery.ToEntityArray(Allocator.TempJob);
        var enemyTransforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, dmg, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<DamageOnContactData>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            for (int i = 0; i < enemyEntities.Length; i++)
            {
                float distSq = math.distancesq(transform.ValueRO.Position, enemyTransforms[i].Position);
                float hitDistSq = dmg.ValueRO.HitRadius * dmg.ValueRO.HitRadius;
                if (distSq > hitDistSq) continue;

                Entity enemy = enemyEntities[i];
                HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                hp.CurrentHP -= dmg.ValueRO.Damage;
                ecb.SetComponent(enemy, hp);

                ecb.DestroyEntity(entity); // projectile biến mất sau khi trúng — chưa xử lý piercing (GDD 2.2 Lv5)
                break; // 1 projectile chỉ trúng 1 enemy/frame
            }
        }

        enemyEntities.Dispose();
        enemyTransforms.Dispose();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}