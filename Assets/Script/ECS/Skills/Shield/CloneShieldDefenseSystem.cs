using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống xử lý Khiên Hộ Thể (Aegis Shield) cho các Phân Thân Ảo Ảnh:
/// - Khi phân thân có khiên kích hoạt (HitsRemainingInCurrentShield > 0), nếu quái chạm vào bán kính khiên (0.85m),
///   khiên sẽ đỡ đòn thay vì để quái lọt qua.
/// - Khi khiên vỡ, sinh ShieldBreakEvent tại vị trí phân thân (với 25% sát thương nổ)
///   để ShieldExplosionSystem và ShieldVisualBridge kích hoạt nổ AOE & hiệu ứng vỡ khiên trực quan.
/// - Tự động hồi phục khiên theo chu kỳ hồi chiêu RechargeCooldown.
/// </summary>
[UpdateAfter(typeof(PlayerCloneMovementSystem))]
[UpdateBefore(typeof(ShieldExplosionSystem))]
[BurstCompile]
public partial struct CloneShieldDefenseSystem : ISystem
{
    private EntityQuery _cloneQuery;
    private EntityQuery _shieldSkillQuery;

    public void OnCreate(ref SystemState state)
    {
        _cloneQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerCloneTag, PlayerCloneData, LocalTransform, PlayerShieldData>()
            .Build();

        _shieldSkillQuery = SystemAPI.QueryBuilder()
            .WithAll<ShieldSkillTag, ShieldSkillData>()
            .WithNone<CloneSkillTag>()
            .Build();

        state.RequireForUpdate<SpatialGridSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_cloneQuery.IsEmpty) return;

        float dt = SystemAPI.Time.DeltaTime;
        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;

        ShieldSkillData skillData = default;
        if (!_shieldSkillQuery.IsEmpty)
        {
            skillData = SystemAPI.GetComponent<ShieldSkillData>(_shieldSkillQuery.GetSingletonEntity());
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (shield, transform, entity) in
                 SystemAPI.Query<RefRW<PlayerShieldData>, RefRO<LocalTransform>>()
                     .WithAll<PlayerCloneTag>()
                     .WithEntityAccess())
        {
            if (shield.ValueRO.SkillLevel <= 0) continue;

            // 1. Logic hồi khiên của phân thân
            if (shield.ValueRO.CurrentCharges < shield.ValueRO.MaxCharges)
            {
                shield.ValueRW.RechargeTimer -= dt;
                if (shield.ValueRO.RechargeTimer <= 0f)
                {
                    shield.ValueRW.CurrentCharges++;
                    shield.ValueRW.HitsRemainingInCurrentShield = shield.ValueRO.HitsPerShield;
                    shield.ValueRW.RechargeTimer = shield.ValueRO.RechargeCooldown > 0f ? shield.ValueRO.RechargeCooldown : 30f;
                }
            }

            // 2. Kiểm tra quái va chạm vào lớp khiên đang mở
            if (shield.ValueRO.CurrentCharges <= 0 || shield.ValueRO.HitsRemainingInCurrentShield <= 0)
                continue;

            float3 clonePos = transform.ValueRO.Position;
            float shieldRadius = 0.85f;
            float shieldRadiusSq = shieldRadius * shieldRadius;

            int2 cell = SpatialGridUtility.ToCell(clonePos.xy, cellSize);
            bool hasBlocked = false;

            for (int dx = -1; dx <= 1 && !hasBlocked; dx++)
            for (int dy = -1; dy <= 1 && !hasBlocked; dy++)
            {
                int hash = SpatialGridUtility.HashCell(cell + new int2(dx, dy));
                if (!grid.TryGetFirstValue(hash, out Entity enemy, out var it)) continue;

                do
                {
                    if (!SystemAPI.Exists(enemy) || !SystemAPI.HasComponent<LocalTransform>(enemy)) continue;
                    if (!SystemAPI.HasComponent<EnemyTag>(enemy)) continue;

                    float3 enemyPos = SystemAPI.GetComponent<LocalTransform>(enemy).Position;
                    float distSq = math.distancesq(clonePos.xy, enemyPos.xy);

                    if (distSq <= shieldRadiusSq)
                    {
                        // Chặn đòn thành công!
                        hasBlocked = true;
                        shield.ValueRW.HitsRemainingInCurrentShield--;
                        shield.ValueRW.HasJustBlocked = true;

                        // Nếu hết hit: Khiên vỡ tung và phát nổ!
                        if (shield.ValueRW.HitsRemainingInCurrentShield <= 0)
                        {
                            shield.ValueRW.CurrentCharges = math.max(0, shield.ValueRO.CurrentCharges - 1);
                            shield.ValueRW.RechargeTimer = shield.ValueRO.RechargeCooldown > 0f ? shield.ValueRO.RechargeCooldown : 30f;

                            bool isLv4 = shield.ValueRO.SkillLevel >= 4;
                            Entity breakEvt = ecb.CreateEntity();
                            ecb.AddComponent(breakEvt, new ShieldBreakEvent
                            {
                                Position = clonePos,
                                Radius = isLv4 ? skillData.ExplosionRadius : 2.5f,
                                Damage = isLv4 ? skillData.ExplosionDamage : 0f,
                                KnockbackForce = isLv4 ? skillData.KnockbackForce : 1.5f,
                                IsExplosion = isLv4,
                                Source = entity
                            });
                        }
                        break;
                    }
                }
                while (grid.TryGetNextValue(out enemy, ref it));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

