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
                    FireZoneRadius = 2.5f,
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

            // Tính toán Cooldown và Sát thương scale theo cấp độ
            // Lv1: 2.6s, Lv2: 2.3s, Lv3: 2.0s, Lv4: 1.8s
            float scaledCooldown = level switch
            {
                2 => 2.3f,
                3 => 2.0f,
                4 => 1.8f,
                _ => 2.6f
            };
            runtime.ValueRW.Timer = scaledCooldown;

            // Lấy vị trí Player
            Entity playerEntity = owner.ValueRO.Owner;
            if (!state.EntityManager.Exists(playerEntity) || !state.EntityManager.HasComponent<LocalTransform>(playerEntity))
                continue;

            float3 playerPos = state.EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;

            // Tìm mục tiêu quái gần nhất qua Spatial Grid
            float detectRange = skill.ValueRO.DetectRange;
            float2 targetDir = new float2(1f, 0f); // Hướng mặc định nếu không có quái
            bool foundEnemy = TryFindTargetDirection(ref state, grid, cellSize, playerPos, detectRange, out targetDir);

            if (!foundEnemy)
            {
                // Nếu không có quái trong tầm, lấy hướng di chuyển của Player làm hướng bắn
                if (state.EntityManager.HasComponent<MovementData>(playerEntity))
                {
                    float2 moveDir = state.EntityManager.GetComponentData<MovementData>(playerEntity).Direction;
                    if (math.lengthsq(moveDir) > 0.01f)
                    {
                        targetDir = math.normalizesafe(moveDir);
                    }
                }
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

            // Spawn Fireball Projectile
            Entity fireball;
            if (skill.ValueRO.FireballPrefab != Entity.Null)
            {
                fireball = ecb.Instantiate(skill.ValueRO.FireballPrefab);
                ecb.SetComponent(fireball, LocalTransform.FromPosition(playerPos));
            }
            else
            {
                fireball = ecb.CreateEntity();
                ecb.AddComponent(fireball, LocalTransform.FromPosition(playerPos));
            }

            ecb.AddComponent<FireballTag>(fireball);
            ecb.AddComponent(fireball, new MovementData
            {
                Direction = targetDir,
                MoveSpeed = skill.ValueRO.FireballSpeed
            });

            ecb.AddComponent(fireball, new FireballProjectileData
            {
                Direction = targetDir,
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

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private bool TryFindTargetDirection(ref SystemState state, NativeParallelMultiHashMap<int, Entity> grid, float cellSize, float3 playerPos, float detectRange, out float2 targetDir)
    {
        int2 playerCell = SpatialGridUtility.ToCell(playerPos.xy, cellSize);
        int cellRadius = (int)math.ceil(detectRange / cellSize);
        float closestDistSq = detectRange * detectRange;
        float3 closestPos = playerPos;
        bool found = false;

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
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPos = enemyPos;
                    found = true;
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        if (found)
        {
            targetDir = math.normalizesafe((closestPos - playerPos).xy);
            return true;
        }

        // Fallback kiểm tra query tổng nếu quái nằm ngoài bán kính ô lân cận
        if (!_enemyQuery.IsEmpty)
        {
            var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
            {
                float distSq = math.distancesq(playerPos, transforms[i].Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPos = transforms[i].Position;
                    found = true;
                }
            }
            transforms.Dispose();

            if (found)
            {
                targetDir = math.normalizesafe((closestPos - playerPos).xy);
                return true;
            }
        }

        targetDir = new float2(1f, 0f);
        return false;
    }
}
