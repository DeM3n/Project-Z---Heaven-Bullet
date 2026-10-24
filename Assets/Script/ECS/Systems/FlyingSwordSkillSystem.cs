using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct FlyingSwordSkillSystem : ISystem
{
    private EntityQuery _swordQuery;
    private EntityQuery _enemyQuery;
    private EntityQuery _enemyProjectileQuery;

    public void OnCreate(ref SystemState state)
    {
        _swordQuery = SystemAPI.QueryBuilder()
            .WithAll<FlyingSwordTag, FlyingSwordData, LocalTransform>()
            .Build();

        _enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, LocalTransform>()
            .Build();

        _enemyProjectileQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyProjectileTag, LocalTransform>()
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
                 SystemAPI.Query<RefRO<SkillRuntimeData>, RefRO<SkillOwnerData>, RefRO<FlyingSwordSkillData>>()
                     .WithAll<FlyingSwordSkillTag>()
                     .WithEntityAccess())
        {
            if (!state.EntityManager.Exists(owner.ValueRO.Owner)) continue;

            int targetSwordCount = math.clamp(runtime.ValueRO.Level, 1, 4);

            int currentSwordCount = 0;
            var swordEntities = _swordQuery.ToEntityArray(Allocator.Temp);
            var swordDataArray = _swordQuery.ToComponentDataArray<FlyingSwordData>(Allocator.Temp);

            for (int i = 0; i < swordEntities.Length; i++)
            {
                if (swordDataArray[i].SkillEntity == skillEntity)
                {
                    currentSwordCount++;
                }
            }

            if (currentSwordCount < targetSwordCount)
            {
                for (int i = currentSwordCount; i < targetSwordCount; i++)
                {
                    Entity sword = ecb.Instantiate(skillData.ValueRO.SwordPrefab);
                    ecb.SetComponent(sword, LocalTransform.FromPosition(playerPos));
                    ecb.SetComponent(sword, new FlyingSwordData
                    {
                        SkillEntity = skillEntity,
                        State = 0, // Orbiting (IDLE)
                        CurrentAngle = (math.PI * 2f / targetSwordCount) * i,
                        Timer = 0.2f * i,
                        SwordIndex = i,
                        TotalSwords = targetSwordCount,
                        UltimateTimer = 0f,
                        IsUltimate = false,
                        LastTarget = Entity.Null,
                        LoopDirection = (i % 2 == 0) ? 1 : -1
                    });
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

        // --- 2. Cập nhật trạng thái và chuyển động của từng thanh kiếm ---
        foreach (var (transform, sword, hitBuffer, swordEntity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<FlyingSwordData>, DynamicBuffer<SwordHitTargetBuffer>>()
                     .WithAll<FlyingSwordTag>()
                     .WithEntityAccess())
        {
            if (!state.EntityManager.Exists(sword.ValueRO.SkillEntity))
            {
                ecb.DestroyEntity(swordEntity);
                continue;
            }

            var skillData = state.EntityManager.GetComponentData<FlyingSwordSkillData>(sword.ValueRO.SkillEntity);
            var runtime = state.EntityManager.GetComponentData<SkillRuntimeData>(sword.ValueRO.SkillEntity);
            int level = runtime.Level;

            float currentDamage = skillData.BaseDamage * (1f + (level - 1) * 0.35f);
            float currentRushSpeed = skillData.RushSpeed * (1f + (level - 1) * 0.2f);
            float maxLeashSq = skillData.MaxLeashRange * skillData.MaxLeashRange;
            float detectRangeSq = skillData.DetectRange * skillData.DetectRange;

            // Đếm lùi cooldown của Ultimate Level 4
            if (sword.ValueRO.UltimateTimer > 0f)
            {
                sword.ValueRW.UltimateTimer -= dt;
            }

            // Đồng bộ kích thước kiếm theo trạng thái Ultimate
            if (sword.ValueRO.IsUltimate)
            {
                transform.ValueRW.Scale = 1.75f; // Cú lướt Ultimate: Kiếm hóa to khổng lồ
            }
            else if (level >= 4 && sword.ValueRO.UltimateTimer <= 0f)
            {
                transform.ValueRW.Scale = 1.25f; // Ultimate đã sẵn sàng: Kiếm sáng rực và hơi to hơn
            }
            else
            {
                transform.ValueRW.Scale = 1.0f;  // Trạng thái bình thường
            }

            switch (sword.ValueRO.State)
            {
                case 0: // ================== PHA 1: IDLE (Orbiting quanh Player) ==================
                {
                    // Kiếm chỉ xoay quanh Player khi không có quái hoặc đang chờ mục tiêu
                    sword.ValueRW.CurrentAngle += skillData.OrbitSpeed * dt;
                    if (sword.ValueRW.CurrentAngle > math.PI * 2f)
                        sword.ValueRW.CurrentAngle -= math.PI * 2f;

                    float angleOffset = (math.PI * 2f / math.max(1, sword.ValueRO.TotalSwords)) * sword.ValueRO.SwordIndex;
                    float finalAngle = sword.ValueRO.CurrentAngle + angleOffset;

                    float3 desiredPos = playerPos + new float3(
                        math.cos(finalAngle) * skillData.OrbitRadius,
                        math.sin(finalAngle) * skillData.OrbitRadius,
                        0f);

                    transform.ValueRW.Position = math.lerp(transform.ValueRO.Position, desiredPos, math.clamp(dt * 20f, 0f, 1f));

                    float tangentAngle = finalAngle + math.PI * 0.5f;
                    transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, tangentAngle - math.PI * 0.5f);

                    // Quét tìm quái trong tầm camera (DetectRange) quanh Player
                    sword.ValueRW.Timer -= dt;
                    if (sword.ValueRW.Timer <= 0f)
                    {
                        sword.ValueRW.Timer = 0.15f; // Quét mỗi 0.15s

                        bool canUseUltimate = (level >= 4 && sword.ValueRO.UltimateTimer <= 0f);
                        bool foundTarget = false;
                        float3 targetPos = float3.zero;
                        bool isUltimatePass = false;

                        if (canUseUltimate)
                        {
                            foundTarget = TryFindDensestCluster(ref state, grid, cellSize, playerPos, skillData.DetectRange, out targetPos);
                            if (foundTarget) isUltimatePass = true;
                        }

                        Entity targetEnemy = Entity.Null;
                        if (!foundTarget)
                        {
                            foundTarget = TryFindEnemyInCamera(ref state, grid, cellSize, transform.ValueRO.Position, playerPos, skillData.DetectRange, Entity.Null, out targetEnemy, out targetPos);
                        }

                        if (foundTarget)
                        {
                            // Xuất kích vào COMBAT!
                            float3 toTarget = targetPos - transform.ValueRO.Position;
                            float3 dir = math.normalizesafe(toTarget);

                            sword.ValueRW.TargetPosition = targetPos + dir * 1.2f;
                            sword.ValueRW.State = 1; // Rushing
                            sword.ValueRW.Timer = 0.8f;
                            sword.ValueRW.IsUltimate = isUltimatePass;
                            sword.ValueRW.LastTarget = targetEnemy;
                            hitBuffer.Clear();
                        }
                    }
                    break;
                }

                case 1: // ================== PHA 2: COMBAT (Lao liên tục từ quái này sang quái khác) ==================
                {
                    sword.ValueRW.Timer -= dt;
                    float3 toTarget = sword.ValueRO.TargetPosition - transform.ValueRO.Position;
                    float dist = math.length(toTarget);
                    float3 dir = dist > 0.001f ? (toTarget / dist) : float3.zero;

                    float step = currentRushSpeed * dt;
                    transform.ValueRW.Position += dir * math.min(step, dist);

                    if (dist > 0.01f)
                    {
                        transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, math.atan2(dir.y, dir.x) - math.PI * 0.5f);
                    }

                    // Sát thương xuyên thấu (Piercing) quét qua Spatial Grid 3x3
                    int2 cell = SpatialGridUtility.ToCell(transform.ValueRO.Position.xy, cellSize);
                    float hitDistSq = skillData.HitRadius * skillData.HitRadius;

                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int hash = SpatialGridUtility.HashCell(cell + new int2(dx, dy));
                        if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                        do
                        {
                            if (!state.EntityManager.Exists(enemy)) continue;
                            if (!state.EntityManager.HasComponent<LocalTransform>(enemy) || !state.EntityManager.HasComponent<HealthData>(enemy)) continue;

                            bool alreadyHit = false;
                            for (int b = 0; b < hitBuffer.Length; b++)
                            {
                                if (hitBuffer[b].Target == enemy)
                                {
                                    alreadyHit = true;
                                    break;
                                }
                            }
                            if (alreadyHit) continue;

                            float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                            if (math.distancesq(transform.ValueRO.Position, enemyPos) <= hitDistSq)
                            {
                                HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                                hp.CurrentHP -= currentDamage;
                                ecb.SetComponent(enemy, hp);

                                hitBuffer.Add(new SwordHitTargetBuffer { Target = enemy });
                                sword.ValueRW.LastTarget = enemy; // Ghi nhận mục tiêu vừa đâm trúng
                            }
                        } while (grid.TryGetNextValue(out enemy, ref it));
                    }

                    // Khi chém xong mục tiêu hiện tại (hoặc hết timeout lượt lướt)
                    if (dist <= 0.35f || sword.ValueRO.Timer <= 0f)
                    {
                        // 1. Kích hoạt vụ nổ AOE & phá đạn nếu đây là đòn Ultimate
                        if (sword.ValueRO.IsUltimate)
                        {
                            TriggerLevel4Aoe(ref state, ecb, grid, cellSize, transform.ValueRO.Position, skillData.AoeRadius, skillData.AoeDamage);
                            sword.ValueRW.UltimateTimer = skillData.UltimateCooldown; // Bắt đầu tính cooldown riêng cho Ultimate
                            sword.ValueRW.IsUltimate = false;
                        }

                        // 2. Kiểm tra khoảng cách kiếm với Player (Tránh bay ra khỏi tầm màn hình/map)
                        float distToPlayerSq = math.distancesq(transform.ValueRO.Position, playerPos);
                        if (distToPlayerSq > maxLeashSq)
                        {
                            // Quá xa Player -> Thu hồi kiếm về
                            sword.ValueRW.State = 2; // Returning
                            sword.ValueRW.Timer = 1.5f;
                            break;
                        }

                        // 3. TIẾP TỤC TẤN CÔNG LIÊN TỤC:
                        // Ưu tiên 1: Đòn Ultimate Level 4 nếu hồi xong và có cụm đông
                        bool canUseUltimate = (level >= 4 && sword.ValueRO.UltimateTimer <= 0f);
                        bool foundUltimate = false;
                        float3 ultimatePos = float3.zero;

                        if (canUseUltimate)
                        {
                            foundUltimate = TryFindDensestCluster(ref state, grid, cellSize, playerPos, skillData.DetectRange, out ultimatePos);
                        }

                        if (foundUltimate)
                        {
                            float3 nextDir = math.normalizesafe(ultimatePos - transform.ValueRO.Position);
                            sword.ValueRW.TargetPosition = ultimatePos + nextDir * 1.2f;
                            sword.ValueRW.State = 1; // Tiếp tục Rushing Ultimate
                            sword.ValueRW.Timer = 0.8f;
                            sword.ValueRW.IsUltimate = true;
                            hitBuffer.Clear();
                            break;
                        }

                        // Ưu tiên 2: Tìm MỤC TIÊU MỚI (khác LastTarget vừa chém) trong tầm camera quanh Player
                        bool foundNewTarget = TryFindEnemyInCamera(
                            ref state, grid, cellSize, transform.ValueRO.Position, playerPos, skillData.DetectRange,
                            sword.ValueRO.LastTarget, out Entity newEnemy, out float3 nextTargetPos);

                        if (foundNewTarget)
                        {
                            // Chuyển hướng lướt tiếp sang mục tiêu MỚI ngay lập tức!
                            float3 nextDir = math.normalizesafe(nextTargetPos - transform.ValueRO.Position);
                            sword.ValueRW.TargetPosition = nextTargetPos + nextDir * 1.2f;
                            sword.ValueRW.State = 1; // Tiếp tục Rushing
                            sword.ValueRW.Timer = 0.8f;
                            sword.ValueRW.IsUltimate = false;
                            sword.ValueRW.LastTarget = newEnemy;
                            hitBuffer.Clear();
                        }
                        else
                        {
                            // Không có quái mới nào khác. Kiểm tra xem LastTarget có còn sống và trong tầm camera không
                            if (IsEnemyValidInCamera(ref state, sword.ValueRO.LastTarget, playerPos, skillData.DetectRange, out float3 lastEnemyPos))
                            {
                                // CHỈ CÒN 1 MỤC TIÊU DUY NHẤT:
                                // Hạn chế ping-pong giật lùi -> Chuyển sang bay vòng cung (State 3: Looping) lấy góc tấn công mới
                                float3 toEnemy = lastEnemyPos - transform.ValueRO.Position;
                                float distToEnemy = math.length(toEnemy);
                                float3 forward = distToEnemy > 0.01f ? (toEnemy / distToEnemy) : new float3(1f, 0f, 0f);

                                int loopSign = sword.ValueRO.LoopDirection != 0 ? -sword.ValueRO.LoopDirection : 1;
                                sword.ValueRW.LoopDirection = loopSign;

                                float3 tangent = new float3(-forward.y, forward.x, 0f) * loopSign;
                                // Waypoint vòng cung: cách quái ~1.8m về phía tiếp tuyến bên hông và lùi lại nhẹ để tạo đường cong elip
                                float3 arcWaypoint = lastEnemyPos + tangent * 1.8f - forward * 0.4f;

                                sword.ValueRW.TargetPosition = arcWaypoint;
                                sword.ValueRW.State = 3; // Looping (Bay vòng lấy góc)
                                sword.ValueRW.Timer = 0.45f;
                                sword.ValueRW.IsUltimate = false;
                                hitBuffer.Clear();
                            }
                            else
                            {
                                // Không còn quái nào trong camera -> Quay về Player vào IDLE
                                sword.ValueRW.State = 2; // Returning
                                sword.ValueRW.Timer = 1.5f;
                                sword.ValueRW.LastTarget = Entity.Null;
                            }
                        }
                    }
                    break;
                }

                case 2: // ================== PHA 3: RETURNING (Bay về Player) ==================
                {
                    sword.ValueRW.Timer -= dt;
                    float3 toPlayer = playerPos - transform.ValueRO.Position;
                    float distToPlayer = math.length(toPlayer);

                    if (distToPlayer > skillData.OrbitRadius)
                    {
                        float3 dir = toPlayer / distToPlayer;
                        transform.ValueRW.Position += dir * skillData.ReturnSpeed * dt;
                        transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, math.atan2(dir.y, dir.x) - math.PI * 0.5f);
                    }

                    // Nếu trên đường bay về có quái xuất hiện trong camera -> có thể quay lại tấn công ngay
                    bool foundEnemyOnReturn = TryFindEnemyInCamera(ref state, grid, cellSize, transform.ValueRO.Position, playerPos, skillData.DetectRange, sword.ValueRO.LastTarget, out Entity enemyOnReturn, out float3 targetPosOnReturn);
                    if (!foundEnemyOnReturn)
                    {
                        foundEnemyOnReturn = TryFindEnemyInCamera(ref state, grid, cellSize, transform.ValueRO.Position, playerPos, skillData.DetectRange, Entity.Null, out enemyOnReturn, out targetPosOnReturn);
                    }

                    if (foundEnemyOnReturn && distToPlayer <= skillData.MaxLeashRange)
                    {
                        float3 dir = math.normalizesafe(targetPosOnReturn - transform.ValueRO.Position);
                        sword.ValueRW.TargetPosition = targetPosOnReturn + dir * 1.2f;
                        sword.ValueRW.State = 1; // Trở lại Rushing
                        sword.ValueRW.Timer = 0.8f;
                        sword.ValueRW.IsUltimate = false;
                        sword.ValueRW.LastTarget = enemyOnReturn;
                        hitBuffer.Clear();
                        break;
                    }

                    // Khi đã về đến bán kính hộ thể -> Trở về trạng thái IDLE
                    if (distToPlayer <= skillData.OrbitRadius || sword.ValueRO.Timer <= 0f)
                    {
                        sword.ValueRW.State = 0; // Orbiting (IDLE)
                        sword.ValueRW.Timer = 0.1f;
                        hitBuffer.Clear();
                    }
                    break;
                }

                case 3: // ================== PHA 4: LOOPING (Bay vòng cung lấy góc mới quanh 1 mục tiêu duy nhất) ==================
                {
                    sword.ValueRW.Timer -= dt;
                    float3 toWaypoint = sword.ValueRO.TargetPosition - transform.ValueRO.Position;
                    float dist = math.length(toWaypoint);
                    float3 dir = dist > 0.001f ? (toWaypoint / dist) : float3.zero;

                    float swoopSpeed = currentRushSpeed * 0.85f;
                    float step = swoopSpeed * dt;
                    transform.ValueRW.Position += dir * math.min(step, dist);

                    if (dist > 0.01f)
                    {
                        transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, math.atan2(dir.y, dir.x) - math.PI * 0.5f);
                    }

                    // Kiểm tra nếu trong lúc lượn vòng có quái MỚI xuất hiện trong tầm camera -> ưu tiên chém quái mới
                    bool foundNewEnemy = TryFindEnemyInCamera(
                        ref state, grid, cellSize, transform.ValueRO.Position, playerPos, skillData.DetectRange,
                        sword.ValueRO.LastTarget, out Entity newEnemy, out float3 newEnemyPos);

                    if (foundNewEnemy)
                    {
                        float3 nextDir = math.normalizesafe(newEnemyPos - transform.ValueRO.Position);
                        sword.ValueRW.TargetPosition = newEnemyPos + nextDir * 1.2f;
                        sword.ValueRW.State = 1; // Rushing sang quái mới
                        sword.ValueRW.Timer = 0.8f;
                        sword.ValueRW.IsUltimate = false;
                        sword.ValueRW.LastTarget = newEnemy;
                        hitBuffer.Clear();
                        break;
                    }

                    // Khi hoàn thành vòng lượn (hoặc hết timer ~0.45s)
                    if (dist <= 0.35f || sword.ValueRO.Timer <= 0f)
                    {
                        // Quái mục tiêu còn sống và còn trong camera không?
                        if (IsEnemyValidInCamera(ref state, sword.ValueRO.LastTarget, playerPos, skillData.DetectRange, out float3 targetEnemyPos))
                        {
                            // Lao vào chém từ góc mới!
                            float3 attackDir = math.normalizesafe(targetEnemyPos - transform.ValueRO.Position);
                            sword.ValueRW.TargetPosition = targetEnemyPos + attackDir * 1.2f;
                            sword.ValueRW.State = 1; // Rushing
                            sword.ValueRW.Timer = 0.8f;
                            sword.ValueRW.IsUltimate = false;
                            hitBuffer.Clear();
                        }
                        else
                        {
                            // Quái đã chết -> Quay về Player
                            sword.ValueRW.State = 2; // Returning
                            sword.ValueRW.Timer = 1.5f;
                            sword.ValueRW.LastTarget = Entity.Null;
                        }
                    }
                    break;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    // --- Helper: Kích hoạt AOE Level 4 & Tiêu hủy đạn địch ---
    private void TriggerLevel4Aoe(
        ref SystemState state,
        EntityCommandBuffer ecb,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float3 centerPos,
        float aoeRadius,
        float aoeDamage)
    {
        float aoeRadiusSq = aoeRadius * aoeRadius;
        int aoeCellRadius = (int)math.ceil(aoeRadius / cellSize);
        int2 aoeCenterCell = SpatialGridUtility.ToCell(centerPos.xy, cellSize);

        // Sát thương AOE quái vật
        for (int dx = -aoeCellRadius; dx <= aoeCellRadius; dx++)
        for (int dy = -aoeCellRadius; dy <= aoeCellRadius; dy++)
        {
            int hash = SpatialGridUtility.HashCell(aoeCenterCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy)) continue;
                if (!state.EntityManager.HasComponent<LocalTransform>(enemy) || !state.EntityManager.HasComponent<HealthData>(enemy)) continue;
                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                if (math.distancesq(centerPos, enemyPos) <= aoeRadiusSq)
                {
                    HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                    hp.CurrentHP -= aoeDamage;
                    ecb.SetComponent(enemy, hp);
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        // Tiêu hủy 100% đạn địch (EnemyProjectileTag) trong bán kính AOE
        foreach (var (projTransform, projEntity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<EnemyProjectileTag>()
                     .WithEntityAccess())
        {
            if (math.distancesq(centerPos, projTransform.ValueRO.Position) <= aoeRadiusSq)
            {
                ecb.DestroyEntity(projEntity);
            }
        }

        // Tạo event để Hybrid Bridge render hiệu ứng Shockwave Hitbox trực quan
        Entity blastEvent = ecb.CreateEntity();
        ecb.AddComponent(blastEvent, new FlyingSwordAoeBlastEvent
        {
            Position = centerPos,
            Radius = aoeRadius
        });
    }

    // --- Helper: Quét Spatial Grid tìm cụm quái đông nhất TRONG TẦM CAMERA ---
    private static bool TryFindDensestCluster(
        ref SystemState state,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float3 playerPos,
        float maxSearchRadiusFromPlayer,
        out float3 bestClusterPos)
    {
        int2 playerCell = SpatialGridUtility.ToCell(playerPos.xy, cellSize);
        int cellRadius = (int)math.ceil(maxSearchRadiusFromPlayer / cellSize);
        float maxDistSq = maxSearchRadiusFromPlayer * maxSearchRadiusFromPlayer;
        int maxCount = 0;
        int2 bestCell = playerCell;
        bestClusterPos = playerPos;

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            int2 cell = playerCell + new int2(dx, dy);
            float2 cellCenter = (new float2(cell.x, cell.y) + 0.5f) * cellSize;
            if (math.distancesq(playerPos.xy, cellCenter) > maxDistSq) continue; // Bỏ qua nếu ô nằm ngoài tầm nhìn camera

            int hash = SpatialGridUtility.HashCell(cell);
            int count = grid.CountValuesForKey(hash);
            if (count > maxCount)
            {
                maxCount = count;
                bestCell = cell;
            }
        }

        if (maxCount > 0)
        {
            float2 cellCenter = (new float2(bestCell.x, bestCell.y) + 0.5f) * cellSize;
            bestClusterPos = new float3(cellCenter.x, cellCenter.y, 0f);
            return true;
        }

        return false;
    }

    // --- Helper: Tìm quái gần kiếm nhất (ưu tiên quái khác excludeEnemy) và BẮT BUỘC nằm trong tầm camera quanh Player ---
    private static bool TryFindEnemyInCamera(
        ref SystemState state,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float3 swordPos,
        float3 playerPos,
        float maxDistFromPlayer,
        Entity excludeEnemy,
        out Entity targetEnemy,
        out float3 targetPos)
    {
        int2 swordCell = SpatialGridUtility.ToCell(swordPos.xy, cellSize);
        int searchCellRadius = (int)math.ceil(maxDistFromPlayer / cellSize);
        float maxDistFromPlayerSq = maxDistFromPlayer * maxDistFromPlayer;

        float closestDistToSwordSq = float.MaxValue;
        targetEnemy = Entity.Null;
        targetPos = swordPos;
        bool found = false;

        for (int dx = -searchCellRadius; dx <= searchCellRadius; dx++)
        for (int dy = -searchCellRadius; dy <= searchCellRadius; dy++)
        {
            int hash = SpatialGridUtility.HashCell(swordCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy)) continue;
                if (enemy == excludeEnemy) continue; // Bỏ qua target vừa chém

                if (!state.EntityManager.HasComponent<HealthData>(enemy)) continue;
                if (state.EntityManager.GetComponentData<HealthData>(enemy).CurrentHP <= 0f) continue;

                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;

                // QUAN TRỌNG: Quái BẮT BUỘC phải nằm trong tầm camera quanh Player
                if (math.distancesq(enemyPos, playerPos) > maxDistFromPlayerSq) continue;

                float distToSwordSq = math.distancesq(swordPos, enemyPos);
                if (distToSwordSq < closestDistToSwordSq)
                {
                    closestDistToSwordSq = distToSwordSq;
                    targetEnemy = enemy;
                    targetPos = enemyPos;
                    found = true;
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        return found;
    }

    // --- Helper: Kiểm tra mục tiêu cũ còn hợp lệ, còn sống và nằm trong tầm camera quanh Player không ---
    private static bool IsEnemyValidInCamera(
        ref SystemState state,
        Entity enemy,
        float3 playerPos,
        float maxDistFromPlayer,
        out float3 enemyPos)
    {
        enemyPos = float3.zero;
        if (enemy == Entity.Null || !state.EntityManager.Exists(enemy)) return false;
        if (!state.EntityManager.HasComponent<HealthData>(enemy)) return false;
        if (state.EntityManager.GetComponentData<HealthData>(enemy).CurrentHP <= 0f) return false;
        if (!state.EntityManager.HasComponent<LocalTransform>(enemy)) return false;

        enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
        float maxDistSq = maxDistFromPlayer * maxDistFromPlayer;
        return math.distancesq(enemyPos, playerPos) <= maxDistSq;
    }
}
