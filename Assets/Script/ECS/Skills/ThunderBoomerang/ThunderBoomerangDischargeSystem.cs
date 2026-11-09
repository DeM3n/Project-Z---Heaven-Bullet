using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý chu kỳ phóng sét tự động từ Boomerang khi Kỹ năng Hợp Thể (Thunder Boomerang) đang hoạt động.
/// - Mỗi khi Boomerang bay được 0.5s: Phóng đợt 1 (2 tia sét đánh vào 2 quái gần Boomerang nhất).
/// - Đúng 1.0s sau (tại 1.5s): Phóng đợt 2 (2 tia sét tiếp theo).
/// - Tia sét phóng từ Boomerang thừa hưởng toàn bộ sức mạnh của Lôi Điện Cấp 4:
///   Gây sát thương, lan truyền (Chain Jumps), hỗ trợ nảy ngược (Bounce-back) và Gây Choáng (Stun 0.5s)!
/// </summary>
[UpdateBefore(typeof(EnemyDamageResolutionSystem))]
[BurstCompile]
public partial struct ThunderBoomerangDischargeSystem : ISystem
{
    private EntityQuery _fusionSkillQuery;
    private EntityQuery _playerQuery;
    private EntityQuery _enemyQuery;

    private BufferLookup<EnemyIncomingDamage> _incomingDamageLookup;
    private ComponentLookup<EnemyStunData> _stunLookup;
    private ComponentLookup<HealthData> _healthLookup;
    private ComponentLookup<LocalTransform> _transformLookup;

    public void OnCreate(ref SystemState state)
    {
        _fusionSkillQuery = SystemAPI.QueryBuilder()
            .WithAll<ThunderBoomerangFusionTag, SkillRuntimeData, ThunderBoomerangSkillData>()
            .Build();

        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .Build();

        _enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, LocalTransform, HealthData>()
            .Build();

        _incomingDamageLookup = state.GetBufferLookup<EnemyIncomingDamage>(false);
        _stunLookup = state.GetComponentLookup<EnemyStunData>(false);
        _healthLookup = state.GetComponentLookup<HealthData>(true);
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Tự động Bootstrap Skill Entity nếu chưa có trong Scene
        if (_fusionSkillQuery.IsEmpty)
        {
            if (!_playerQuery.IsEmpty)
            {
                Entity playerEntity = _playerQuery.GetSingletonEntity();
                Entity skillEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<ThunderBoomerangFusionTag>(skillEntity);
                state.EntityManager.AddComponentData(skillEntity, new SkillOwnerData { Owner = playerEntity });
                state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
                {
                    Cooldown = 0f,
                    Timer = 0f,
                    Level = 0, // Mặc định 0 (chưa học), mở khóa khi Boomerang Lv4 + Electric Lv4
                    SkillId = default
                });
                state.EntityManager.AddComponentData(skillEntity, new ThunderBoomerangSkillData
                {
                    BoltDamage = 90f,
                    DetectRange = 8.5f,
                    ChainRange = 6.0f,
                    ChainCount = 2,
                    BoltsPerDischarge = 2,
                    MaxDischarges = 2,
                    InitialDelay = 0.5f,
                    DischargeInterval = 1.0f,
                    StunDuration = 0.5f
                });
            }
            return;
        }

        float dt = SystemAPI.Time.DeltaTime;
        _incomingDamageLookup.Update(ref state);
        _stunLookup.Update(ref state);
        _healthLookup.Update(ref state);
        _transformLookup.Update(ref state);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Duyệt qua tất cả các Boomerang đang bay có mang BoomerangLightningData
        foreach (var (transform, lightning, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<BoomerangLightningData>>()
                     .WithAll<BoomerangTag>()
                     .WithEntityAccess())
        {
            if (lightning.ValueRO.DischargeCount >= lightning.ValueRO.MaxDischarges)
                continue; // Đã bắn đủ 2 lần

            lightning.ValueRW.Timer -= dt;
            if (lightning.ValueRO.Timer > 0f)
                continue; // Chưa tới thời điểm bắn

            // Đến lượt bắn: Tăng số lần đã phóng và đặt bộ đếm cho lần kế tiếp (1.0s sau)
            lightning.ValueRW.DischargeCount++;
            lightning.ValueRW.Timer = 1.0f;

            if (_enemyQuery.IsEmpty)
                continue;

            float3 boomerangPos = transform.ValueRO.Position;
            Entity source = SystemAPI.HasComponent<BoomerangProjectileData>(entity)
                ? SystemAPI.GetComponent<BoomerangProjectileData>(entity).Owner
                : Entity.Null;

            var enemyEntities = _enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var enemyHealths = _enemyQuery.ToComponentDataArray<HealthData>(Allocator.Temp);

            int boltsToFire = lightning.ValueRO.BoltsPerDischarge; // 2 tia
            var targetEnemies = new NativeList<Entity>(boltsToFire, Allocator.Temp);

            // Tìm 2 quái gần Boomerang nhất trong tầm DetectRange
            CollectNearestEnemies(boomerangPos, lightning.ValueRO.DetectRange, boltsToFire,
                enemyEntities, enemyTransforms, enemyHealths, ref targetEnemies);

            float boltDamage = lightning.ValueRO.BoltDamage;
            float chainDamage = boltDamage * 0.30f;
            int chainCount = lightning.ValueRO.ChainCount;
            float chainRange = lightning.ValueRO.ChainRange;
            float stunDuration = lightning.ValueRO.StunDuration;

            // Bắn các tia sét từ Boomerang vào từng mục tiêu
            for (int b = 0; b < targetEnemies.Length; b++)
            {
                Entity primaryTarget = targetEnemies[b];
                if (!_healthLookup.HasComponent(primaryTarget) || _healthLookup[primaryTarget].CurrentHP <= 0f)
                    continue;

                float3 primaryPos = _transformLookup[primaryTarget].Position;

                // Gây sát thương tia sét cha từ Boomerang
                EnemyDamage.Add(ref _incomingDamageLookup, primaryTarget, SkillIds.ThunderBoomerang, boltDamage, source);

                // Gây Stun 0.5s
                if (stunDuration > 0f && _stunLookup.HasComponent(primaryTarget))
                {
                    _stunLookup.SetComponentEnabled(primaryTarget, true);
                    _stunLookup[primaryTarget] = new EnemyStunData { StunTimer = stunDuration };
                }

                // Sinh sự kiện vẽ tia sét: Tâm phát là Boomerang, điểm đến là quái
                Entity arc1 = ecb.CreateEntity();
                ecb.AddComponent(arc1, new ElectricArcEvent
                {
                    StartPos = boomerangPos,
                    EndPos = primaryPos,
                    Damage = boltDamage,
                    IsPrimary = true,
                    InflictsStun = (stunDuration > 0f),
                    HopIndex = 0,
                    TargetEntity = primaryTarget
                });

                // Chuỗi lan truyền tia sét (Chain Jumps)
                var chainHistory = new NativeList<Entity>(chainCount + 2, Allocator.Temp);
                chainHistory.Add(primaryTarget);

                Entity currentNode = primaryTarget;
                float3 currentPos = primaryPos;

                for (int hop = 1; hop <= chainCount; hop++)
                {
                    Entity nextTarget = FindNextChainTarget(currentPos, chainRange, currentNode,
                        true, chainHistory, enemyEntities, enemyTransforms, enemyHealths);

                    if (nextTarget == Entity.Null)
                        break;

                    float3 nextPos = _transformLookup[nextTarget].Position;

                    // Gây sát thương lan
                    EnemyDamage.Add(ref _incomingDamageLookup, nextTarget, SkillIds.ThunderBoomerang, chainDamage, source);

                    // Gây Stun
                    if (stunDuration > 0f && _stunLookup.HasComponent(nextTarget))
                    {
                        _stunLookup.SetComponentEnabled(nextTarget, true);
                        _stunLookup[nextTarget] = new EnemyStunData { StunTimer = stunDuration };
                    }

                    // Sinh event vẽ tia sét lan
                    Entity chainArc = ecb.CreateEntity();
                    ecb.AddComponent(chainArc, new ElectricArcEvent
                    {
                        StartPos = currentPos,
                        EndPos = nextPos,
                        Damage = chainDamage,
                        IsPrimary = false,
                        InflictsStun = (stunDuration > 0f),
                        HopIndex = hop,
                        TargetEntity = nextTarget
                    });

                    chainHistory.Add(nextTarget);
                    currentNode = nextTarget;
                    currentPos = nextPos;
                }

                chainHistory.Dispose();
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
            enemyHealths.Dispose();
            targetEnemies.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private static void CollectNearestEnemies(
        float3 origin,
        float maxRange,
        int maxCount,
        NativeArray<Entity> entities,
        NativeArray<LocalTransform> transforms,
        NativeArray<HealthData> healths,
        ref NativeList<Entity> result)
    {
        float maxRangeSq = maxRange * maxRange;

        for (int k = 0; k < maxCount; k++)
        {
            float nearestDistSq = maxRangeSq;
            Entity nearestEnemy = Entity.Null;

            for (int i = 0; i < entities.Length; i++)
            {
                if (healths[i].CurrentHP <= 0f) continue;

                Entity e = entities[i];
                if (result.Contains(e)) continue;

                float distSq = math.distancesq(origin, transforms[i].Position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestEnemy = e;
                }
            }

            if (nearestEnemy != Entity.Null)
            {
                result.Add(nearestEnemy);
            }
            else
            {
                break;
            }
        }
    }

    private static Entity FindNextChainTarget(
        float3 currentPos,
        float chainRange,
        Entity currentNode,
        bool canBounceBack,
        NativeList<Entity> chainHistory,
        NativeArray<Entity> entities,
        NativeArray<LocalTransform> transforms,
        NativeArray<HealthData> healths)
    {
        float chainRangeSq = chainRange * chainRange;

        Entity bestUnhit = Entity.Null;
        float bestUnhitDistSq = chainRangeSq;

        Entity bestBounce = Entity.Null;
        float bestBounceDistSq = chainRangeSq;

        for (int i = 0; i < entities.Length; i++)
        {
            if (healths[i].CurrentHP <= 0f) continue;

            Entity candidate = entities[i];
            if (candidate == currentNode) continue;

            float distSq = math.distancesq(currentPos, transforms[i].Position);
            if (distSq > chainRangeSq) continue;

            bool alreadyHit = chainHistory.Contains(candidate);

            if (!alreadyHit)
            {
                if (distSq < bestUnhitDistSq)
                {
                    bestUnhitDistSq = distSq;
                    bestUnhit = candidate;
                }
            }
            else if (canBounceBack)
            {
                if (distSq < bestBounceDistSq)
                {
                    bestBounceDistSq = distSq;
                    bestBounce = candidate;
                }
            }
        }

        if (bestUnhit != Entity.Null) return bestUnhit;
        if (canBounceBack && bestBounce != Entity.Null) return bestBounce;

        return Entity.Null;
    }
}

