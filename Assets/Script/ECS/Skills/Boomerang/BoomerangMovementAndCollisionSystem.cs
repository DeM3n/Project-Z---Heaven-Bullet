using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều khiển chuyển động bay, cơ chế xuyên thấu quái,
/// quay đầu theo hướng đón đầu của người chơi (không homing)
/// và xử lý cơ chế Bắt Dính (Catch) giảm 25% hồi chiêu / phản xạ Cường Hóa ở Cấp 4.
/// </summary>
[BurstCompile]
public partial struct BoomerangMovementAndCollisionSystem : ISystem
{
    private EntityQuery _playerQuery;
    private EntityQuery _boomerangQuery;
    private EntityQuery _fusionQuery;
    private BufferLookup<EnemyIncomingDamage> _incomingDamageLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();

        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform, MovementData>()
            .Build();

        _boomerangQuery = SystemAPI.QueryBuilder()
            .WithAll<BoomerangTag, LocalTransform, BoomerangProjectileData>()
            .Build();

        _fusionQuery = SystemAPI.QueryBuilder()
            .WithAll<ThunderBoomerangFusionTag, SkillRuntimeData>()
            .Build();

        _incomingDamageLookup = state.GetBufferLookup<EnemyIncomingDamage>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty || _boomerangQuery.IsEmpty) return;

        Entity player = _playerQuery.GetSingletonEntity();
        LocalTransform playerTransform = state.EntityManager.GetComponentData<LocalTransform>(player);
        MovementData playerMove = state.EntityManager.GetComponentData<MovementData>(player);
        float2 playerPos = playerTransform.Position.xy;

        var gridSingleton = SystemAPI.GetSingleton<SpatialGridSingleton>();
        var grid = gridSingleton.Grid;
        float cellSize = gridSingleton.CellSize;
        float dt = SystemAPI.Time.DeltaTime;
        bool isThunderFusion = !_fusionQuery.IsEmpty && _fusionQuery.GetSingleton<SkillRuntimeData>().Level >= 1;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        _incomingDamageLookup.Update(ref state);

        foreach (var (transform, projData, hitBuffer, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<BoomerangProjectileData>, DynamicBuffer<BoomerangHitBuffer>>()
                     .WithAll<BoomerangTag>()
                     .WithEntityAccess())
        {
            // 1. Cập nhật góc tự xoay tròn với tốc độ cao (1080 độ/s)
            projData.ValueRW.RotationAngle += 1080f * dt;
            transform.ValueRW.Rotation = quaternion.RotateZ(math.radians(projData.ValueRO.RotationAngle));

            // =========================================================================
            // PHA 1: BAY TỚI (OUTWARD) - XUYÊN QUA QUÁI & CHỜ ĐẠT CỰ LY QUAY ĐẦU
            // =========================================================================
            if (projData.ValueRO.State == BoomerangFlightState.Outward)
            {
                float2 moveStep = projData.ValueRO.Direction * projData.ValueRO.Speed * dt;
                transform.ValueRW.Position.xy += moveStep;
                projData.ValueRW.DistanceTraveled += math.length(moveStep);

                // Quét va chạm gây sát thương xuyên thấu qua Spatial Grid
                CheckPiercingCollision(ref state, ref ecb, ref _incomingDamageLookup, grid, cellSize, transform.ValueRO.Position.xy, projData.ValueRO.HitRadius, projData.ValueRO.Damage, hitBuffer);

                // Khi bay đủ cự ly tối đa -> Đổi chiều quay về
                if (projData.ValueRO.DistanceTraveled >= projData.ValueRO.MaxDistance)
                {
                    projData.ValueRW.State = BoomerangFlightState.Returning;
                    hitBuffer.Clear(); // Xóa danh sách trúng đòn để có thể chém lại quái trên đường bay về!

                    // --- TÍNH TOÁN HƯỚNG BAY VỀ THEO HƯỚNG DI CHUYỂN CỦA PLAYER (KHÔNG HOMING) ---
                    float distToPlayer = math.distance(transform.ValueRO.Position.xy, playerPos);
                    float flightTime = distToPlayer / math.max(1f, projData.ValueRO.Speed);
                    float2 playerVelocity = playerMove.Direction * playerMove.MoveSpeed;
                    
                    // Điểm đón đầu dự kiến dựa trên vận tốc hiện tại của Player
                    float2 leadTargetPos = playerPos + playerVelocity * (flightTime * 0.7f);
                    float2 returnDir = leadTargetPos - transform.ValueRO.Position.xy;

                    if (math.lengthsq(returnDir) < 0.001f)
                    {
                        returnDir = -projData.ValueRO.Direction;
                    }
                    else
                    {
                        returnDir = math.normalize(returnDir);
                    }

                    // Hướng bay về được CỐ ĐỊNH từ thời điểm này, KHÔNG tự động bám đuổi (no continuous homing)
                    projData.ValueRW.Direction = returnDir;
                }
            }
            // =========================================================================
            // PHA 2: QUAY VỀ (RETURNING) - KIỂM TRA BẮT DÍNH (CATCH) HOẶC HẾT CỰ LY
            // =========================================================================
            else
            {
                float2 moveStep = projData.ValueRO.Direction * projData.ValueRO.Speed * dt;
                transform.ValueRW.Position.xy += moveStep;
                projData.ValueRW.ReturnDistanceTraveled += math.length(moveStep);

                // Tiếp tục xuyên thấu và chém quái trên đường quay về
                CheckPiercingCollision(ref state, ref ecb, ref _incomingDamageLookup, grid, cellSize, transform.ValueRO.Position.xy, projData.ValueRO.HitRadius, projData.ValueRO.Damage, hitBuffer);

                // --- KIỂM TRA NGƯỜI CHƠI BẮT DÍNH (CATCH) PHI TIÊU ---
                float distToPlayerSq = math.distancesq(transform.ValueRO.Position.xy, playerPos);
                float catchRadius = projData.ValueRO.CatchRadius;

                if (distToPlayerSq <= catchRadius * catchRadius)
                {
                    // >>> BẮT DÍNH THÀNH CÔNG! <<<
                    Entity skillEntity = projData.ValueRO.SkillEntity;

                    // Sinh Event cho Visual Bridge (hiệu ứng bùng nổ vòng hào quang quanh Player)
                    Entity catchEventEntity = ecb.CreateEntity();
                    ecb.AddComponent(catchEventEntity, new BoomerangCatchEvent
                    {
                        Position = playerTransform.Position,
                        IsEmpowered = (projData.ValueRO.SkillLevel >= 4)
                    });

                    if (state.EntityManager.Exists(skillEntity) && state.EntityManager.HasComponent<SkillRuntimeData>(skillEntity))
                    {
                        var runtime = state.EntityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                        var skillData = state.EntityManager.GetComponentData<BoomerangSkillData>(skillEntity);

                        if (projData.ValueRO.SkillLevel < 4)
                        {
                            // Cấp 1, 2, 3: Bắt dính giúp giảm 25% hồi chiêu hiện tại
                            runtime.Timer = math.min(runtime.Cooldown, runtime.Timer + runtime.Cooldown * 0.25f);
                            ecb.SetComponent(skillEntity, runtime);
                        }
                        else
                        {
                            // Cấp 4: "khi chụp thành công boomerang thì lập tức bắn lại cái boomerang đó, reset cooldown ngay lập tức, đòn đó gây thêm sát thương"
                            // Nếu đang thiếu phi tiêu (hụt 1 cái), bắt dính phi tiêu này vẫn giúp giảm 25% cooldown để phi tiêu bị mất hồi nhanh hơn!
                            runtime.Timer = math.min(runtime.Cooldown, runtime.Timer + runtime.Cooldown * 0.25f);
                            ecb.SetComponent(skillEntity, runtime);

                            // Lập tức bắn lại 1 chiếc Boomerang Cường Hóa (Empowered Boomerang) với +60% Sát Thương
                            float empoweredDamage = skillData.BaseDamage * skillData.EmpoweredMultiplier;
                            
                            // Hướng bắn: Tìm quái gần nhất hoặc bắn theo hướng di chuyển hiện tại của Player
                            float2 reboundDir = FindNearestEnemyDirection(ref state, grid, cellSize, playerPos, skillData.DetectRange);
                            if (math.lengthsq(reboundDir) < 0.001f)
                            {
                                reboundDir = math.lengthsq(playerMove.Direction) > 0.01f ? math.normalize(playerMove.Direction) : new float2(1f, 0f);
                            }

                            BoomerangSkillCastSystem.SpawnBoomerang(
                                ref ecb,
                                player,
                                skillEntity,
                                playerTransform.Position,
                                reboundDir,
                                skillData,
                                empoweredDamage,
                                projData.ValueRO.HitRadius,
                                projData.ValueRO.SkillLevel,
                                true, // IsEmpowered = true!
                                isThunderFusion
                            );
                        }
                    }

                    // Hủy chiếc Boomerang đang quay về vừa được chụp
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Nếu người chơi chụp hụt: Cho phi tiêu tiếp tục bay xa vượt qua Player (22.5m, bay xuyên ra khỏi màn hình) rồi mới hủy
                if (projData.ValueRO.ReturnDistanceTraveled >= projData.ValueRO.MaxDistance * 3.0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private static void CheckPiercingCollision(
        ref SystemState state,
        ref EntityCommandBuffer ecb,
        ref BufferLookup<EnemyIncomingDamage> incomingDamageLookup,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float2 currentPos,
        float hitRadius,
        float damage,
        DynamicBuffer<BoomerangHitBuffer> hitBuffer)
    {
        int2 cell = SpatialGridUtility.ToCell(currentPos, cellSize);
        float hitDistSq = hitRadius * hitRadius;

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

                // Kiểm tra xem quái này đã bị chém trong đợt bay hiện tại chưa
                bool alreadyHit = false;
                for (int i = 0; i < hitBuffer.Length; i++)
                {
                    if (hitBuffer[i].Target == enemy)
                    {
                        alreadyHit = true;
                        break;
                    }
                }
                if (alreadyHit) continue;

                float2 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position.xy;
                if (math.distancesq(currentPos, enemyPos) <= hitDistSq)
                {
                    // Đánh dấu đã chém quái để không gây sát thương liên tục nhiều frame
                    hitBuffer.Add(new BoomerangHitBuffer { Target = enemy });

                    // Gây sát thương quái qua Pipeline
                    EnemyDamage.Add(ref incomingDamageLookup, enemy, SkillIds.Boomerang, damage);
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }
    }

    private static float2 FindNearestEnemyDirection(
        ref SystemState state,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float2 centerPos,
        float range)
    {
        float bestDistSq = range * range;
        float2 bestEnemyPos = float2.zero;
        bool found = false;

        int2 centerCell = SpatialGridUtility.ToCell(centerPos, cellSize);
        int cellRadius = (int)math.ceil(range / cellSize);

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            int hash = SpatialGridUtility.HashCell(centerCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy) || !state.EntityManager.HasComponent<LocalTransform>(enemy))
                    continue;

                float2 ePos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position.xy;
                float dSq = math.distancesq(centerPos, ePos);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestEnemyPos = ePos;
                    found = true;
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }

        if (found)
        {
            return math.normalize(bestEnemyPos - centerPos);
        }
        return float2.zero;
    }
}

