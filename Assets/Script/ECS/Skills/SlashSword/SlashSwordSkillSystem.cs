using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
public partial struct SlashSwordSkillSystem : ISystem
{
    private EntityQuery _swordQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
        state.RequireForUpdate<SpatialGridSingleton>();

        _swordQuery = SystemAPI.QueryBuilder()
            .WithAll<SlashSwordTag, SlashSwordData, LocalTransform>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!SystemAPI.HasSingleton<SpatialGridSingleton>()) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float dt = SystemAPI.Time.DeltaTime;

        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // --- 1. Đồng bộ số lượng kiếm theo Skill Level ---
        foreach (var (runtime, owner, skillData, skillEntity) in
                 SystemAPI.Query<RefRO<SkillRuntimeData>, RefRO<SkillOwnerData>, RefRO<SlashSwordSkillData>>()
                     .WithAll<SlashSwordSkillTag>()
                     .WithEntityAccess())
        {
            if (!state.EntityManager.Exists(owner.ValueRO.Owner)) continue;

            int targetSwordCount = runtime.ValueRO.Level <= 0 ? 0 : math.clamp(runtime.ValueRO.Level, 1, 3);

            int currentSwordCount = 0;
            var swordEntities = _swordQuery.ToEntityArray(Allocator.Temp);
            var swordDataArray = _swordQuery.ToComponentDataArray<SlashSwordData>(Allocator.Temp);

            for (int i = 0; i < swordEntities.Length; i++)
            {
                if (swordDataArray[i].SkillEntity == skillEntity)
                {
                    currentSwordCount++;
                }
            }

            if (currentSwordCount < targetSwordCount && skillData.ValueRO.SwordPrefab != Entity.Null)
            {
                for (int i = currentSwordCount; i < targetSwordCount; i++)
                {
                    Entity sword = ecb.Instantiate(skillData.ValueRO.SwordPrefab);
                    ecb.SetComponent(sword, LocalTransform.FromPosition(playerPos));
                    ecb.SetComponent(sword, new SlashSwordData
                    {
                        SkillEntity = skillEntity,
                        State = 0, // IdleOrbit
                        OrbitAngle = (math.PI * 2f / targetSwordCount) * i,
                        CooldownTimer = (i == 0) ? 0.2f : (i * 0.7f), // Kiếm đầu tiên kích hoạt sớm, các kiếm sau giãn nhịp
                        StateTimer = 0f,
                        TargetPosition = playerPos,
                        TargetEnemy = Entity.Null,
                        SlashDirection = new float3(1f, 0f, 0f),
                        SwordIndex = i,
                        TotalSwords = targetSwordCount,
                        SlashProgress = 0f
                    });
                }
            }
            else if (currentSwordCount > targetSwordCount)
            {
                int toRemove = currentSwordCount - targetSwordCount;
                for (int i = swordEntities.Length - 1; i >= 0 && toRemove > 0; i--)
                {
                    if (swordDataArray[i].SkillEntity == skillEntity)
                    {
                        ecb.DestroyEntity(swordEntities[i]);
                        toRemove--;
                    }
                }
            }

            for (int i = 0; i < swordEntities.Length; i++)
            {
                if (swordDataArray[i].SkillEntity == skillEntity && swordDataArray[i].TotalSwords != targetSwordCount)
                {
                    var data = swordDataArray[i];
                    data.TotalSwords = targetSwordCount;
                    ecb.SetComponent(swordEntities[i], data);
                }
            }

            swordEntities.Dispose();
            swordDataArray.Dispose();
        }

        // --- 2. Cập nhật State Machine cho từng thanh kiếm ---
        foreach (var (transform, sword, swordEntity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<SlashSwordData>>()
                     .WithAll<SlashSwordTag>()
                     .WithEntityAccess())
        {
            if (!state.EntityManager.Exists(sword.ValueRO.SkillEntity))
            {
                ecb.DestroyEntity(swordEntity);
                continue;
            }

            var skillData = state.EntityManager.GetComponentData<SlashSwordSkillData>(sword.ValueRO.SkillEntity);
            var runtime = state.EntityManager.GetComponentData<SkillRuntimeData>(sword.ValueRO.SkillEntity);
            int level = runtime.Level;

            // Tính toán chỉ số theo Level
            float currentCooldown = GetCooldownForLevel(level);
            float currentDamage = skillData.BaseDamage + (level - 1) * 32f;
            float currentSpeed = skillData.ApproachSpeed + (level - 1) * 3f;

            switch (sword.ValueRO.State)
            {
                case 0: // ================== PHA 1: IDLE ORBIT ==================
                {
                    // Kiếm bay lơ lửng bảo vệ quanh Player
                    sword.ValueRW.OrbitAngle += 2.0f * dt;
                    if (sword.ValueRW.OrbitAngle > math.PI * 2f)
                        sword.ValueRW.OrbitAngle -= math.PI * 2f;

                    float angleOffset = (math.PI * 2f / math.max(1, sword.ValueRO.TotalSwords)) * sword.ValueRO.SwordIndex;
                    float finalAngle = sword.ValueRO.OrbitAngle + angleOffset;

                    float3 desiredPos = playerPos + new float3(
                        math.cos(finalAngle) * 1.6f,
                        math.sin(finalAngle) * 1.6f,
                        0f);

                    transform.ValueRW.Position = math.lerp(transform.ValueRO.Position, desiredPos, math.clamp(dt * 15f, 0f, 1f));

                    // Hướng mũi kiếm xoay theo tiếp tuyến quỹ đạo
                    float tangentAngle = finalAngle + math.PI * 0.5f;
                    transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, tangentAngle - math.PI * 0.5f);

                    // Đếm lùi thời gian hồi chiêu
                    if (sword.ValueRO.CooldownTimer > 0f)
                    {
                        sword.ValueRW.CooldownTimer -= dt;
                    }
                    else
                    {
                        // Khi hết hồi chiêu, tìm quái trong tầm DetectRange quanh Player
                        if (TryFindTargetEnemy(ref state, grid, cellSize, playerPos, skillData.DetectRange, sword.ValueRO.SwordIndex, out Entity targetEnemy, out float3 targetEnemyPos))
                        {
                            sword.ValueRW.State = 1; // Approaching
                            sword.ValueRW.TargetEnemy = targetEnemy;
                            sword.ValueRW.TargetPosition = targetEnemyPos;
                            sword.ValueRW.StateTimer = 1.0f; // Timeout 1s
                        }
                    }
                    break;
                }

                case 1: // ================== PHA 2: APPROACHING ==================
                {
                    sword.ValueRW.StateTimer -= dt;

                    // Cập nhật vị trí mục tiêu nếu quái còn tồn tại
                    if (state.EntityManager.Exists(sword.ValueRO.TargetEnemy) &&
                        state.EntityManager.HasComponent<LocalTransform>(sword.ValueRO.TargetEnemy))
                    {
                        sword.ValueRW.TargetPosition = state.EntityManager.GetComponentData<LocalTransform>(sword.ValueRO.TargetEnemy).Position;
                    }

                    float3 toTarget = sword.ValueRO.TargetPosition - transform.ValueRO.Position;
                    float dist = math.length(toTarget);
                    float3 dir = dist > 0.001f ? (toTarget / dist) : float3.zero;

                    // Di chuyển lao về phía quái
                    transform.ValueRW.Position += dir * math.min(currentSpeed * dt, dist);

                    if (dist > 0.01f)
                    {
                        transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, math.atan2(dir.y, dir.x) - math.PI * 0.5f);
                    }

                    // Khi đã áp sát cự ly chém (<= 1.3m) hoặc timeout
                    if (dist <= 1.3f || sword.ValueRO.StateTimer <= 0f)
                    {
                        sword.ValueRW.State = 2; // Slashing
                        sword.ValueRW.SlashDirection = (dist > 0.001f) ? dir : new float3(1f, 0f, 0f);
                        sword.ValueRW.StateTimer = 0.22f; // Thời gian vung kiếm
                        sword.ValueRW.SlashProgress = 0f;

                        // Thực hiện đòn chém quét diện rộng
                        float3 slashCenter = transform.ValueRO.Position + sword.ValueRO.SlashDirection * 0.7f;
                        float slashRadius = skillData.SlashRadius;
                        float slashRadiusSq = slashRadius * slashRadius;

                        int2 cell = SpatialGridUtility.ToCell(slashCenter.xy, cellSize);

                        for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int hash = SpatialGridUtility.HashCell(cell + new int2(dx, dy));
                            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                            do
                            {
                                if (!state.EntityManager.Exists(enemy)) continue;
                                if (!state.EntityManager.HasComponent<LocalTransform>(enemy) ||
                                    !state.EntityManager.HasComponent<HealthData>(enemy)) continue;

                                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                                if (math.distancesq(slashCenter, enemyPos) <= slashRadiusSq)
                                {
                                    HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                                    hp.CurrentHP -= currentDamage;
                                    ecb.SetComponent(enemy, hp);
                                }
                            } while (grid.TryGetNextValue(out enemy, ref it));
                        }

                        // Sinh SlashVisualEvent để Hybrid Bridge hiển thị vệt chém bán nguyệt
                        Entity slashEventEntity = ecb.CreateEntity();
                        ecb.AddComponent(slashEventEntity, new SlashVisualEvent
                        {
                            Position = slashCenter,
                            Direction = sword.ValueRO.SlashDirection,
                            Radius = slashRadius
                        });

                        // CẤP 4: Bắn ra đòn Kiếm Khí xuyên thấu (Piercing Sword Wave)
                        if (level >= 4 && skillData.SwordWavePrefab != Entity.Null)
                        {
                            Entity waveEntity = ecb.Instantiate(skillData.SwordWavePrefab);
                            float rotZ = math.atan2(sword.ValueRO.SlashDirection.y, sword.ValueRO.SlashDirection.x) - math.PI * 0.5f;

                            ecb.SetComponent(waveEntity, LocalTransform.FromPositionRotationScale(
                                slashCenter,
                                quaternion.Euler(0f, 0f, rotZ),
                                1.0f
                            ));

                            ecb.SetComponent(waveEntity, new SwordWaveData
                            {
                                Direction = sword.ValueRO.SlashDirection.xy,
                                Speed = skillData.WaveSpeed,
                                Damage = skillData.WaveDamage,
                                Radius = skillData.WaveRadius,
                                Lifetime = skillData.WaveDuration,
                                MaxLifetime = skillData.WaveDuration
                            });
                        }
                    }
                    break;
                }

                case 2: // ================== PHA 3: SLASHING ==================
                {
                    sword.ValueRW.StateTimer -= dt;
                    sword.ValueRW.SlashProgress += dt / 0.22f;

                    // Kiếm vung quét một vòng cung từ -65° đến +65° tạo cảm giác chém uy lực
                    float sweepAngle = math.lerp(-1.15f, 1.15f, math.clamp(sword.ValueRO.SlashProgress, 0f, 1f));
                    float baseAngle = math.atan2(sword.ValueRO.SlashDirection.y, sword.ValueRO.SlashDirection.x) - math.PI * 0.5f;
                    transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, baseAngle + sweepAngle);

                    if (sword.ValueRO.StateTimer <= 0f || sword.ValueRO.SlashProgress >= 1f)
                    {
                        sword.ValueRW.State = 3; // Returning
                        sword.ValueRW.StateTimer = 1.2f;
                    }
                    break;
                }

                case 3: // ================== PHA 4: RETURNING ==================
                {
                    sword.ValueRW.StateTimer -= dt;

                    float3 toPlayer = playerPos - transform.ValueRO.Position;
                    float distToPlayer = math.length(toPlayer);
                    float3 dir = distToPlayer > 0.001f ? (toPlayer / distToPlayer) : float3.zero;

                    transform.ValueRW.Position += dir * math.min(skillData.ReturnSpeed * dt, distToPlayer);

                    if (distToPlayer > 0.01f)
                    {
                        transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, math.atan2(dir.y, dir.x) - math.PI * 0.5f);
                    }

                    // Khi đã quay về gần Player hoặc timeout
                    if (distToPlayer <= 1.8f || sword.ValueRO.StateTimer <= 0f)
                    {
                        sword.ValueRW.State = 0; // IdleOrbit
                        sword.ValueRW.CooldownTimer = currentCooldown; // Bắt đầu chu kỳ hồi chiêu mới
                    }
                    break;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private static float GetCooldownForLevel(int level)
    {
        switch (level)
        {
            case 1: return 4.0f; // Cấp 1: 4.0s
            case 2: return 3.3f;
            case 3: return 2.6f;
            default: return 2.0f; // Cấp 4: 2.0s
        }
    }

    private static bool TryFindTargetEnemy(
        ref SystemState state,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float3 playerPos,
        float detectRange,
        int swordIndex,
        out Entity bestEnemy,
        out float3 bestPos)
    {
        bestEnemy = Entity.Null;
        bestPos = float3.zero;
        float bestDistSq = detectRange * detectRange;

        int2 playerCell = SpatialGridUtility.ToCell(playerPos.xy, cellSize);
        int cellRadius = (int)math.ceil(detectRange / cellSize);

        // Quét các ô xung quanh Player
        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            int hash = SpatialGridUtility.HashCell(playerCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy)) continue;
                if (!state.EntityManager.HasComponent<LocalTransform>(enemy) ||
                    !state.EntityManager.HasComponent<HealthData>(enemy)) continue;

                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                float distSq = math.distancesq(playerPos, enemyPos);

                if (distSq <= bestDistSq)
                {
                    bestDistSq = distSq;
                    bestEnemy = enemy;
                    bestPos = enemyPos;
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        return bestEnemy != Entity.Null;
    }
}
