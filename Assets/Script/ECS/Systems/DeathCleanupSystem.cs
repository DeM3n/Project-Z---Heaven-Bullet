using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(ProjectileCollisionSystem))]
public partial struct DeathCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (hp, transform, drop, entity) in
                SystemAPI.Query<RefRO<HealthData>, RefRO<LocalTransform>, RefRO<EnemyDeathDropData>>()
                     .WithAll<EnemyTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.CurrentHP <= 0f)
            {
                Entity orb = ecb.Instantiate(drop.ValueRO.ExpOrbPrefab);
                ecb.SetComponent(orb, LocalTransform.FromPosition(transform.ValueRO.Position));
                ecb.SetComponent(orb, new ExpOrbData { ExpValue = drop.ValueRO.ExpReward });
                ecb.DestroyEntity(entity); // destroy ngay ở prototype; deferred/rải theo frame (GDD 11.5) để dành khi có mass-death spike
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}