using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều khiển thời gian hồi chiêu và bắn Cầu Lửa (Fireball) từ vị trí Player
/// Hướng ngắm tự động ưu tiên quái vật gần nhất trong tầm quét qua Spatial Grid.
/// </summary>
[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct FireballSkillCastSystem : ISystem
{
    private EntityQuery _enemyQuery;
    private EntityQuery _playerQuery;
    private EntityQuery _skillQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();

        _enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, LocalTransform>()
            .Build();

        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .Build();

        _skillQuery = SystemAPI.QueryBuilder()
            .WithAll<FireballSkillTag, SkillRuntimeData, FireballSkillData>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        // 1. Tự động Bootstrap Skill Entity nếu chưa có trong Scene (giúp skill hoạt động tức thì)
        if (_skillQuery.IsEmpty)
        {
            if (!_playerQuery.IsEmpty)
            {
                Entity playerEntity = _playerQuery.GetSingletonEntity();
                Entity skillEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<FireballSkillTag>(skillEntity);
                state.EntityManager.AddComponentData(skillEntity, new SkillOwnerData { Owner = playerEntity });
                var cfg = FireballSkillConfigSO.Instance;
                state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
                {
                    Cooldown = cfg != null ? cfg.GetCooldown(1) : 5.4f,
                    Timer = 0.5f,
                    Level = 1,
                    SkillId = default
                });
                state.EntityManager.AddComponentData(skillEntity, new FireballSkillData
                {
                    FireballPrefab = Entity.Null,
                    SubFireballPrefab = Entity.Null,
                    BaseDamage = cfg != null ? cfg.GetDamage(1) : 45f,
                    FireballSpeed = cfg != null ? cfg.FireballSpeed : 9.5f,
                    ExplosionRadius = cfg != null ? cfg.GetExplosionRadius(1) : 1.8f,
                    SubFireballSpeed = cfg != null ? cfg.SubFireballSpeed : 11.0f,
                    SubFireballLifetime = cfg != null ? cfg.SubFireballLifetime : 0.45f,
                    SubExplosionRadius = cfg != null ? cfg.SubExplosionRadius : 1.2f,
                    FireZoneRadius = cfg != null ? cfg.FireZoneRadius : 1.8f,
                    FireZoneDuration = cfg != null ? cfg.FireZoneDuration : 3.5f,
                    FireZoneTickInterval = cfg != null ? cfg.FireZoneTickInterval : 0.25f,
                    FireZoneDamagePerTick = cfg != null ? cfg.FireZoneDamagePerTick : 16f,
                    DetectRange = cfg != null ? cfg.DetectRange : 9.5f
                });
                state.EntityManager.AddComponentData(skillEntity, new FireballLevelBalanceData
                {
                    CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 5.4f,
                    CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 3.6f,
                    CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 2.7f,
                    CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 2.25f,

                    DamageLv1 = cfg != null ? cfg.GetDamage(1) : 45f,
                    DamageLv2 = cfg != null ? cfg.GetDamage(2) : 68f,
                    DamageLv3 = cfg != null ? cfg.GetDamage(3) : 95f,
                    DamageLv4 = cfg != null ? cfg.GetDamage(4) : 120f,

                    SubCountLv1 = cfg != null ? cfg.GetSubFireballCount(1) : 1,
                    SubCountLv2 = cfg != null ? cfg.GetSubFireballCount(2) : 2,
                    SubCountLv3 = cfg != null ? cfg.GetSubFireballCount(3) : 3,
                    SubCountLv4 = cfg != null ? cfg.GetSubFireballCount(4) : 4,

                    RadiusLv1 = cfg != null ? cfg.GetExplosionRadius(1) : 1.8f,
                    RadiusLv2 = cfg != null ? cfg.GetExplosionRadius(2) : 2.1f,
                    RadiusLv3 = cfg != null ? cfg.GetExplosionRadius(3) : 2.4f,
                    RadiusLv4 = cfg != null ? cfg.GetExplosionRadius(4) : 2.8f
                });
            }
            return;
        }

        float dt = SystemAPI.Time.DeltaTime;
        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (runtime, owner, skill, entity) in
                 SystemAPI.Query<RefRW<SkillRuntimeData>, RefRO<SkillOwnerData>, RefRO<FireballSkillData>>()
                     .WithAll<FireballSkillTag>()
                     .WithEntityAccess())
        {
            int level = runtime.ValueRO.Level;
            if (level <= 0) continue; // Chưa kích hoạt hoặc level 0

            runtime.ValueRW.Timer -= dt;
            if (runtime.ValueRO.Timer > 0f) continue;

            // Tính toán Cooldown, Sát thương, Bán kính nổ và Số cầu con trực tiếp từ ScriptableObject (FireballLevelBalanceData)
            float scaledCooldown;
            float damage;
            float explosionRadius;
            int subFireballCount;

            if (SystemAPI.HasComponent<FireballLevelBalanceData>(entity))
            {
                var bal = SystemAPI.GetComponent<FireballLevelBalanceData>(entity);
                scaledCooldown = bal.GetCooldown(level);
                damage = bal.GetDamage(level);
                explosionRadius = bal.GetRadius(level);
                subFireballCount = bal.GetSubCount(level);
            }
            else
            {
                scaledCooldown = level switch
                {
                    2 => 3.6f,
                    3 => 2.7f,
                    4 => 2.25f,
                    _ => 5.4f
                };
                damage = level switch
                {
                    2 => skill.ValueRO.BaseDamage * 1.45f,
                    3 => skill.ValueRO.BaseDamage * 2.00f,
                    4 => skill.ValueRO.BaseDamage * 2.67f,
                    _ => skill.ValueRO.BaseDamage
                };
                explosionRadius = level switch
                {
                    2 => skill.ValueRO.ExplosionRadius + 0.3f,
                    3 => skill.ValueRO.ExplosionRadius + 0.6f,
                    4 => skill.ValueRO.ExplosionRadius + 1.0f,
                    _ => skill.ValueRO.ExplosionRadius
                };
                subFireballCount = level;
            }

            runtime.ValueRW.Timer = runtime.ValueRO.Cooldown;

            // Lấy vị trí Caster (Player hoặc Clone)
            Entity casterEntity = owner.ValueRO.Owner;
            if (!state.EntityManager.Exists(casterEntity) || !state.EntityManager.HasComponent<LocalTransform>(casterEntity))
                continue;

            float3 casterPos = state.EntityManager.GetComponentData<LocalTransform>(casterEntity).Position;

            // Tìm tối đa 2 mục tiêu quái độc lập gần nhất qua Spatial Grid
            float detectRange = skill.ValueRO.DetectRange;
            float2 targetDir1, targetDir2;
            int foundTargetCount = TryFindTwoTargets(ref state, grid, cellSize, casterPos, detectRange, out targetDir1, out targetDir2);

            if (foundTargetCount == 0)
            {
                // Nếu không có quái trong tầm, lấy hướng di chuyển của Caster làm hướng ngắm mặc định
                float2 defaultDir = new float2(1f, 0f);
                if (state.EntityManager.HasComponent<MovementData>(casterEntity))
                {
                    float2 moveDir = state.EntityManager.GetComponentData<MovementData>(casterEntity).Direction;
                    if (math.lengthsq(moveDir) > 0.01f)
                    {
                        defaultDir = math.normalizesafe(moveDir);
                    }
                }
                targetDir1 = defaultDir;
                targetDir2 = defaultDir;
            }

            // Số lượng Cầu Lửa Mẹ (Main Fireball):
            // Cấp 1 & 2: 1 Cầu Lửa Mẹ ngắm vào mục tiêu chính
            // Cấp 3 & 4: 2 Cầu Lửa Mẹ - Tự động ngắm vào 2 kẻ địch khác nhau!
            int mainFireballCount = (level >= 3) ? 2 : 1;

            for (int i = 0; i < mainFireballCount; i++)
            {
                float2 shootDir;
                float3 spawnPos;

                if (i == 0)
                {
                    shootDir = targetDir1;
                    // Nếu chỉ có 1 mục tiêu duy nhất và bắn 2 quả, quả 1 lệch nhẹ song song sang trái (-0.2m)
                    float2 perp = new float2(-shootDir.y, shootDir.x);
                    float lateral = (mainFireballCount > 1 && foundTargetCount < 2) ? -0.2f : 0f;
                    spawnPos = casterPos + new float3(perp.x * lateral, perp.y * lateral, 0f);
                }
                else
                {
                    if (foundTargetCount >= 2)
                    {
                        // ĐÃ TÌM THẤY KẺ ĐỊCH THỨ 2: Quả thứ 2 ngắm thẳng 100% vào kẻ địch thứ 2!
                        shootDir = targetDir2;
                        spawnPos = casterPos;
                    }
                    else
                    {
                        // Chỉ có 1 mục tiêu duy nhất: Bắn thẳng vào cùng mục tiêu (lệch nhẹ song song sang phải +0.2m)
                        shootDir = targetDir1;
                        float2 perp = new float2(-shootDir.y, shootDir.x);
                        spawnPos = casterPos + new float3(perp.x * 0.2f, perp.y * 0.2f, 0f);
                    }
                }

                Entity fireball;
                if (skill.ValueRO.FireballPrefab != Entity.Null)
                {
                    fireball = ecb.Instantiate(skill.ValueRO.FireballPrefab);
                    ecb.SetComponent(fireball, LocalTransform.FromPosition(spawnPos));
                }
                else
                {
                    fireball = ecb.CreateEntity();
                    ecb.AddComponent(fireball, LocalTransform.FromPosition(spawnPos));
                }

                ecb.AddComponent<FireballTag>(fireball);
                ecb.AddComponent(fireball, new MovementData
                {
                    Direction = shootDir,
                    MoveSpeed = skill.ValueRO.FireballSpeed
                });

                ecb.AddComponent(fireball, new FireballProjectileData
                {
                    Direction = shootDir,
                    Speed = skill.ValueRO.FireballSpeed,
                    Damage = damage,
                    ExplosionRadius = explosionRadius,
                    Lifetime = 2.8f,
                    HitRadius = 0.45f,
                    SubFireballCount = subFireballCount,
                    SkillLevel = level,
                    IsSubFireball = false,
                    Source = casterEntity
                });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private int TryFindTwoTargets(
        ref SystemState state,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float3 playerPos,
        float detectRange,
        out float2 targetDir1,
        out float2 targetDir2)
    {
        int2 playerCell = SpatialGridUtility.ToCell(playerPos.xy, cellSize);
        int cellRadius = (int)math.ceil(detectRange / cellSize);
        float maxRangeSq = detectRange * detectRange;

        Entity enemy1 = Entity.Null;
        float distSq1 = maxRangeSq;
        float3 pos1 = playerPos;

        Entity enemy2 = Entity.Null;
        float distSq2 = maxRangeSq;
        float3 pos2 = playerPos;

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            int hash = SpatialGridUtility.HashCell(playerCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy) || !state.EntityManager.HasComponent<LocalTransform>(enemy))
                    continue;

                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                float distSq = math.distancesq(playerPos, enemyPos);
                if (distSq > maxRangeSq) continue;

                if (distSq < distSq1)
                {
                    if (enemy1 != enemy)
                    {
                        enemy2 = enemy1;
                        distSq2 = distSq1;
                        pos2 = pos1;
                    }
                    enemy1 = enemy;
                    distSq1 = distSq;
                    pos1 = enemyPos;
                }
                else if (enemy != enemy1 && distSq < distSq2)
                {
                    enemy2 = enemy;
                    distSq2 = distSq;
                    pos2 = enemyPos;
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        // Fallback: Nếu Spatial Grid ô lân cận chưa tìm đủ 2 mục tiêu, kiểm tra query quái tổng
        if (enemy1 == Entity.Null || enemy2 == Entity.Null)
        {
            if (!_enemyQuery.IsEmpty)
            {
                var entities = _enemyQuery.ToEntityArray(Allocator.Temp);
                var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity e = entities[i];
                    float3 ePos = transforms[i].Position;
                    float distSq = math.distancesq(playerPos, ePos);
                    if (distSq > maxRangeSq) continue;

                    if (distSq < distSq1)
                    {
                        if (enemy1 != e)
                        {
                            enemy2 = enemy1;
                            distSq2 = distSq1;
                            pos2 = pos1;
                        }
                        enemy1 = e;
                        distSq1 = distSq;
                        pos1 = ePos;
                    }
                    else if (e != enemy1 && distSq < distSq2)
                    {
                        enemy2 = e;
                        distSq2 = distSq;
                        pos2 = ePos;
                    }
                }

                entities.Dispose();
                transforms.Dispose();
            }
        }

        int targetCount = 0;
        if (enemy1 != Entity.Null)
        {
            targetDir1 = math.normalizesafe((pos1 - playerPos).xy);
            targetCount++;
        }
        else
        {
            targetDir1 = new float2(1f, 0f);
        }

        if (enemy2 != Entity.Null)
        {
            targetDir2 = math.normalizesafe((pos2 - playerPos).xy);
            targetCount++;
        }
        else
        {
            targetDir2 = targetDir1;
        }

        return targetCount;
    }
}
