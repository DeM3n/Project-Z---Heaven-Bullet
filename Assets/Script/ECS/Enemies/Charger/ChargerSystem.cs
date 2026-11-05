using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều khiển hành vi Charger Enemy:
/// - Phase 0 (Approach): Tiếp cận chậm tới HoldDistance, đứng chờ cooldown và player trong TriggerRange
/// - Phase 1 (Telegraph): Đứng yên báo hiệu, chốt DashDirection ngẫu nhiên (+/- AimSpreadDeg) ở frame cuối
/// - Phase 2 (Dash): Lao thẳng tốc độ cao không homing đến khi đi đủ DashDistance
/// - Phase 3 (Recover): Đứng im hồi phục, hết RecoverTime reset CooldownTimer về Approach
/// </summary>
[UpdateAfter(typeof(SpatialGridBuildSystem))]
[UpdateBefore(typeof(ApplyMovementSystem))]
public partial struct ChargerSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpatialGridSingleton>();
        state.RequireForUpdate<PlayerTag>();
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        _transformLookup.Update(ref state);
        var grid = SystemAPI.GetSingleton<SpatialGridSingleton>().Grid;
        float cellSize = SystemAPI.GetSingleton<SpatialGridSingleton>().CellSize;
        float dt = SystemAPI.Time.DeltaTime;

        JobHandle jobHandle = new ChargerJob
        {
            PlayerPosition = playerPos,
            Grid = grid,
            TransformLookup = _transformLookup,
            CellSize = cellSize,
            DeltaTime = dt
        }.ScheduleParallel(state.Dependency);

        state.Dependency = new ChargerVisualJob().ScheduleParallel(jobHandle);

#if UNITY_EDITOR
        state.Dependency.Complete();
        foreach (var (chargerState, entity) in SystemAPI.Query<RefRW<ChargerState>>().WithEntityAccess())
        {
            if (chargerState.ValueRO.Phase != chargerState.ValueRO.PreviousPhase)
            {
                byte oldP = chargerState.ValueRO.PreviousPhase;
                byte newP = chargerState.ValueRO.Phase;
                chargerState.ValueRW.PreviousPhase = newP;

                string pInfo = newP switch
                {
                    0 => $"Phase 0 [Approach] - Giữ cự ly, Cooldown: {chargerState.ValueRO.CooldownTimer:F1}s",
                    1 => $"Phase 1 [Telegraph/Windup] - Đứng yên gồng báo hiệu!",
                    2 => $"Phase 2 [Dash] - Lao thẳng hướng ({chargerState.ValueRO.DashDirection.x:F2}, {chargerState.ValueRO.DashDirection.y:F2})",
                    3 => $"Phase 3 [Recover] - Đã lao {chargerState.ValueRO.DashTraveled:F1}m, nghỉ hồi phục",
                    _ => $"Phase {newP}"
                };
                UnityEngine.Debug.Log($"<color=#FFA500>[Charger #{entity.Index}]</color> Chuyển trạng thái: <b>{pInfo}</b>");
            }
        }
#endif
    }
}

/// <summary>
/// Job xử lý Logic AI và chuyển động của Charger.
/// Nhận 'in LocalTransform' (ReadOnly) để tránh lỗi Aliasing với ComponentLookup TransformLookup.
/// </summary>
[BurstCompile]
[WithAll(typeof(ChargerTag))]
[WithNone(typeof(EnemyStunData))]
public partial struct ChargerJob : IJobEntity
{
    public float3 PlayerPosition;
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Grid;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    public float CellSize;
    public float DeltaTime;

    void Execute(
        Entity entity,
        ref ChargerState state,
        ref ChargerRandomData rngData,
        ref MovementData movement,
        in LocalTransform transform,
        in ChargerConfigData config,
        in EnemyFlockingData flocking)
    {
        // Bảo vệ an toàn: Nếu seed ngẫu nhiên bị 0 khi instantiate tại runtime, khôi phục lại ngay
        if (rngData.Value.state == 0)
        {
            rngData.Value.InitState((uint)math.max(1, entity.Index * 31 + 17));
        }

        float2 myPos = transform.Position.xy;
        float2 toPlayer = (PlayerPosition - transform.Position).xy;
        float distToPlayer = math.length(toPlayer);

        switch (state.Phase)
        {
            // =========================================================================
            // PHASE 0: APPROACH
            // Đi chậm về phía Player đến khi vào HoldDistance thì đứng yên.
            // Kết thúc khi: DashCooldown xong VÀ Player trong TriggerRange.
            // =========================================================================
            case 0:
            {
                if (state.CooldownTimer > 0f)
                {
                    state.CooldownTimer -= DeltaTime;
                }

                // Kiểm tra điều kiện kích hoạt Telegraph
                if (state.CooldownTimer <= 0f && distToPlayer <= config.TriggerRange)
                {
                    state.Phase = 1;
                    state.PhaseTimer = 0f;
                    movement.Direction = float2.zero;
                    return;
                }

                // Nếu chưa vào HoldDistance, đi chậm tiếp cận kết hợp tách đàn (Separation)
                if (distToPlayer > config.HoldDistance)
                {
                    float2 separationForce = CalculateSeparation(entity, myPos, flocking.SeparationRadius);
                    float2 dir = math.normalizesafe(toPlayer);
                    float2 combined = dir * 0.7f + separationForce * flocking.SeparationWeight;
                    movement.Direction = math.normalizesafe(combined) * 0.7f;
                }
                else
                {
                    // Đã vào HoldDistance -> Đứng yên giữ khoảng cách
                    movement.Direction = float2.zero;
                }
                break;
            }

            // =========================================================================
            // PHASE 1: TELEGRAPH
            // Đứng yên báo hiệu chuẩn bị lao.
            // Frame cuối chốt duy nhất 1 hướng DashDirection, chuyển sang Phase 2.
            // =========================================================================
            case 1:
            {
                movement.Direction = float2.zero;
                state.PhaseTimer += DeltaTime;

                // Frame cuối của Telegraph: Chốt hướng DashDirection
                if (state.PhaseTimer >= config.WindupTime)
                {
                    float2 baseDir = math.normalizesafe(toPlayer);
                    if (math.lengthsq(baseDir) < 0.0001f) baseDir = new float2(1f, 0f);

                    // Xoay hướng ngẫu nhiên trong khoảng [-AimSpreadDeg, +AimSpreadDeg]
                    float spreadDeg = rngData.Value.NextFloat(-config.AimSpreadDeg, config.AimSpreadDeg);
                    float spreadRad = math.radians(spreadDeg);
                    float cosA = math.cos(spreadRad);
                    float sinA = math.sin(spreadRad);
                    float2 finalDir = new float2(baseDir.x * cosA - baseDir.y * sinA, baseDir.x * sinA + baseDir.y * cosA);

                    state.DashDirection = math.normalizesafe(finalDir);
                    state.Phase = 2;
                    state.PhaseTimer = 0f;
                    state.DashTraveled = 0f;
                    state.HasHitThisDash = false;

                    // Bắt đầu lao ngay frame này
                    float speedRatio = config.DashSpeed / math.max(0.1f, movement.MoveSpeed);
                    movement.Direction = state.DashDirection * speedRatio;
                }
                break;
            }

            // =========================================================================
            // PHASE 2: DASH
            // Lao thẳng theo DashDirection với DashSpeed, không đổi hướng, không homing.
            // Kết thúc khi đi đủ DashDistance -> chuyển sang Phase 3 Recover.
            // =========================================================================
            case 2:
            {
                float speedRatio = config.DashSpeed / math.max(0.1f, movement.MoveSpeed);
                movement.Direction = state.DashDirection * speedRatio;

                float step = config.DashSpeed * DeltaTime;
                state.DashTraveled += step;

                // Khi đi đủ quãng đường DashDistance -> kết thúc dash
                if (state.DashTraveled >= config.DashDistance)
                {
                    state.Phase = 3;
                    state.PhaseTimer = 0f;
                    movement.Direction = float2.zero;
                }
                break;
            }

            // =========================================================================
            // PHASE 3: RECOVER
            // Đứng im hồi phục. Hết RecoverTime -> quay lại Phase 0 Approach.
            // =========================================================================
            case 3:
            {
                movement.Direction = float2.zero;
                state.PhaseTimer += DeltaTime;

                if (state.PhaseTimer >= config.RecoverTime)
                {
                    state.Phase = 0;
                    state.PhaseTimer = 0f;
                    state.CooldownTimer = config.DashCooldown;
                    state.HasHitThisDash = false;
                }
                break;
            }
        }
    }

    private float2 CalculateSeparation(Entity selfEntity, float2 myPos, float sepRadius)
    {
        float2 force = float2.zero;
        float sepRadiusSq = sepRadius * sepRadius;
        int2 myCell = SpatialGridUtility.ToCell(myPos, CellSize);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int hash = SpatialGridUtility.HashCell(myCell + new int2(dx, dy));
                if (!Grid.TryGetFirstValue(hash, out Entity otherEntity, out var it)) continue;

                do
                {
                    if (otherEntity == selfEntity) continue;
                    if (!TransformLookup.HasComponent(otherEntity)) continue;

                    float2 otherPos = TransformLookup[otherEntity].Position.xy;
                    float2 diff = myPos - otherPos;
                    float distSq = math.lengthsq(diff);
                    if (distSq < sepRadiusSq && distSq > 0.0001f)
                    {
                        float d = math.sqrt(distSq);
                        float strength = 1.0f - (d / sepRadius);
                        force += (diff / d) * strength;
                    }
                } while (Grid.TryGetNextValue(out otherEntity, ref it));
            }
        }

        return force;
    }
}

/// <summary>
/// Job phụ trách hiệu ứng hình ảnh báo hiệu Telegraph (Scale Jitter).
/// Chạy riêng biệt không chứa TransformLookup, hoàn toàn không bị Aliasing.
/// </summary>
[BurstCompile]
[WithAll(typeof(ChargerTag))]
[WithNone(typeof(EnemyStunData))]
public partial struct ChargerVisualJob : IJobEntity
{
    void Execute(ref LocalTransform transform, in ChargerState state)
    {
        if (state.Phase == 1) // Telegraph: Rung và phình to báo hiệu rõ ràng
        {
            transform.Scale = 1.0f + 0.22f * math.sin(state.PhaseTimer * 32f);
        }
        else if (transform.Scale != 1.0f)
        {
            transform.Scale = 1.0f;
        }
    }
}
