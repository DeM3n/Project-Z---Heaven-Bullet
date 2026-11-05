using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống kiểm tra va chạm cận chiến giữa quái vật và người chơi.
/// Khi quái chạm Player và đủ nhịp cắn (TickInterval), đẩy gói sát thương vào IncomingDamage buffer trên Player.
/// Riêng Charger khi đang Dash (Phase 2): Gây sát thương ContactDamage * DashDamageMultiplier và chỉ gây 1 lần duy nhất mỗi cú dash.
/// Không xử lý logic trừ máu/khiên/i-frames (thuộc trách nhiệm của PlayerDamageResolutionSystem).
/// </summary>
[BurstCompile]
public partial struct PlayerContactDamageSystem : ISystem
{
    private ComponentLookup<ChargerState> _chargerStateLookup;
    private ComponentLookup<ChargerConfigData> _chargerConfigLookup;

    public void OnCreate(ref SystemState state)
    {
        _chargerStateLookup = state.GetComponentLookup<ChargerState>(false);
        _chargerConfigLookup = state.GetComponentLookup<ChargerConfigData>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity)) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);
        float dt = SystemAPI.Time.DeltaTime;

        _chargerStateLookup.Update(ref state);
        _chargerConfigLookup.Update(ref state);

        foreach (var (transform, contact, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyContactDamageData>>()
                     .WithAll<EnemyTag>()
                     .WithNone<TreasureGoblinTag>()
                     .WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerTransform.Position);
            float radiusSq = contact.ValueRO.ContactRadius * contact.ValueRO.ContactRadius;
            if (distSq > radiusSq) continue;

            // Xử lý riêng cho Charger trong đợt lao (Phase 2: Dash)
            if (_chargerStateLookup.HasComponent(entity))
            {
                var chargerState = _chargerStateLookup[entity];
                if (chargerState.Phase == 2)
                {
                    if (chargerState.HasHitThisDash) continue; // Đã trúng trong đợt dash này -> chỉ gây 1 lần

                    chargerState.HasHitThisDash = true;
                    _chargerStateLookup[entity] = chargerState;

                    float dashDamage = contact.ValueRO.Damage;
                    if (_chargerConfigLookup.HasComponent(entity))
                    {
                        dashDamage *= _chargerConfigLookup[entity].DashDamageMultiplier;
                    }

                    damageBuffer.Add(new IncomingDamage
                    {
                        Amount = dashDamage,
                        SourceType = DamageSourceType.Contact,
                        SourcePosition = transform.ValueRO.Position
                    });
                    continue;
                }
            }

            // Xử lý va chạm thường theo chu kỳ TickInterval cho quái thường hoặc Charger ở phase khác
            contact.ValueRW.Timer -= dt;
            if (contact.ValueRO.Timer > 0f) continue;

            contact.ValueRW.Timer = contact.ValueRO.TickInterval;
            damageBuffer.Add(new IncomingDamage
            {
                Amount = contact.ValueRO.Damage,
                SourceType = DamageSourceType.Contact,
                SourcePosition = transform.ValueRO.Position
            });
        }
    }
}
