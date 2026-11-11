using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều khiển chuyển động, lực cản không khí, va chạm và kích nổ Kỹ năng Tiên Nữ Tán Hoa:
/// 1. Vận tốc ban đầu nhanh, sau đó giảm tốc nhanh chóng do lực cản (Linear Drag Deceleration).
/// 2. Tự động xoay tròn cánh hoa theo thời gian tạo hiệu ứng hoa bay xoáy tự nhiên.
/// 3. Va chạm với quái hoặc hết thời gian tồn tại (Duration) -> Phát nổ AOE.
/// 4. ĐẶC BIỆT CẤP 4: Kích hoạt Lực hút Chân Không (Petal Implosion Vortex) hút quái vật vào tâm trước khi nổ.
/// 5. Sinh sự kiện PetalDetonationEvent để Hybrid Visual Bridge thể hiện hiệu ứng thị giác và âm thanh.
/// </summary>
[UpdateAfter(typeof(PetalBurstSkillCastSystem))]
[UpdateBefore(typeof(EnemyDamageResolutionSystem))]
[BurstCompile]
public partial struct PetalBurstMovementAndCollisionSystem : ISystem
{
    private BufferLookup<EnemyIncomingDamage> _damageLookup;
    private ComponentLookup<LocalTransform> _transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();
        _damageLookup = state.GetBufferLookup<EnemyIncomingDamage>(false);
        _transformLookup = state.GetComponentLookup<LocalTransform>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _damageLookup.Update(ref state);
        _transformLookup.Update(ref state);

        var gridSingleton = SystemAPI.GetSingleton<SpatialGridSingleton>();
        var grid = gridSingleton.Grid;
        float cellSize = gridSingleton.CellSize;
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (petal, transform, entity) in
                 SystemAPI.Query<RefRW<PetalProjectileData>, RefRW<LocalTransform>>()
                     .WithAll<PetalProjectileTag>()
                     .WithEntityAccess())
        {
            // 1. Giảm tốc độ do lực cản không khí (Deceleration / Linear Drag) giúp hoa trôi chậm dần
            float speed = math.max(0.25f, petal.ValueRO.Speed - petal.ValueRO.DragDeceleration * dt);
            petal.ValueRW.Speed = speed;

            // 2. Di chuyển theo hướng tỏa ra
            float3 moveStep = new float3(petal.ValueRO.Direction.x, petal.ValueRO.Direction.y, 0f) * speed * dt;
            transform.ValueRW.Position += moveStep;

            // 3. Xoay tròn bông hoa
            float rotAngle = petal.ValueRO.RotationAngle + petal.ValueRO.RotationSpeed * dt;
            petal.ValueRW.RotationAngle = rotAngle;
            transform.ValueRW.Rotation = quaternion.RotateZ(rotAngle);

            // 4. Cập nhật thời gian bay
            petal.ValueRW.LifeTimer += dt;

            float2 petalPos2D = transform.ValueRO.Position.xy;
            bool shouldDetonate = petal.ValueRO.LifeTimer >= petal.ValueRO.Duration;

            // 5. Kiểm tra va chạm với quái vật qua Spatial Grid nếu chưa hết giờ
            if (!shouldDetonate)
            {
                int2 cell = SpatialGridUtility.ToCell(petalPos2D, cellSize);
                float hitRadSq = petal.ValueRO.HitRadius * petal.ValueRO.HitRadius;

                for (int dx = -1; dx <= 1 && !shouldDetonate; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int hash = SpatialGridUtility.HashCell(cell + new int2(dx, dy));
                        if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                        do
                        {
                            if (!_transformLookup.HasComponent(enemy)) continue;
                            float2 enemyPos = _transformLookup[enemy].Position.xy;
                            if (math.distancesq(petalPos2D, enemyPos) <= hitRadSq)
                            {
                                shouldDetonate = true;
                                break;
                            }
                        } while (grid.TryGetNextValue(out enemy, ref it));
                    }
                }
            }

            // 6. Xử lý Kích Nổ & Hiệu Ứng Hút Quái Cấp 4
            if (shouldDetonate)
            {
                int skillLvl = petal.ValueRO.SkillLevel;
                float explosionRad = petal.ValueRO.ExplosionRadius;
                float aoeRadSq = explosionRad * explosionRad;
                Entity owner = petal.ValueRO.Owner;
                float damage = petal.ValueRO.Damage;

                // --- HIỆU ỨNG CẤP 4: HOA TRẬN HÚT QUÁI (PETAL IMPLOSION VORTEX - OPTION A: VACUUM SNAP) ---
                float searchRadius = explosionRad;
                if (skillLvl >= 4)
                {
                    float vortexRad = petal.ValueRO.VortexPullRadius;
                    searchRadius = math.max(searchRadius, vortexRad);
                    int cellRange = (int)math.ceil(vortexRad / cellSize);
                    int2 centerCell = SpatialGridUtility.ToCell(petalPos2D, cellSize);

                    for (int dx = -cellRange; dx <= cellRange; dx++)
                    {
                        for (int dy = -cellRange; dy <= cellRange; dy++)
                        {
                            int hash = SpatialGridUtility.HashCell(centerCell + new int2(dx, dy));
                            if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                            do
                            {
                                if (!_transformLookup.HasComponent(enemy)) continue;
                                float2 enemyPos = _transformLookup[enemy].Position.xy;
                                float2 toCenter = petalPos2D - enemyPos;
                                float dist = math.length(toCenter);

                                // Quái vật trong bán kính hút bị giật mạnh tức thì về tâm nổ (Vacuum Pop / Implosion Snap)
                                if (dist > 0.15f && dist <= vortexRad)
                                {
                                    // Giật mạnh 70% cự ly về tâm (tối thiểu 0.75m, tối đa 1.85m), gom toàn bộ quái vào tâm nổ
                                    float pullDist = math.min(dist - 0.12f, math.max(0.75f, dist * 0.70f));
                                    float2 pullStep = (toCenter / dist) * math.max(0f, pullDist);
                                    LocalTransform enemyTr = _transformLookup[enemy];
                                    enemyTr.Position.xy += pullStep;
                                    _transformLookup[enemy] = enemyTr;
                                }
                            } while (grid.TryGetNextValue(out enemy, ref it));
                        }
                    }
                }

                // Gây sát thương nổ AOE cho toàn bộ quái trong Bán kính nổ (kể cả quái vừa bị hút vào)
                int expCellRange = (int)math.ceil(searchRadius / cellSize);
                int2 expCenterCell = SpatialGridUtility.ToCell(petalPos2D, cellSize);

                for (int dx = -expCellRange; dx <= expCellRange; dx++)
                {
                    for (int dy = -expCellRange; dy <= expCellRange; dy++)
                    {
                        int hash = SpatialGridUtility.HashCell(expCenterCell + new int2(dx, dy));
                        if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                        do
                        {
                            if (!_transformLookup.HasComponent(enemy)) continue;
                            float2 enemyPos = _transformLookup[enemy].Position.xy;
                            if (math.distancesq(petalPos2D, enemyPos) <= aoeRadSq)
                            {
                                EnemyDamage.Add(ref _damageLookup, enemy, SkillIds.PetalBurst, damage, owner);
                            }
                        } while (grid.TryGetNextValue(out enemy, ref it));
                    }
                }

                // Tạo thực thể Sự kiện Nổ để Visual Bridge sinh VFX / SFX
                Entity evt = ecb.CreateEntity();
                ecb.AddComponent(evt, new PetalDetonationEvent
                {
                    Position = transform.ValueRO.Position,
                    ExplosionRadius = explosionRad,
                    SkillLevel = skillLvl,
                    HasVortex = (skillLvl >= 4)
                });

                // Hủy bông hoa sau khi phát nổ
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

