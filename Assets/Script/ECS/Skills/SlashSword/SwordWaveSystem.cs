using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(SpatialGridBuildSystem))]
[BurstCompile]
public partial struct SwordWaveSystem : ISystem
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

        foreach (var (transform, wave, hitBuffer, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<SwordWaveData>, DynamicBuffer<SwordWaveHitTargetBuffer>>()
                     .WithAll<SwordWaveTag>()
                     .WithEntityAccess())
        {
            // 1. Đếm lùi thời gian sống
            wave.ValueRW.Lifetime -= dt;
            if (wave.ValueRO.Lifetime <= 0f)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            // 2. Di chuyển theo hướng bay thẳng
            float2 dir = math.normalizesafe(wave.ValueRO.Direction);
            transform.ValueRW.Position.xy += dir * wave.ValueRO.Speed * dt;

            // Đồng bộ góc quay theo hướng bay
            float rotZ = math.atan2(dir.y, dir.x) - math.PI * 0.5f;
            transform.ValueRW.Rotation = quaternion.Euler(0f, 0f, rotZ);

            // 3. Quét va chạm xuyên thấu (Piercing Collision) qua Spatial Grid 3x3
            int2 cell = SpatialGridUtility.ToCell(transform.ValueRO.Position.xy, cellSize);
            float hitDistSq = wave.ValueRO.Radius * wave.ValueRO.Radius;

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

                    // Kiểm tra xem quái đã bị đợt kiếm khí này trúng chưa
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

                    float3 enemyPos = state.EntityManager.GetComponentData<LocalTransform>(enemy).Position;
                    if (math.distancesq(transform.ValueRO.Position, enemyPos) <= hitDistSq)
                    {
                        // Thêm vào danh sách đã trúng đòn để không trừ máu nhiều lần trong các frame tiếp theo
                        hitBuffer.Add(new SwordWaveHitTargetBuffer { Target = enemy });

                        // Gây sát thương
                        HealthData hp = state.EntityManager.GetComponentData<HealthData>(enemy);
                        hp.CurrentHP -= wave.ValueRO.Damage;
                        ecb.SetComponent(enemy, hp);

                        // Lưu ý: KHÔNG hủy entity kiếm khí ở đây vì có thuộc tính Piercing (xuyên thấu)!
                    }
                } while (grid.TryGetNextValue(out enemy, ref it));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
