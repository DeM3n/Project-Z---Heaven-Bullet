using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ShooterAttackSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, attack) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<ShooterAttackData>>()
                     .WithAll<ShooterTag>())
        {
            attack.ValueRW.Timer -= dt;
            if (attack.ValueRO.Timer > 0f) continue;

            float2 toPlayer = (playerPos - transform.ValueRO.Position).xy;
            float dist = math.length(toPlayer);
            if (dist > attack.ValueRO.PreferredRange + 1f) continue; // ngoài tầm, chưa bắn

            attack.ValueRW.Timer = attack.ValueRO.Cooldown;

            float2 dir = math.normalizesafe(toPlayer);
            Entity projectile = ecb.Instantiate(attack.ValueRO.ProjectilePrefab);
            ecb.SetComponent(projectile, LocalTransform.FromPosition(transform.ValueRO.Position));
            ecb.SetComponent(projectile, new MovementData
            {
                Direction = dir,
                MoveSpeed = attack.ValueRO.ProjectileSpeed
            });
            ecb.SetComponent(projectile, new DamageOnContactData
            {
                Damage = attack.ValueRO.Damage,
                HitRadius = attack.ValueRO.ProjectileHitRadius
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}