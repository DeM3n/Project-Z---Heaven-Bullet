using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct SkillCastSystem : ISystem
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

        bool hasEnemies = !_enemyQuery.IsEmpty;
        NativeArray<LocalTransform> enemyTransforms = hasEnemies
            ? _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp)
            : default;

        foreach (var (skill, transform) in
                 SystemAPI.Query<RefRW<SkillCastData>, RefRO<LocalTransform>>())
        {
            skill.ValueRW.Timer -= dt;
            if (skill.ValueRO.Timer > 0f) continue;
            skill.ValueRW.Timer = skill.ValueRO.Cooldown;

            if (!hasEnemies) continue; // hết cooldown nhưng chưa có mục tiêu → bỏ lượt bắn này

            float3 casterPos = transform.ValueRO.Position;
            float closestDistSq = float.MaxValue;
            float3 closestPos = casterPos;

            // Brute-force tìm enemy gần nhất — tạm ổn ở quy mô prototype (vài chục enemy).
            // Sẽ thay bằng spatial hash grid (GDD mục 6.3) khi làm collision ở bước 4.
            for (int i = 0; i < enemyTransforms.Length; i++)
            {
                float distSq = math.distancesq(casterPos, enemyTransforms[i].Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPos = enemyTransforms[i].Position;
                }
            }

            float2 dir = math.normalizesafe((closestPos - casterPos).xy);

            Entity projectile = ecb.Instantiate(skill.ValueRO.ProjectilePrefab);
            ecb.SetComponent(projectile, LocalTransform.FromPosition(casterPos));
            ecb.SetComponent(projectile, new MovementData { Direction = dir, MoveSpeed = skill.ValueRO.ProjectileSpeed });
            ecb.SetComponent(projectile, new ProjectileLifetimeData { TimeRemaining = skill.ValueRO.ProjectileLifetime });
            ecb.SetComponent(projectile, new DamageOnContactData { Damage = skill.ValueRO.ProjectileDamage, HitRadius = skill.ValueRO.ProjectileHitRadius });
        }

        if (hasEnemies) enemyTransforms.Dispose();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}