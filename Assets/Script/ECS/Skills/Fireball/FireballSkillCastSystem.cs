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
                state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
                {
                    Cooldown = 2.6f,
                    Timer = 0.5f,
                    Level = 1,
                    SkillId = new FixedString32Bytes("fireball")
                });
                state.EntityManager.AddComponentData(skillEntity, new FireballSkillData
                {
                    FireballPrefab = Entity.Null,
                    SubFireballPrefab = Entity.Null,
                    BaseDamage = 45f,
                    FireballSpeed = 9.5f,
                    ExplosionRadius = 1.8f,
                    SubFireballSpeed = 11.0f,
                    SubFireballLifetime = 0.45f,
                    SubExplosionRadius = 1.2f,
                    FireZoneRadius = 1.8f,
                    FireZoneDuration = 3.5f,
                    FireZoneTickInterval = 0.25f,
                    FireZoneDamagePerTick = 16f,
                    DetectRange = 9.5f
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

            // Tính toán Cooldown và Sát thương scale theo cấp độ (+10% ở mọi cấp độ):
            // Lv1: 2.86s, Lv2: 2.53s, Lv3: 2.20s, Lv4: 1.98s
            float scaledCooldown = level switch
            {
                2 => 2.53f,
                3 => 2.20f,
                4 => 1.98f,
                _ => 2.86f
            };
            runtime.ValueRW.Timer = scaledCooldown;

            // Lấy vị trí Player
            Entity playerEntity = owner.ValueRO.Owner;
            if (!state.EntityManager.Exists(playerEntity) || !state.EntityManager.HasComponent<LocalTransform>(playerEntity))
                continue;

            float3 playerPos = state.EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;

            // Tìm tối đa 2 mục tiêu quái độc lập gần nhất qua Spatial Grid
            float detectRange = skill.ValueRO.DetectRange;
            float2 targetDir1, targetDir2;
            int foundTargetCount = TryFindTwoTargets(ref state, grid, cellSize, playerPos, detectRange, out targetDir1, out targetDir2);

            if (foundTargetCount == 0)
            {
                // Nếu không có quái trong tầm, lấy hướng di chuyển của Player làm hướng ngắm mặc định
                float2 defaultDir = new float2(1f, 0f);
                if (state.EntityManager.HasComponent<MovementData>(playerEntity))
                {
                    float2 moveDir = state.EntityManager.GetComponentData<MovementData>(playerEntity).Direction;
                    if (math.lengthsq(moveDir) > 0.01f)
                    {
                        defaultDir = math.normalizesafe(moveDir);
                    }
                }
                targetDir1 = defaultDir;
                targetDir2 = defaultDir;
            }

            // Sát thương và Bán kính nổ cầu lửa mẹ theo cấp:
            // Lv1: 45 dmg, 1.8m
            // Lv2: 65 dmg, 2.1m
            // Lv3: 90 dmg, 2.4m
            // Lv4: 120 dmg, 2.8m
            float damage = level switch
            {
                2 => skill.ValueRO.BaseDamage * 1.45f,
                3 => skill.ValueRO.BaseDamage * 2.00f,
                4 => skill.ValueRO.BaseDamage * 2.67f,
                _ => skill.ValueRO.BaseDamage
            };

            float explosionRadius = level switch
            {
                2 => skill.ValueRO.ExplosionRadius + 0.3f,
                3 => skill.ValueRO.ExplosionRadius + 0.6f,
                4 => skill.ValueRO.ExplosionRadius + 1.0f,
                _ => skill.ValueRO.ExplosionRadius
            };

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
                    spawnPos = playerPos + new float3(perp.x * lateral, perp.y * lateral, 0f);
                }
                else
                {
                    if (foundTargetCount >= 2)
                    {
                        // ĐÃ TÌM THẤY KẺ ĐỊCH THỨ 2: Quả thứ 2 ngắm thẳng 100% vào kẻ địch thứ 2!
                        shootDir = targetDir2;
                        spawnPos = playerPos;
                    }
                    else
                    {
                        // Chỉ có 1 mục tiêu duy nhất: Bắn thẳng vào cùng mục tiêu (lệch nhẹ song song sang phải +0.2m)
                        shootDir = targetDir1;
                        float2 perp = new float2(-shootDir.y, shootDir.x);
                        spawnPos = playerPos + new float3(perp.x * 0.2f, perp.y * 0.2f, 0f);
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
                    SubFireballCount = level, // Số cầu lửa con = cấp độ kỹ năng (1 ở Lv1, 2 ở Lv2, 3 ở Lv3, 4 ở Lv4)
                    SkillLevel = level,
                    IsSubFireball = false
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
