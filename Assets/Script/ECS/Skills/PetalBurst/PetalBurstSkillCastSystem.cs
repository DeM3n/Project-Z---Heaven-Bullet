using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống ngắm bắn và giải phóng Kỹ năng Tiên Nữ Tán Hoa (Fairy Petal Burst).
/// Hoàn toàn Burst-compiled:
/// - Bung tỏa đồng loạt Y bông hoa tỏa đều 360 độ quanh người chơi.
/// - Tự động Bootstrap thực thể kỹ năng nếu chưa có trong Scene.
/// - Đồng bộ chỉ số tự động theo cấp độ 1-4 từ PetalBurstLevelBalanceData.
/// </summary>
[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(PetalBurstMovementAndCollisionSystem))]
[BurstCompile]
public partial struct PetalBurstSkillCastSystem : ISystem
{
    private EntityQuery _playerQuery;
    private EntityQuery _skillQuery;
    private Unity.Mathematics.Random _rng;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();

        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .Build();

        _skillQuery = SystemAPI.QueryBuilder()
            .WithAll<PetalBurstSkillTag, SkillRuntimeData, PetalBurstSkillData>()
            .Build();

        _rng = new Unity.Mathematics.Random(98765);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Tự động Bootstrap Skill Entity nếu chưa có trong Scene
        if (_skillQuery.IsEmpty)
        {
            if (!_playerQuery.IsEmpty)
            {
                Entity playerEntity = _playerQuery.GetSingletonEntity();
                Entity skillEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<PetalBurstSkillTag>(skillEntity);
                state.EntityManager.AddComponentData(skillEntity, new SkillOwnerData { Owner = playerEntity });

                // Mặc định khởi tạo Level 1 để test và sẵn sàng kích hoạt
                state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
                {
                    Cooldown = 3.5f,
                    Timer = 0.5f,
                    Level = 1,
                    SkillId = "PetalBurst"
                });

                state.EntityManager.AddComponentData(skillEntity, new PetalBurstSkillData
                {
                    BaseDamage = 22f,
                    FlySpeed = 8.5f,
                    MinSpeedMultiplier = 0.65f,
                    MaxSpeedMultiplier = 1.35f,
                    DragDeceleration = 7.0f,
                    AngleSpreadJitter = 0.65f,
                    HitRadius = 0.38f,
                    ExplosionRadius = 0.92f,
                    Duration = 0.85f,
                    PetalCount = 6,
                    VortexPullRadius = 2.6f,
                    VortexPullForce = 8.5f
                });

                state.EntityManager.AddComponentData(skillEntity, new PetalBurstLevelBalanceData
                {
                    DamageLv1 = 22f,
                    DamageLv2 = 32f,
                    DamageLv3 = 48f,
                    DamageLv4 = 70f,

                    CooldownLv1 = 3.5f,
                    CooldownLv2 = 3.0f,
                    CooldownLv3 = 2.6f,
                    CooldownLv4 = 2.2f,

                    CountLv1 = 6,
                    CountLv2 = 9,
                    CountLv3 = 13,
                    CountLv4 = 18,

                    DurationLv1 = 0.85f,
                    DurationLv2 = 0.95f,
                    DurationLv3 = 1.10f,
                    DurationLv4 = 1.25f,

                    ExplosionRadiusLv1 = 0.92f,
                    ExplosionRadiusLv2 = 1.03f,
                    ExplosionRadiusLv3 = 1.19f,
                    ExplosionRadiusLv4 = 1.46f
                });
            }
            return;
        }

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach (var (skillData, balance, runtime, ownerData, entity) in
                 SystemAPI.Query<RefRW<PetalBurstSkillData>, RefRO<PetalBurstLevelBalanceData>, RefRW<SkillRuntimeData>, RefRO<SkillOwnerData>>()
                     .WithAll<PetalBurstSkillTag>()
                     .WithEntityAccess())
        {
            int level = math.clamp(runtime.ValueRO.Level, 0, 4);
            if (level <= 0) continue; // Chưa mở khóa kỹ năng

            // Xác định thông số cân bằng theo cấp độ
            float curDamage = balance.ValueRO.DamageLv1;
            float curCooldown = balance.ValueRO.CooldownLv1;
            int curCount = balance.ValueRO.CountLv1;
            float curDuration = balance.ValueRO.DurationLv1;
            float curExplosionRad = balance.ValueRO.ExplosionRadiusLv1;

            if (level == 2)
            {
                curDamage = balance.ValueRO.DamageLv2;
                curCooldown = balance.ValueRO.CooldownLv2;
                curCount = balance.ValueRO.CountLv2;
                curDuration = balance.ValueRO.DurationLv2;
                curExplosionRad = balance.ValueRO.ExplosionRadiusLv2;
            }
            else if (level == 3)
            {
                curDamage = balance.ValueRO.DamageLv3;
                curCooldown = balance.ValueRO.CooldownLv3;
                curCount = balance.ValueRO.CountLv3;
                curDuration = balance.ValueRO.DurationLv3;
                curExplosionRad = balance.ValueRO.ExplosionRadiusLv3;
            }
            else if (level >= 4)
            {
                curDamage = balance.ValueRO.DamageLv4;
                curCooldown = balance.ValueRO.CooldownLv4;
                curCount = balance.ValueRO.CountLv4;
                curDuration = balance.ValueRO.DurationLv4;
                curExplosionRad = balance.ValueRO.ExplosionRadiusLv4;
            }

            runtime.ValueRW.Cooldown = curCooldown;
            runtime.ValueRW.Timer -= dt;

            if (runtime.ValueRO.Timer <= 0f)
            {
                runtime.ValueRW.Timer = curCooldown;

                Entity owner = ownerData.ValueRO.Owner;
                if (owner == Entity.Null || !transformLookup.HasComponent(owner)) continue;

                float3 spawnPos = transformLookup[owner].Position;

                // Góc xuất phát ngẫu nhiên ban đầu
                float baseAngle = _rng.NextFloat(0f, math.PI * 2f);
                float angleJitter = skillData.ValueRO.AngleSpreadJitter > 0f ? skillData.ValueRO.AngleSpreadJitter : 0.65f;
                float minSpdMul = skillData.ValueRO.MinSpeedMultiplier > 0f ? skillData.ValueRO.MinSpeedMultiplier : 0.65f;
                float maxSpdMul = skillData.ValueRO.MaxSpeedMultiplier > 0f ? skillData.ValueRO.MaxSpeedMultiplier : 1.35f;

                // Bắn tỏa Y bông hoa xung quanh người chơi theo kiểu Particle Burst (vận tốc đa dạng + góc ngẫu nhiên)
                for (int i = 0; i < curCount; i++)
                {
                    // 1. Góc phóng ngẫu nhiên kiểu Particle System Burst (tự nhiên, có chùm và độ lệch phóng khoáng)
                    float angle = baseAngle + (i * math.PI * 2f / curCount) + _rng.NextFloat(-angleJitter, angleJitter);
                    float2 dir = new float2(math.cos(angle), math.sin(angle));
                    float rotSpeed = _rng.NextFloat(6f, 14f) * (_rng.NextBool() ? 1f : -1f);

                    // 2. Vận tốc ban đầu đa dạng (Varied Start Speed): một số hoa vút xa, một số trôi gần
                    float speedFactor = _rng.NextFloat(minSpdMul, maxSpdMul);
                    float petalStartSpeed = skillData.ValueRO.FlySpeed * speedFactor;

                    // 3. Thời gian tồn tại chênh lệch nhẹ để hoa nổ so le giòn giã (Asynchronous Pop)
                    float petalDuration = curDuration * _rng.NextFloat(0.88f, 1.12f);

                    Entity petal = ecb.CreateEntity();
                    ecb.AddComponent<PetalProjectileTag>(petal);
                    ecb.AddComponent(petal, LocalTransform.FromPositionRotation(
                        spawnPos,
                        quaternion.RotateZ(_rng.NextFloat(0f, math.PI * 2f))));

                    ecb.AddComponent(petal, new PetalProjectileData
                    {
                        Direction = dir,
                        Speed = petalStartSpeed,
                        DragDeceleration = skillData.ValueRO.DragDeceleration,
                        Damage = curDamage,
                        HitRadius = skillData.ValueRO.HitRadius,
                        ExplosionRadius = curExplosionRad,
                        Duration = petalDuration,
                        LifeTimer = 0f,
                        SkillLevel = level,
                        Owner = owner,
                        RotationAngle = _rng.NextFloat(0f, math.PI * 2f),
                        RotationSpeed = rotSpeed,
                        VortexPullRadius = skillData.ValueRO.VortexPullRadius,
                        VortexPullForce = skillData.ValueRO.VortexPullForce
                    });
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

