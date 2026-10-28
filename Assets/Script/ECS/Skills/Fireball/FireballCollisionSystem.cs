using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý va chạm và nổ AOE của Cầu Lửa (Fireball) & Cầu Lửa Con (Mortar Cluster Arc).
/// - Cầu Lửa Mẹ: Bay thẳng, chạm quái hoặc hết tầm thì phát nổ AOE.
/// - Cầu Lửa Con: Nổ chùm bắn vút lên không trung theo quỹ đạo hình Parabol (Mortar Lob) rồi cắm xuống đất nổ tiếp.
/// - Ở Cấp 4: Cầu Lửa Mẹ sinh thêm Vùng Lửa (Fire Zone) thiêu đốt DOT.
/// </summary>
[UpdateAfter(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct FireballCollisionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<SpatialGridSingleton>()) return;

        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, projData, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<FireballProjectileData>>()
                     .WithEntityAccess())
        {
            if (projData.ValueRO.IsSubFireball)
            {
                // =========================================================================
                // 1. CẦU LỬA CON (SUB-FIREBALL): BẮN VÚT LÊN TRỜI RỒI RƠI CẮM XUỐNG ĐẤT
                // =========================================================================
                projData.ValueRW.ElapsedTime += dt;
                float duration = math.max(0.01f, projData.ValueRO.TotalDuration);
                float t = math.saturate(projData.ValueRO.ElapsedTime / duration);

                // Tọa độ lướt trên mặt đất (Ground Plane)
                float2 groundPos = math.lerp(projData.ValueRO.StartPosition, projData.ValueRO.TargetPosition, t);

                // Độ cao theo hàm Parabol: h = 4 * H * t * (1 - t)
                // Đạt đỉnh cực đại tại t = 0.5, chạm đất tại t = 1.0
                float height = 4f * projData.ValueRO.ArcHeight * t * (1f - t);

                // Cập nhật vị trí không gian (Y được cộng thêm độ cao bay trên không)
                transform.ValueRW.Position = new float3(groundPos.x, groundPos.y + height, 0f);

                // Vector tiếp tuyến quỹ đạo để xoay đầu đạn chúc xuống khi rơi
                float2 travelVector = projData.ValueRO.TargetPosition - projData.ValueRO.StartPosition;
                float2 tangent = travelVector + new float2(0f, 4f * projData.ValueRO.ArcHeight * (1f - 2f * t));
                if (math.lengthsq(tangent) > 0.001f)
                {
                    float rotZ = math.atan2(tangent.y, tangent.x) - math.PI * 0.5f;
                    transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, rotZ);
                }

                // Khi chạm đất (t >= 1.0f): Phát nổ AOE!
                if (t >= 1f)
                {
                    float3 impactPos = new float3(projData.ValueRO.TargetPosition.x, projData.ValueRO.TargetPosition.y, 0f);
                    float explosionRadius = projData.ValueRO.ExplosionRadius;
                    float damage = projData.ValueRO.Damage;

                    // 1. Gây sát thương nổ phụ (50% DMG) quanh điểm tiếp đất
                    ApplyAoeDamage(ref state, ref ecb, grid, cellSize, impactPos, explosionRadius, damage);

                    // 2. Bắn Event cho Visual Bridge
                    Entity eventEntity = ecb.CreateEntity();
                    ecb.AddComponent(eventEntity, new FireballExplosionEvent
                    {
                        Position = impactPos,
                        Radius = explosionRadius,
                        IsMainExplosion = false
                    });

                    // 3. Hủy Entity cầu lửa con
                    ecb.DestroyEntity(entity);
                }
            }
            else
            {
                // =========================================================================
                // 2. CẦU LỬA MẸ (MAIN FIREBALL): BAY THẲNG & VA CHẠM QUÁI
                // =========================================================================
                projData.ValueRW.Lifetime -= dt;
                bool shouldDetonate = projData.ValueRO.Lifetime <= 0f;
                float3 currentPos = transform.ValueRO.Position;

                // Quét va chạm trực tiếp với quái qua Spatial Grid (3x3 ô)
                if (!shouldDetonate)
                {
                    int2 cell = SpatialGridUtility.ToCell(currentPos.xy, cellSize);
                    float hitDistSq = projData.ValueRO.HitRadius * projData.ValueRO.HitRadius;

                    for (int dx = -1; dx <= 1 && !shouldDetonate; dx++)
                    for (int dy = -1; dy <= 1 && !shouldDetonate; dy++)
                    {
                        int hash = SpatialGridUtility.HashCell(cell + new int2(dx, dy));
                        if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                        do
                        {
                            if (!state.EntityManager.Exists(enemy) || !state.EntityManager.HasComponent<LocalTransform>(enemy))
                                continue;

                            float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                            if (math.distancesq(currentPos, enemyPos) <= hitDistSq)
                            {
                                shouldDetonate = true;
                                break;
                            }
                        } while (grid.TryGetNextValue(out enemy, ref it));
                    }
                }

                // Xử lý nổ khi va chạm hoặc hết thời gian bay
                if (shouldDetonate)
                {
                    float explosionRadius = projData.ValueRO.ExplosionRadius;
                    float damage = projData.ValueRO.Damage;

                    // A. Gây sát thương nổ AOE cầu mẹ
                    ApplyAoeDamage(ref state, ref ecb, grid, cellSize, currentPos, explosionRadius, damage);

                    // B. Kích hoạt Nổ Chùm: Bắn các Cầu Lửa Con vút lên trời rồi rơi cắm đất
                    int subCount = math.max(1, projData.ValueRO.SubFireballCount);
                    float subDamage = damage * 0.5f; // Đúng 50% sát thương của cầu mẹ
                    float subRadius = 1.35f;

                    float baseAngleOffset = (currentPos.x * 12.9898f + currentPos.y * 78.233f) % (math.PI * 2f);
                    float angleStep = (math.PI * 2f) / subCount;

                    for (int i = 0; i < subCount; i++)
                    {
                        float angle = baseAngleOffset + angleStep * i;
                        float2 subDir = new float2(math.cos(angle), math.sin(angle));

                        // Điểm rơi tiếp đất cách tâm nổ từ 2.4m đến 3.6m
                        float landingDist = 2.4f + ((i * 1.414f) % 1.2f);
                        float2 landingPos = currentPos.xy + subDir * landingDist;

                        // Độ cao vút lên trời từ 2.8m đến 3.4m
                        float arcHeight = 2.8f + ((i * 0.618f) % 0.6f);

                        // Thời gian rơi từ 0.62s đến 0.74s (so le nhịp nổ lách tách)
                        float duration = 0.62f + ((i * 0.05f) % 0.12f);

                        Entity subFb = ecb.CreateEntity();
                        ecb.AddComponent<SubFireballTag>(subFb);
                        ecb.AddComponent(subFb, LocalTransform.FromPosition(currentPos));
                        // LƯU Ý: Không thêm MovementData để quỹ đạo Parabol toàn quyền kiểm soát vị trí
                        ecb.AddComponent(subFb, new FireballProjectileData
                        {
                            Direction = subDir,
                            Speed = 0f,
                            Damage = subDamage,
                            ExplosionRadius = subRadius,
                            Lifetime = duration + 0.1f,
                            HitRadius = 0.35f,
                            SubFireballCount = 0,
                            SkillLevel = projData.ValueRO.SkillLevel,
                            IsSubFireball = true,

                            StartPosition = currentPos.xy,
                            TargetPosition = landingPos,
                            ArcHeight = arcHeight,
                            TotalDuration = duration,
                            ElapsedTime = 0f
                        });
                    }

                    // C. Ở Cấp độ 4: Tạo Vùng Lửa Thiêu Đốt (Scorched Earth / Fire Zone DOT)
                    if (projData.ValueRO.SkillLevel >= 4)
                    {
                        Entity fireZone = ecb.CreateEntity();
                        ecb.AddComponent<FireZoneTag>(fireZone);
                        ecb.AddComponent(fireZone, LocalTransform.FromPosition(currentPos));
                        ecb.AddComponent(fireZone, new FireZoneData
                        {
                            Center = currentPos.xy,
                            Radius = 2.5f,
                            DurationRemaining = 3.5f,
                            TickInterval = 0.25f,
                            TickTimer = 0.25f,
                            DamagePerTick = 16f
                        });
                    }

                    // D. Bắn Event cho Hybrid Visual Bridge sinh hiệu ứng nổ
                    Entity eventEntity = ecb.CreateEntity();
                    ecb.AddComponent(eventEntity, new FireballExplosionEvent
                    {
                        Position = currentPos,
                        Radius = explosionRadius,
                        IsMainExplosion = true
                    });

                    // E. Hủy thực thể Cầu Lửa Mẹ
                    ecb.DestroyEntity(entity);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private static void ApplyAoeDamage(
        ref SystemState state,
        ref EntityCommandBuffer ecb,
        NativeParallelMultiHashMap<int, Entity> grid,
        float cellSize,
        float3 centerPos,
        float radius,
        float damage)
    {
        int2 centerCell = SpatialGridUtility.ToCell(centerPos.xy, cellSize);
        int cellRadius = (int)math.ceil(radius / cellSize);
        float radiusSq = radius * radius;

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            int hash = SpatialGridUtility.HashCell(centerCell + new int2(dx, dy));
            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

            do
            {
                if (!state.EntityManager.Exists(enemy) || !state.EntityManager.HasComponent<HealthData>(enemy))
                    continue;

                float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                if (math.distancesq(centerPos, enemyPos) <= radiusSq)
                {
                    HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                    hp.CurrentHP -= damage;
                    ecb.SetComponent(enemy, hp);
                }
            } while (grid.TryGetNextValue(out enemy, ref it));
        }
    }
}
