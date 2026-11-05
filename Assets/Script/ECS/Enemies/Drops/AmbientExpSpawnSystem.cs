using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống sinh ngọc EXP ngẫu nhiên (Small & Medium) rải rác quanh người chơi trên bản đồ.
/// Quy chuẩn kỹ thuật:
/// - Sinh gần Player: bán kính 4.5m - 11.5m (vừa vặn trong/rìa khung nhìn camera 16:9).
/// - Giới hạn số lượng (Cap = 15): không sinh quá nhiều gây rối mắt hay làm mất cân bằng lượng EXP.
/// - Nhịp sinh điều độ: 1-2 viên mỗi 3.5s.
/// - Culling xa (> 30m): tự động dọn các viên ngọc tự nhiên khi người chơi đi quá xa để nhường slot sinh ngọc mới gần người chơi.
/// - Tỉ lệ ngẫu nhiên: 70% Ngọc Nhỏ (Exp 3), 30% Ngọc Vừa (Exp 6).
/// - Pure ECS 1.x + Burst Compile hiệu năng cao, zero GC allocation.
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(SpawnSystem))]
[UpdateBefore(typeof(XPPickupSystem))]
public partial struct AmbientExpSpawnSystem : ISystem
{
    private EntityQuery _playerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .Build();

        state.RequireForUpdate<PlayerTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 1. Quét và đếm số lượng ngọc tự nhiên đang có trên sân, đồng thời dọn dẹp các viên ở quá xa (> 30m)
        const float despawnDistSq = 30f * 30f;
        int activeAmbientCount = 0;

        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<AmbientExpOrbTag, ExpOrbData>()
                     .WithEntityAccess())
        {
            float distSq = math.distancesq(playerPos.xy, transform.ValueRO.Position.xy);
            if (distSq > despawnDistSq)
            {
                ecb.DestroyEntity(entity);
            }
            else
            {
                activeAmbientCount++;
            }
        }

        // 2. Tìm hoặc Bootstrap AmbientExpSpawnerData nếu cần
        bool hasSpawner = SystemAPI.HasSingleton<AmbientExpSpawnerData>();
        if (!hasSpawner)
        {
            // Tìm prefab ngọc từ các enemy drop đã bake trong ECS world để làm fallback an toàn
            Entity fallbackSmallPrefab = Entity.Null;
            Entity fallbackMedPrefab = Entity.Null;

            foreach (var drop in SystemAPI.Query<RefRO<EnemyDeathDropData>>())
            {
                if (drop.ValueRO.ExpOrbPrefab != Entity.Null)
                {
                    if (fallbackSmallPrefab == Entity.Null)
                    {
                        fallbackSmallPrefab = drop.ValueRO.ExpOrbPrefab;
                    }
                    else if (fallbackMedPrefab == Entity.Null && drop.ValueRO.ExpOrbPrefab != fallbackSmallPrefab)
                    {
                        fallbackMedPrefab = drop.ValueRO.ExpOrbPrefab;
                    }
                }
            }

            if (fallbackSmallPrefab != Entity.Null)
            {
                Entity spawnerEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(spawnerEntity, new AmbientExpSpawnerData
                {
                    SmallOrbPrefab = fallbackSmallPrefab,
                    MediumOrbPrefab = (fallbackMedPrefab != Entity.Null) ? fallbackMedPrefab : fallbackSmallPrefab,
                    Interval = 3.5f,
                    Timer = 1.5f,
                    MaxAmbientOrbs = 15,
                    MinRadius = 4.5f,
                    MaxRadius = 11.5f,
                    Rng = new Random(54321)
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            return;
        }

        // 3. Tiến hành đếm thời gian và sinh ngọc tự nhiên
        foreach (var spawner in SystemAPI.Query<RefRW<AmbientExpSpawnerData>>())
        {
            Entity smallPrefab = spawner.ValueRO.SmallOrbPrefab;
            Entity medPrefab = spawner.ValueRO.MediumOrbPrefab;

            // Kiểm tra an toàn Prefabs nếu chưa gán
            if (smallPrefab == Entity.Null && medPrefab == Entity.Null)
            {
                foreach (var drop in SystemAPI.Query<RefRO<EnemyDeathDropData>>())
                {
                    if (drop.ValueRO.ExpOrbPrefab != Entity.Null)
                    {
                        smallPrefab = drop.ValueRO.ExpOrbPrefab;
                        medPrefab = drop.ValueRO.ExpOrbPrefab;
                        spawner.ValueRW.SmallOrbPrefab = smallPrefab;
                        spawner.ValueRW.MediumOrbPrefab = medPrefab;
                        break;
                    }
                }
            }

            if (smallPrefab == Entity.Null && medPrefab == Entity.Null) continue;
            if (medPrefab == Entity.Null) medPrefab = smallPrefab;
            if (smallPrefab == Entity.Null) smallPrefab = medPrefab;

            spawner.ValueRW.Timer -= dt;
            if (spawner.ValueRO.Timer > 0f) continue;

            // Hết timer -> đặt lại chu kỳ
            spawner.ValueRW.Timer = spawner.ValueRO.Interval;

            // Đảm bảo không vượt quá giới hạn tối đa (MaxAmbientOrbs)
            if (activeAmbientCount >= spawner.ValueRO.MaxAmbientOrbs) continue;

            // Số lượng sinh mỗi đợt: 1 hoặc 2 viên (75% ra 1 viên, 25% ra 2 viên)
            int spawnCount = (spawner.ValueRW.Rng.NextFloat() < 0.25f) ? 2 : 1;
            spawnCount = math.min(spawnCount, spawner.ValueRO.MaxAmbientOrbs - activeAmbientCount);

            for (int i = 0; i < spawnCount; i++)
            {
                // Tỉ lệ: 70% Small (3 EXP), 30% Medium (6 EXP)
                bool isMedium = spawner.ValueRW.Rng.NextFloat() < 0.30f;
                Entity chosenPrefab = isMedium ? medPrefab : smallPrefab;
                int expValue = isMedium ? 6 : 3;

                // Tọa độ ngẫu nhiên quanh Player: Bán kính từ MinRadius (4.5m) đến MaxRadius (11.5m)
                float angle = spawner.ValueRW.Rng.NextFloat(0f, math.PI * 2f);
                float radius = spawner.ValueRW.Rng.NextFloat(spawner.ValueRO.MinRadius, spawner.ValueRO.MaxRadius);
                float3 spawnPos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0f);

                Entity orbEntity = ecb.Instantiate(chosenPrefab);
                ecb.SetComponent(orbEntity, LocalTransform.FromPosition(spawnPos));
                ecb.SetComponent(orbEntity, new ExpOrbData
                {
                    ExpValue = expValue,
                    FlySpeed = 0f
                });
                ecb.AddComponent<AmbientExpOrbTag>(orbEntity);
                activeAmbientCount++;
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

