using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateAfter(typeof(ProjectileCollisionSystem))]
public partial struct DeathCleanupSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    public void OnCreate(ref SystemState state)
    {
        uint seed = (uint)System.DateTime.UtcNow.Ticks;
        if (seed == 0) seed = 12345;
        _random = new Unity.Mathematics.Random(seed);
    }

    [BurstCompile]
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
                // Kiểm tra tỉ lệ rớt ngọc EXP theo DropChance của từng loại quái
                float roll = _random.NextFloat(0f, 1f);
                if (roll <= drop.ValueRO.DropChance && drop.ValueRO.ExpOrbPrefab != Entity.Null)
                {
                    Entity orb = ecb.Instantiate(drop.ValueRO.ExpOrbPrefab);
                    ecb.SetComponent(orb, LocalTransform.FromPosition(transform.ValueRO.Position));
                    ecb.SetComponent(orb, new ExpOrbData { ExpValue = drop.ValueRO.ExpReward, FlySpeed = 0f });
                }

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}