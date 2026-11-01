using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống ngắm bắn và kích hoạt Kỹ năng Boomerang (Phi Tiêu Hồi Toàn).
/// Hoàn toàn Burst-compiled, tự động Bootstrap thực thể Skill nếu chưa có trong Scene.
/// </summary>
[BurstCompile]
public partial struct BoomerangSkillCastSystem : ISystem
{
    private EntityQuery _enemyQuery;
    private EntityQuery _playerQuery;
    private EntityQuery _skillQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();

        _enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, LocalTransform>()
            .Build();

        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform, MovementData>()
            .Build();

        _skillQuery = SystemAPI.QueryBuilder()
            .WithAll<BoomerangSkillTag, SkillRuntimeData, BoomerangSkillData>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Tự động Bootstrap Skill Entity nếu chưa có trong Scene (mặc định mở khóa Level 1 để test ngay)
        if (_skillQuery.IsEmpty)
        {
            if (!_playerQuery.IsEmpty)
            {
                Entity playerEntity = _playerQuery.GetSingletonEntity();
                Entity skillEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<BoomerangSkillTag>(skillEntity);
                state.EntityManager.AddComponentData(skillEntity, new SkillOwnerData { Owner = playerEntity });
                
                // Mặc định khởi tạo Level 1
                state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
                {
                    Cooldown = 8.0f,
                    Timer = 0.5f,
                    Level = 1,
                    SkillId = new FixedString32Bytes("boomerang")
                });

                state.EntityManager.AddComponentData(skillEntity, new BoomerangSkillData
                {
                    BoomerangPrefab = Entity.Null,
                    BaseDamage = 35f,
                    FlySpeed = 12.5f,
                    MaxDistance = 7.5f,
                    HitRadius = 0.55f,
                    CatchRadius = 1.4f,
                    DetectRange = 9.5f,
                    EmpoweredMultiplier = 1.6f,
                    BoomerangCount = 1
                });

                state.EntityManager.AddComponentData(skillEntity, new BoomerangLevelBalanceData
                {
                    CooldownLv1 = 8.0f,
                    CooldownLv2 = 8.0f,
                    CooldownLv3 = 6.0f,
                    CooldownLv4 = 5.5f,

                    DamageLv1 = 35f,
                    DamageLv2 = 55f,
                    DamageLv3 = 55f,
                    DamageLv4 = 80f,

                    HitRadiusLv1 = 0.55f,
                    HitRadiusLv2 = 0.85f,
                    HitRadiusLv3 = 0.85f,
                    HitRadiusLv4 = 0.95f,

                    CountLv1 = 1,
                    CountLv2 = 1,
                    CountLv3 = 2,
                    CountLv4 = 2
                });
            }
            return;
        }

        if (_playerQuery.IsEmpty) return;

        Entity player = _playerQuery.GetSingletonEntity();
        float3 playerPos3 = state.EntityManager.GetComponentData<LocalTransform>(player).Position;
        float2 playerPos = playerPos3.xy;
        MovementData playerMove = state.EntityManager.GetComponentData<MovementData>(player);
        float deltaTime = SystemAPI.Time.DeltaTime;

        var gridSingleton = SystemAPI.GetSingleton<SpatialGridSingleton>();
        var grid = gridSingleton.Grid;
        float cellSize = gridSingleton.CellSize;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (runtime, skillData, balance, skillEntity) in
                 SystemAPI.Query<RefRW<SkillRuntimeData>, RefRW<BoomerangSkillData>, RefRO<BoomerangLevelBalanceData>>()
                     .WithAll<BoomerangSkillTag>()
                     .WithEntityAccess())
        {
            int level = runtime.ValueRO.Level;
            if (level <= 0) continue; // Chưa học kỹ năng

            // Cập nhật thông số unmanaged theo Level từ BoomerangLevelBalanceData
            float currentCooldown = level switch
            {
                1 => balance.ValueRO.CooldownLv1,
                2 => balance.ValueRO.CooldownLv2,
                3 => balance.ValueRO.CooldownLv3,
                _ => balance.ValueRO.CooldownLv4
            };

            float currentDamage = level switch
            {
                1 => balance.ValueRO.DamageLv1,
                2 => balance.ValueRO.DamageLv2,
                3 => balance.ValueRO.DamageLv3,
                _ => balance.ValueRO.DamageLv4
            };

            float currentHitRadius = level switch
            {
                1 => balance.ValueRO.HitRadiusLv1,
                2 => balance.ValueRO.HitRadiusLv2,
                3 => balance.ValueRO.HitRadiusLv3,
                _ => balance.ValueRO.HitRadiusLv4
            };

            int currentCount = level switch
            {
                1 => balance.ValueRO.CountLv1,
                2 => balance.ValueRO.CountLv2,
                3 => balance.ValueRO.CountLv3,
                _ => balance.ValueRO.CountLv4
            };

            runtime.ValueRW.Cooldown = currentCooldown;
            skillData.ValueRW.BaseDamage = currentDamage;
            skillData.ValueRW.HitRadius = currentHitRadius;
            skillData.ValueRW.BoomerangCount = currentCount;

            // 1. Kiểm tra số lượng Boomerang hiện đang còn sống trên chiến trường của Skill này
            int activeBoomerangCount = 0;
            foreach (var proj in SystemAPI.Query<RefRO<BoomerangProjectileData>>().WithAll<BoomerangTag>())
            {
                if (proj.ValueRO.SkillEntity == skillEntity)
                {
                    activeBoomerangCount++;
                }
            }

            // Nếu đã đủ số lượng phi tiêu tối đa đang bay trên chiến trường -> không cần bắn thêm
            if (activeBoomerangCount >= currentCount)
            {
                continue;
            }

            // Nếu đang thiếu phi tiêu (ví dụ bắn 2 cái mà hụt 1 cái, hoặc mới bắt đầu):
            // Tiếp tục đếm hồi chiêu để triệu hồi bổ sung chiếc phi tiêu bị mất!
            runtime.ValueRW.Timer += deltaTime;
            if (runtime.ValueRW.Timer < runtime.ValueRW.Cooldown) continue;

            // Đã hồi chiêu -> Bắn bổ sung đúng số lượng phi tiêu còn thiếu!
            runtime.ValueRW.Timer = 0f;
            int spawnNeeded = currentCount - activeBoomerangCount;

            // Tìm quái vật trong bán kính DetectRange qua Spatial Grid
            float detectRange = skillData.ValueRO.DetectRange;
            float detectRangeSq = detectRange * detectRange;
            int2 centerCell = SpatialGridUtility.ToCell(playerPos, cellSize);
            int cellRadius = (int)math.ceil(detectRange / cellSize);

            // Tìm tối đa 2 mục tiêu kẻ địch khác nhau
            Entity targetEnemy1 = Entity.Null;
            Entity targetEnemy2 = Entity.Null;
            float bestDistSq1 = float.MaxValue;
            float bestDistSq2 = float.MaxValue;
            float2 targetPos1 = float2.zero;
            float2 targetPos2 = float2.zero;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                int hash = SpatialGridUtility.HashCell(centerCell + new int2(dx, dy));
                if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                do
                {
                    if (!state.EntityManager.Exists(enemy) || !state.EntityManager.HasComponent<LocalTransform>(enemy))
                        continue;

                    float2 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position.xy;
                    float dSq = math.distancesq(playerPos, enemyPos);
                    if (dSq > detectRangeSq) continue;

                    if (dSq < bestDistSq1)
                    {
                        // Đẩy target cũ thành target thứ 2
                        targetEnemy2 = targetEnemy1;
                        bestDistSq2 = bestDistSq1;
                        targetPos2 = targetPos1;

                        targetEnemy1 = enemy;
                        bestDistSq1 = dSq;
                        targetPos1 = enemyPos;
                    }
                    else if (dSq < bestDistSq2 && enemy != targetEnemy1)
                    {
                        targetEnemy2 = enemy;
                        bestDistSq2 = dSq;
                        targetPos2 = enemyPos;
                    }
                } while (grid.TryGetNextValue(out enemy, ref it));
            }

            // Hướng mặc định nếu không có quái (theo hướng di chuyển hoặc hướng nhìn phải)
            float2 defaultDir = math.lengthsq(playerMove.Direction) > 0.01f ? math.normalize(playerMove.Direction) : new float2(1f, 0f);

            if (spawnNeeded == 1)
            {
                // Bắn 1 Boomerang (hoặc triệu hồi bổ sung 1 chiếc bị mất)
                float2 shootDir = targetEnemy1 != Entity.Null ? math.normalize(targetPos1 - playerPos) : defaultDir;
                SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, shootDir, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
            }
            else
            {
                // Bắn 2 Boomerang (Ưu tiên chia về 2 mục tiêu khác nhau)
                if (targetEnemy1 != Entity.Null && targetEnemy2 != Entity.Null)
                {
                    // Có 2 quái riêng biệt -> Bắn thẳng vào 2 quái đó!
                    float2 dir1 = math.normalize(targetPos1 - playerPos);
                    float2 dir2 = math.normalize(targetPos2 - playerPos);
                    SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, dir1, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
                    SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, dir2, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
                }
                else if (targetEnemy1 != Entity.Null)
                {
                    // Chỉ có 1 quái -> Bắn hơi lệch góc 25 độ (+12.5° và -12.5°)
                    float2 baseDir = math.normalize(targetPos1 - playerPos);
                    float2 dir1 = RotateVector(baseDir, math.radians(-12.5f));
                    float2 dir2 = RotateVector(baseDir, math.radians(12.5f));
                    SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, dir1, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
                    SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, dir2, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
                }
                else
                {
                    // Không có quái -> Bắn lệch góc 30 độ theo hướng di chuyển (+15° và -15°)
                    float2 dir1 = RotateVector(defaultDir, math.radians(-15f));
                    float2 dir2 = RotateVector(defaultDir, math.radians(15f));
                    SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, dir1, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
                    SpawnBoomerang(ref ecb, player, skillEntity, playerPos3, dir2, skillData.ValueRO, currentDamage, currentHitRadius, level, false);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    public static void SpawnBoomerang(
        ref EntityCommandBuffer ecb,
        Entity player,
        Entity skillEntity,
        float3 spawnPos,
        float2 direction,
        in BoomerangSkillData skillData,
        float damage,
        float hitRadius,
        int skillLevel,
        bool isEmpowered)
    {
        Entity b;
        if (skillData.BoomerangPrefab != Entity.Null)
        {
            // Nếu có Prefab được bake từ SubScene (Boomerang.prefab), instantiate prefab entity
            b = ecb.Instantiate(skillData.BoomerangPrefab);
            ecb.SetComponent(b, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, 1f));
            ecb.SetComponent(b, new BoomerangProjectileData
            {
                State = BoomerangFlightState.Outward,
                Direction = direction,
                Speed = skillData.FlySpeed,
                Damage = damage,
                HitRadius = hitRadius,
                DistanceTraveled = 0f,
                MaxDistance = skillData.MaxDistance,
                ReturnDistanceTraveled = 0f,
                CatchRadius = skillData.CatchRadius,
                IsEmpowered = isEmpowered,
                SkillLevel = skillLevel,
                Owner = player,
                SkillEntity = skillEntity,
                RotationAngle = 0f
            });
        }
        else
        {
            // Cơ chế tự động procedural: Tự sinh entity thuần túy trong RAM
            b = ecb.CreateEntity();
            ecb.AddComponent<BoomerangTag>(b);
            ecb.AddComponent(b, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, 1f));
            ecb.AddComponent(b, new BoomerangProjectileData
            {
                State = BoomerangFlightState.Outward,
                Direction = direction,
                Speed = skillData.FlySpeed,
                Damage = damage,
                HitRadius = hitRadius,
                DistanceTraveled = 0f,
                MaxDistance = skillData.MaxDistance,
                ReturnDistanceTraveled = 0f,
                CatchRadius = skillData.CatchRadius,
                IsEmpowered = isEmpowered,
                SkillLevel = skillLevel,
                Owner = player,
                SkillEntity = skillEntity,
                RotationAngle = 0f
            });
            ecb.AddBuffer<BoomerangHitBuffer>(b);
        }
    }

    private static float2 RotateVector(float2 v, float angleRad)
    {
        float cos = math.cos(angleRad);
        float sin = math.sin(angleRad);
        return new float2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}

