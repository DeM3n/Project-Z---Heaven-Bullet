using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống kích hoạt và tính toán truyền sét của Kỹ năng Lôi Điện (Electric / Chain Lightning).
/// - Khởi tạo tự động nếu chưa có Skill entity.
/// - Tìm mục tiêu tia sét cha (Primary Bolts) gần Player nhất.
/// - Thực hiện chuỗi lan truyền (Chain Jumps) sang các mục tiêu kế cận qua Spatial Partitioning / Khoảng cách.
/// - Cấp 4: Hỗ trợ nảy ngược về mục tiêu cũ (Bounce-back) và kích hoạt Choáng (EnemyStunData).
/// - Đẩy sát thương vào Damage Pipeline (EnemyDamage.Add) và sinh ElectricArcEvent cho Visual Bridge.
/// </summary>
[UpdateBefore(typeof(EnemyDamageResolutionSystem))]
[BurstCompile]
public partial struct ElectricSkillCastSystem : ISystem
{
    private EntityQuery _playerQuery;
    private EntityQuery _skillQuery;
    private EntityQuery _enemyQuery;

    private BufferLookup<EnemyIncomingDamage> _incomingDamageLookup;
    private ComponentLookup<EnemyStunData> _stunLookup;
    private ComponentLookup<HealthData> _healthLookup;
    private ComponentLookup<LocalTransform> _transformLookup;

    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .Build();

        _skillQuery = SystemAPI.QueryBuilder()
            .WithAll<ElectricSkillTag, SkillRuntimeData, ElectricSkillData>()
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
        // 1. Tự động Bootstrap Skill Entity nếu chưa có trong Scene (mặc định mở khóa Level 1)
        if (_skillQuery.IsEmpty)
        {
            if (!_playerQuery.IsEmpty)
            {
                Entity playerEntity = _playerQuery.GetSingletonEntity();
                Entity skillEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<ElectricSkillTag>(skillEntity);
                state.EntityManager.AddComponentData(skillEntity, new SkillOwnerData { Owner = playerEntity });

                state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
                {
                    Cooldown = 5.4f,
                    Timer = 0.5f,
                    Level = 1,
                    SkillId = default
                });

                state.EntityManager.AddComponentData(skillEntity, new ElectricSkillData
                {
                    BaseDamage = 60f,
                    DetectRange = 9.5f,
                    ChainRange = 6.0f,
                    ChainDamageRatio = 0.30f,
                    PrimaryBoltCount = 1,
                    ChainCount = 1,
                    CanBounceBack = false,
                    StunDuration = 0f
                });

                state.EntityManager.AddComponentData(skillEntity, new ElectricLevelBalanceData
                {
                    CooldownLv1 = 5.4f,
                    CooldownLv2 = 4.86f,
                    CooldownLv3 = 4.374f,
                    CooldownLv4 = 4.374f,

                    DamageLv1 = 60f,
                    DamageLv2 = 90f,
                    DamageLv3 = 90f,
                    DamageLv4 = 120f,

                    PrimaryCountLv1 = 1,
                    PrimaryCountLv2 = 1,
                    PrimaryCountLv3 = 2,
                    PrimaryCountLv4 = 2,

                    ChainCountLv1 = 1,
                    ChainCountLv2 = 2,
                    ChainCountLv3 = 2,
                    ChainCountLv4 = 4
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

        foreach (var (runtime, owner, skill, balance, skillEntity) in
                 SystemAPI.Query<RefRW<SkillRuntimeData>, RefRO<SkillOwnerData>, RefRW<ElectricSkillData>, RefRO<ElectricLevelBalanceData>>()
                     .WithAll<ElectricSkillTag>()
                     .WithEntityAccess())
        {
            int level = math.clamp(runtime.ValueRO.Level, 0, 4);
            if (level <= 0) continue; // Chưa mở khóa kỹ năng

            // Đồng bộ thông số theo Cấp độ hiện tại
            float currentDamage = balance.ValueRO.DamageLv1;
            int primaryCount = balance.ValueRO.PrimaryCountLv1;
            int chainCount = balance.ValueRO.ChainCountLv1;
            bool canBounceBack = (level >= 4);
            float stunDuration = (level >= 4) ? 0.5f : 0f;

            if (level == 2)
            {
                currentDamage = balance.ValueRO.DamageLv2;
                primaryCount = balance.ValueRO.PrimaryCountLv2;
                chainCount = balance.ValueRO.ChainCountLv2;
            }
            else if (level == 3)
            {
                currentDamage = balance.ValueRO.DamageLv3;
                primaryCount = balance.ValueRO.PrimaryCountLv3;
                chainCount = balance.ValueRO.ChainCountLv3;
            }
            else if (level >= 4)
            {
                currentDamage = balance.ValueRO.DamageLv4;
                primaryCount = balance.ValueRO.PrimaryCountLv4;
                chainCount = balance.ValueRO.ChainCountLv4;
            }

            skill.ValueRW.BaseDamage = currentDamage;
            skill.ValueRW.PrimaryBoltCount = primaryCount;
            skill.ValueRW.ChainCount = chainCount;
            skill.ValueRW.CanBounceBack = canBounceBack;
            skill.ValueRW.StunDuration = stunDuration;

            // Đếm ngược thời gian hồi chiêu
            runtime.ValueRW.Timer -= dt;
            if (runtime.ValueRO.Timer > 0f) continue;

            if (!state.EntityManager.Exists(owner.ValueRO.Owner)) continue;
            float3 casterPos = state.EntityManager.GetComponentData<LocalTransform>(owner.ValueRO.Owner).Position;

            if (_enemyQuery.IsEmpty) continue;

            // Thu thập các quái còn sống trong tầm DetectRange để chọn mục tiêu tia sét cha
            var enemyEntities = _enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var enemyHealths = _enemyQuery.ToComponentDataArray<HealthData>(Allocator.Temp);

            var primaryTargets = new NativeList<Entity>(primaryCount, Allocator.Temp);
            CollectNearestEnemies(casterPos, skill.ValueRO.DetectRange, primaryCount,
                enemyEntities, enemyTransforms, enemyHealths, ref primaryTargets);

            if (primaryTargets.IsEmpty)
            {
                enemyEntities.Dispose();
                enemyTransforms.Dispose();
                enemyHealths.Dispose();
                primaryTargets.Dispose();
                continue;
            }

            // Đặt lại Cooldown sau khi tìm thấy mục tiêu
            runtime.ValueRW.Timer = runtime.ValueRO.Cooldown;

            float chainDamage = currentDamage * skill.ValueRO.ChainDamageRatio;

            // Phóng từng tia sét cha độc lập (Lv1-2: 1 tia, Lv3-4: 2 tia)
            for (int p = 0; p < primaryTargets.Length; p++)
            {
                Entity primaryTarget = primaryTargets[p];
                if (!_healthLookup.HasComponent(primaryTarget) || _healthLookup[primaryTarget].CurrentHP <= 0f)
                    continue;

                float3 primaryPos = _transformLookup[primaryTarget].Position;

                // 1. Gây sát thương tia cha vào pipeline
                EnemyDamage.Add(ref _incomingDamageLookup, primaryTarget, SkillIds.Electric, currentDamage, owner.ValueRO.Owner);

                // Nếu Lv4: Kích hoạt Choáng (Stun)
                if (stunDuration > 0f && _stunLookup.HasComponent(primaryTarget))
                {
                    _stunLookup.SetComponentEnabled(primaryTarget, true);
                    _stunLookup[primaryTarget] = new EnemyStunData { StunTimer = stunDuration };
                }

                // Sinh event vẽ tia sét cha từ Caster -> Primary Target
                Entity primaryArcEvent = ecb.CreateEntity();
                ecb.AddComponent(primaryArcEvent, new ElectricArcEvent
                {
                    StartPos = casterPos,
                    EndPos = primaryPos,
                    Damage = currentDamage,
                    IsPrimary = true,
                    InflictsStun = (stunDuration > 0f),
                    HopIndex = 0,
                    TargetEntity = primaryTarget
                });

                // 2. Thực hiện chuỗi lan truyền (Chain Jumps)
                var chainHistory = new NativeList<Entity>(chainCount + 2, Allocator.Temp);
                chainHistory.Add(primaryTarget);

                Entity currentNode = primaryTarget;
                float3 currentPos = primaryPos;

                for (int hop = 1; hop <= chainCount; hop++)
                {
                    Entity nextTarget = FindNextChainTarget(currentPos, skill.ValueRO.ChainRange, currentNode,
                        canBounceBack, chainHistory, enemyEntities, enemyTransforms, enemyHealths);

                    if (nextTarget == Entity.Null)
                        break; // Không còn quái nào trong tầm lan

                    float3 nextPos = _transformLookup[nextTarget].Position;

                    // Gây sát thương tia lan (30% sát thương gốc)
                    EnemyDamage.Add(ref _incomingDamageLookup, nextTarget, SkillIds.Electric, chainDamage, owner.ValueRO.Owner);

                    // Nếu Lv4: Gây Stun
                    if (stunDuration > 0f && _stunLookup.HasComponent(nextTarget))
                    {
                        _stunLookup.SetComponentEnabled(nextTarget, true);
                        _stunLookup[nextTarget] = new EnemyStunData { StunTimer = stunDuration };
                    }

                    // Sinh event vẽ tia sét lan từ currentNode -> nextTarget
                    Entity chainArcEvent = ecb.CreateEntity();
                    ecb.AddComponent(chainArcEvent, new ElectricArcEvent
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
            primaryTargets.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Thu thập các quái vật còn sống gần vị trí gốc nhất trong bán kính detectRange.
    /// </summary>
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
                if (result.Contains(e)) continue; // Đã chọn mục tiêu này cho tia cha khác

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

    /// <summary>
    /// Tìm mục tiêu tiếp theo cho chuỗi lan truyền tia sét:
    /// - Cấp 1-3: Tuyệt đối không chọn lại các quái đã bị giật trong chainHistory.
    /// - Cấp 4 (canBounceBack): Ưu tiên quái chưa bị giật, nhưng nếu không còn quái mới thì cho phép nảy về quái cũ (khác currentNode).
    /// </summary>
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
            if (candidate == currentNode) continue; // Không giật lại chính mình ở cùng 1 bước

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

        // Ưu tiên quái mới chưa dính sét trước, nếu không có quái mới và ở Lv4 thì nảy về quái cũ
        if (bestUnhit != Entity.Null) return bestUnhit;
        if (canBounceBack && bestBounce != Entity.Null) return bestBounce;

        return Entity.Null;
    }
}

