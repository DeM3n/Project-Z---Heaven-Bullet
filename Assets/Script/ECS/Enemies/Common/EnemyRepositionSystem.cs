using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống Tái định vị quái vật bị bỏ xa (Enemy Repositioning System):
/// - Dựa trên KÍCH THƯỚC VÀ VỊ TRÍ THỰC TẾ CỦA CAMERA VIEWPORT (Bounding Box AABB từ CameraViewData).
/// - Giải quyết triệt để vấn đề quái vật chậm bị bỏ xa khi người chơi chạy nhanh, gây đói EXP và lãng phí CPU.
/// - Thuật toán Ray-Box Perimeter Intersection:
///   + Camera Bounding Box = [CamPos.x +/- HalfWidth, CamPos.y +/- HalfHeight]
///   + Mở rộng thêm SafetyMargin (mặc định 2.0m) làm vùng cấm xuất hiện.
///   + Chiếu tia từ tâm Camera theo góc đón đầu (Forward-Biased Cone 75% / Flanks 25%) cắt qua viền hộp chữ nhật.
///   + Bổ sung độ tán xạ ngẫu nhiên (Scatter 0.5m - 2.5m).
///   -> ĐẢM BẢO 100% TOÁN HỌC KHÔNG BAO GIỜ XUẤT HIỆN BÊN TRONG TẦM NHÌN CAMERA TRÊN MỌI ĐỘ PHÂN GIẢI & MỨC ZOOM!
/// - Bảo toàn nguyên vẹn máu (HealthData), kích thước, EliteTag; reset cooldown đòn đánh (Grace Period 0.75s).
/// - Pure ECS 1.x, Burst-compiled, zero allocation, thực thi trước SpatialGridBuildSystem.
/// </summary>
[BurstCompile]
[UpdateBefore(typeof(SpatialGridBuildSystem))]
[UpdateAfter(typeof(SpawnSystem))]
public partial struct EnemyRepositionSystem : ISystem
{
    private ComponentLookup<ShooterAttackData> _shooterAttackLookup;
    private ComponentLookup<ChargerState> _chargerStateLookup;
    private ComponentLookup<ChargerTelegraphVisual> _chargerTelegraphLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _shooterAttackLookup = state.GetComponentLookup<ShooterAttackData>(false);
        _chargerStateLookup = state.GetComponentLookup<ChargerState>(false);
        _chargerTelegraphLookup = state.GetComponentLookup<ChargerTelegraphVisual>(false);

        state.RequireForUpdate<PlayerTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Tự động khởi tạo singleton mặc định nếu cảnh chưa bake qua SpawnerAuthoring
        if (!SystemAPI.HasSingleton<EnemyRepositionConfigData>())
        {
            Entity configEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(configEntity, new EnemyRepositionConfigData
            {
                Enabled = true,
                CheckInterval = 0.2f,
                Timer = 0.2f,
                SafetyMargin = 2.0f,
                OutOfRangeOffset = 5.5f,
                ForwardConeHalfAngleRad = math.radians(55f),
                ForwardBiasChance = 0.75f,
                Rng = new Unity.Mathematics.Random(54321)
            });
        }

        ref var config = ref SystemAPI.GetSingletonRW<EnemyRepositionConfigData>().ValueRW;
        if (!config.Enabled) return;

        // 2. Tần suất kiểm tra điều độ (Staggering 0.2s = 5 lần/giây) tiết kiệm tối đa CPU
        float dt = SystemAPI.Time.DeltaTime;
        config.Timer -= dt;
        if (config.Timer > 0f) return;
        config.Timer = config.CheckInterval;

        // 3. Đọc vị trí và hướng di chuyển của Người chơi
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        float2 playerDir = float2.zero;
        if (SystemAPI.HasComponent<MovementData>(playerEntity))
        {
            playerDir = SystemAPI.GetComponent<MovementData>(playerEntity).Direction;
        }

        float speedSq = math.lengthsq(playerDir);
        bool isPlayerMoving = speedSq > 0.001f;
        float baseAngle = 0f;
        if (isPlayerMoving)
        {
            float2 normDir = playerDir / math.sqrt(speedSq);
            baseAngle = math.atan2(normDir.y, normDir.x);
        }

        // 4. Đọc thông số khung nhìn Camera thực tế từ CameraViewData (Hybrid Bridge)
        float3 camPos = playerPos;
        float halfWidth = 12.8f;  // Chuẩn 16:9 ở orthographicSize = 7.2
        float halfHeight = 7.2f;

        if (SystemAPI.TryGetSingleton<CameraViewData>(out var camView))
        {
            camPos = camView.Position;
            halfWidth = math.max(10f, camView.HalfWidth);
            halfHeight = math.max(6f, camView.HalfHeight);
        }

        // Hộp bao an toàn (Camera Frustum AABB + Safety Margin)
        float boxHalfWidth = halfWidth + config.SafetyMargin;
        float boxHalfHeight = halfHeight + config.SafetyMargin;

        // Bán kính góc chéo màn hình cộng thêm khoảng offset để kích hoạt reposition
        float cornerDist = math.sqrt(boxHalfWidth * boxHalfWidth + boxHalfHeight * boxHalfHeight);
        float outOfRangeDist = cornerDist + config.OutOfRangeOffset;
        float outOfRangeDistSq = outOfRangeDist * outOfRangeDist;

        _shooterAttackLookup.Update(ref state);
        _chargerStateLookup.Update(ref state);
        _chargerTelegraphLookup.Update(ref state);

        // 5. Quét toàn bộ quái vật sống (loại trừ Treasure Goblin)
        foreach (var (transform, movement, hp, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<MovementData>, RefRO<HealthData>>()
                     .WithAll<EnemyTag>()
                     .WithNone<TreasureGoblinTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.CurrentHP <= 0f) continue;

            // Kiểm tra khoảng cách so với Camera (hoặc Player)
            float distSq = math.distancesq(transform.ValueRO.Position.xy, camPos.xy);
            if (distSq <= outOfRangeDistSq) continue;

            // 6. Tính toán hướng xuất hiện thông minh
            float angle;
            if (isPlayerMoving)
            {
                float roll = config.Rng.NextFloat(0f, 1f);
                if (roll < config.ForwardBiasChance)
                {
                    // 75%: Hình nón đón đầu phía trước (+/- 55 độ)
                    float offset = config.Rng.NextFloat(-config.ForwardConeHalfAngleRad, config.ForwardConeHalfAngleRad);
                    angle = baseAngle + offset;
                }
                else
                {
                    // 25%: Đánh chặn hai bên sườn (+/- 70 đến 110 độ)
                    float sign = (config.Rng.NextInt(0, 2) == 0) ? 1f : -1f;
                    float offset = config.Rng.NextFloat(math.radians(70f), math.radians(110f)) * sign;
                    angle = baseAngle + offset;
                }
            }
            else
            {
                // Người chơi đứng yên: phân bố đều 360 độ
                angle = config.Rng.NextFloat(0f, math.PI * 2f);
            }

            // 7. Thuật toán Ray-Box Perimeter Intersection:
            // Chiếu tia từ tâm Camera theo góc 'angle' cắt qua viền hình chữ nhật (boxHalfWidth, boxHalfHeight)
            float cosA = math.cos(angle);
            float sinA = math.sin(angle);
            float absCos = math.abs(cosA);
            float absSin = math.abs(sinA);

            float tx = absCos > 0.0001f ? (boxHalfWidth / absCos) : float.MaxValue;
            float ty = absSin > 0.0001f ? (boxHalfHeight / absSin) : float.MaxValue;
            float tPerimeter = math.min(tx, ty);

            // Bổ sung độ tán xạ ngẫu nhiên (Scatter Jitter 0.5m - 2.5m)
            float extraScatter = config.Rng.NextFloat(0.5f, 2.5f);
            float finalDist = tPerimeter + extraScatter;

            float3 newPos = new float3(camPos.x + cosA * finalDist, camPos.y + sinA * finalDist, 0f);

            // 8. Chốt chặn an toàn tuyệt đối (Fail-Safe Assertion):
            // Bảo đảm vị trí MỚI LUÔN NẰM NGOÀI KHUNG NHÌN CAMERA với khoảng cách tối thiểu >= half + 1.5m
            if (math.abs(newPos.x - camPos.x) <= halfWidth + 1.0f && math.abs(newPos.y - camPos.y) <= halfHeight + 1.0f)
            {
                float pushX = newPos.x >= camPos.x ? (halfWidth + 2.5f) : -(halfWidth + 2.5f);
                newPos.x = camPos.x + pushX;
            }

            // Gán vị trí mới (Zero ECB allocation, Burst compile native)
            transform.ValueRW.Position = newPos;

            // Đổi hướng di chuyển lập tức về phía người chơi
            float2 toPlayer = (playerPos.xy - newPos.xy);
            float toPlayerLen = math.length(toPlayer);
            movement.ValueRW.Direction = toPlayerLen > 0.001f ? (toPlayer / toPlayerLen) : float2.zero;

            // 9. Grace Period (Thời gian ân hạn): Đặt lại nhịp tấn công để tránh người chơi bị đánh lén
            if (_shooterAttackLookup.HasComponent(entity))
            {
                var shooter = _shooterAttackLookup[entity];
                shooter.Timer = math.max(shooter.Timer, 0.75f);
                _shooterAttackLookup[entity] = shooter;
            }

            if (_chargerStateLookup.HasComponent(entity))
            {
                var charger = _chargerStateLookup[entity];
                charger.Phase = 0; // Approach
                charger.PhaseTimer = 0f;
                charger.CooldownTimer = math.max(charger.CooldownTimer, 1.0f);
                charger.DashTraveled = 0f;
                charger.HasHitThisDash = false;
                _chargerStateLookup[entity] = charger;

                if (_chargerTelegraphLookup.HasComponent(entity) && _chargerTelegraphLookup.IsComponentEnabled(entity))
                {
                    _chargerTelegraphLookup.SetComponentEnabled(entity, false);
                }
            }
        }
    }
}
