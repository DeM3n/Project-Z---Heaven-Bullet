using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[BurstCompile]
public partial struct SwordFusionSystem : ISystem
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

        // 1. Quản lý Cooldown và Triệu Hồi Đòn Giáng Kiếm Khổng Lồ (Thiên Kiếm Giáng Trần)
        foreach (var (owner, runtime, fusion, skillEntity) in
                 SystemAPI.Query<RefRO<SkillOwnerData>, RefRW<SkillRuntimeData>, RefRO<SwordFusionSkillData>>()
                     .WithAll<SwordFusionTag>()
                     .WithEntityAccess())
        {
            if (runtime.ValueRO.Level < 1) continue;
            if (fusion.ValueRO.GiantSwordPrefab == Entity.Null) continue;

            runtime.ValueRW.Timer -= dt;
            if (runtime.ValueRO.Timer > 0f) continue;

            // Reset hồi chiêu
            runtime.ValueRW.Timer = runtime.ValueRO.Cooldown;

            // Lấy vị trí Player
            Entity playerEntity = owner.ValueRO.Owner;
            if (!state.EntityManager.Exists(playerEntity) || !state.EntityManager.HasComponent<LocalTransform>(playerEntity))
                continue;

            float3 playerPos = state.EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;

            // Tìm vị trí mục tiêu (quái gần nhất hoặc cụm quái trong tầm DetectRange)
            float3 targetPos = playerPos + new float3(0f, 2.5f, 0f); // Fallback mặc định
            float closestDistSq = fusion.ValueRO.DetectRange * fusion.ValueRO.DetectRange;

            int2 playerCell = SpatialGridUtility.ToCell(playerPos.xy, cellSize);
            int searchRadiusCells = (int)math.ceil(fusion.ValueRO.DetectRange / cellSize);

            for (int dx = -searchRadiusCells; dx <= searchRadiusCells; dx++)
            for (int dy = -searchRadiusCells; dy <= searchRadiusCells; dy++)
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
                        targetPos = enemyPos;
                    }
                } while (grid.TryGetNextValue(out enemy, ref it));
            }

            // Spawn Kiếm Khổng Lồ từ trên trời lao xuống
            float3 spawnPos = new float3(targetPos.x, targetPos.y + 12f, targetPos.z);
            Entity swordEntity = ecb.Instantiate(fusion.ValueRO.GiantSwordPrefab);
            ecb.SetComponent(swordEntity, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, 1f));
            ecb.SetComponent(swordEntity, new GiantSwordData
            {
                TargetPosition = targetPos,
                CurrentPosition = spawnPos,
                FallSpeed = 40f,
                StateTimer = 0f,
                State = 0, // Falling
                SwordWavePrefab = fusion.ValueRO.SwordWavePrefab,
                ImpactDamage = fusion.ValueRO.ImpactDamage,
                ImpactRadius = fusion.ValueRO.ImpactRadius,
                WaveDamage = fusion.ValueRO.WaveDamage,
                WaveSpeed = fusion.ValueRO.WaveSpeed,
                WaveLifetime = fusion.ValueRO.WaveLifetime
            });
        }

        // 2. Cập nhật trạng thái các thanh Kiếm Khổng Lồ đang hoạt động
        foreach (var (transform, sword, swordEntity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<GiantSwordData>>()
                     .WithAll<GiantSwordTag>()
                     .WithEntityAccess())
        {
            if (sword.ValueRO.State == 0) // Giai đoạn 0: Đang lao xuống từ trên trời
            {
                sword.ValueRW.CurrentPosition.y -= sword.ValueRO.FallSpeed * dt;
                transform.ValueRW.Position = sword.ValueRO.CurrentPosition;

                // Kiểm tra chạm đất
                if (sword.ValueRO.CurrentPosition.y <= sword.ValueRO.TargetPosition.y)
                {
                    sword.ValueRW.CurrentPosition = sword.ValueRO.TargetPosition;
                    transform.ValueRW.Position = sword.ValueRO.TargetPosition;
                    sword.ValueRW.State = 1; // Chuyển sang cắm đất
                    sword.ValueRW.StateTimer = 1.0f; // Cắm trên đất 1 giây

                    // Gây sát thương AOE cực lớn lên quái xung quanh
                    float impactRadius = sword.ValueRO.ImpactRadius;
                    float impactRadiusSq = impactRadius * impactRadius;
                    int2 impactCell = SpatialGridUtility.ToCell(sword.ValueRO.TargetPosition.xy, cellSize);
                    int impactCells = (int)math.ceil(impactRadius / cellSize);

                    for (int dx = -impactCells; dx <= impactCells; dx++)
                    for (int dy = -impactCells; dy <= impactCells; dy++)
                    {
                        int hash = SpatialGridUtility.HashCell(impactCell + new int2(dx, dy));
                        if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                        do
                        {
                            if (!state.EntityManager.Exists(enemy) ||
                                !state.EntityManager.HasComponent<LocalTransform>(enemy) ||
                                !state.EntityManager.HasComponent<HealthData>(enemy))
                                continue;

                            float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                            if (math.distancesq(sword.ValueRO.TargetPosition, enemyPos) <= impactRadiusSq)
                            {
                                HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                                hp.CurrentHP -= sword.ValueRO.ImpactDamage;
                                ecb.SetComponent(enemy, hp);
                            }
                        } while (grid.TryGetNextValue(out enemy, ref it));
                    }

                    // Phát event cho Visual Bridge tạo hiệu ứng Shockwave chấn động
                    ecb.AddComponent(swordEntity, new GiantSwordImpactEvent
                    {
                        Position = sword.ValueRO.TargetPosition,
                        Radius = impactRadius
                    });

                    // Bắn 5 tia Kiếm Khí (Sword Wave) tỏa đều quanh 360 độ (mỗi góc 72 độ)
                    if (sword.ValueRO.SwordWavePrefab != Entity.Null)
                    {
                        float angleStep = math.PI * 2f / 5f;
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = i * angleStep;
                            float2 dir = new float2(math.cos(angle), math.sin(angle));
                            float rotZ = math.atan2(dir.y, dir.x) - math.PI * 0.5f;

                            Entity wave = ecb.Instantiate(sword.ValueRO.SwordWavePrefab);
                            ecb.SetComponent(wave, LocalTransform.FromPositionRotationScale(
                                sword.ValueRO.TargetPosition,
                                quaternion.Euler(0f, 0f, rotZ),
                                2.25f // Kiếm khí hoàng kim siêu đại của đòn Ultimate Fusion
                            ));
                            ecb.SetComponent(wave, new SwordWaveData
                            {
                                Direction = dir,
                                Speed = sword.ValueRO.WaveSpeed,
                                Damage = sword.ValueRO.WaveDamage,
                                Radius = 1.5f, // Bán kính quét hitbox rộng hơn tương ứng với kích thước khổng lồ
                                Lifetime = sword.ValueRO.WaveLifetime,
                                MaxLifetime = sword.ValueRO.WaveLifetime
                            });
                        }
                    }
                }
            }
            else if (sword.ValueRO.State == 1) // Giai đoạn 1: Đang cắm trên đất
            {
                sword.ValueRW.StateTimer -= dt;
                if (sword.ValueRO.StateTimer <= 0f)
                {
                    sword.ValueRW.State = 2; // Xong
                    ecb.DestroyEntity(swordEntity);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
