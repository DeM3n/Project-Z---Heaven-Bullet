using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Hệ thống TRUNG TÂM duy nhất giải quyết toàn bộ sát thương tiếp nhận của Quái vật (Enemy Damage Pipeline).
/// Duyệt tuần tự các packet trong EnemyIncomingDamage, áp dụng hệ số sát thương tổng thể (Player ATK / DamageMultiplier),
/// tính sát thương hiệu dụng (Effective Damage), cập nhật bộ đếm SkillDamageRecord trên singleton RunStats,
/// trừ CurrentHP và dọn sạch buffer.
/// </summary>
[UpdateAfter(typeof(ShieldExplosionSystem))]
[UpdateBefore(typeof(DeathCleanupSystem))]
[BurstCompile]
public partial struct EnemyDamageResolutionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Khởi tạo idempotent cho Singleton RunStats
        var query = state.GetEntityQuery(ComponentType.ReadOnly<RunStatsTag>());
        if (query.IsEmpty)
        {
            Entity statsEntity = state.EntityManager.CreateEntity(typeof(RunStatsTag));
            state.EntityManager.AddBuffer<SkillDamageRecord>(statsEntity);
        }
        state.RequireForUpdate<RunStatsTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Entity statsEntity = SystemAPI.GetSingletonEntity<RunStatsTag>();
        DynamicBuffer<SkillDamageRecord> records = SystemAPI.GetBuffer<SkillDamageRecord>(statsEntity);

        // Lấy Hệ số Sát thương Tổng thể (ATK Multiplier) của Người chơi từ PlayerRelicData
        float globalDamageMultiplier = 1.0f;
        if (SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity) &&
            SystemAPI.HasComponent<PlayerRelicData>(playerEntity))
        {
            globalDamageMultiplier = math.max(0.01f, SystemAPI.GetComponent<PlayerRelicData>(playerEntity).DamageMultiplier);
        }

        ComponentLookup<PlayerCloneTag> cloneLookup = SystemAPI.GetComponentLookup<PlayerCloneTag>(true);

        foreach (var (damageBuffer, health, entity) in
                 SystemAPI.Query<DynamicBuffer<EnemyIncomingDamage>, RefRW<HealthData>>()
                     .WithAll<EnemyTag>()
                     .WithEntityAccess())
        {
            if (damageBuffer.IsEmpty) continue;

            float currentHP = health.ValueRO.CurrentHP;

            // Nếu quái đã chết trước đòn này (vd: Bomber tự hủy hoặc Cheat), bỏ qua không tính vào thống kê
            if (currentHP > 0f)
            {
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    EnemyIncomingDamage packet = damageBuffer[i];
                    if (packet.Amount <= 0f) continue;
                    if (currentHP <= 0f) break; // Quái đã gục bởi hit trước trong cùng frame

                    // Hệ số nguồn: nếu sát thương đến từ Phân Thân (PlayerCloneTag) -> tự động giảm còn 25% (0.25x)
                    float sourceMultiplier = 1.0f;
                    if (packet.Source != Entity.Null && cloneLookup.HasComponent(packet.Source))
                    {
                        sourceMultiplier = 0.25f;
                    }

                    // Áp dụng hệ số ATK tổng thể và hệ số nguồn cho các đòn đánh kỹ năng
                    float scaledAmount = packet.SkillId != SkillIds.Unattributed
                        ? packet.Amount * globalDamageMultiplier * sourceMultiplier
                        : packet.Amount;

                    // Sát thương hiệu dụng = max(0, min(Amount đã scale, HP trước đòn))
                    float effectiveDamage = math.max(0f, math.min(scaledAmount, currentHP));
                    currentHP -= scaledAmount; // Trừ máu thực tế theo Amount đã scale

                    // Ghi nhận vào SkillDamageRecord của singleton
                    RecordSkillDamage(ref records, packet.SkillId, effectiveDamage);
                }

                health.ValueRW.CurrentHP = currentHP;
            }

            // Dọn sạch buffer sau khi giải quyết xong
            damageBuffer.Clear();
        }
    }

    private static void RecordSkillDamage(ref DynamicBuffer<SkillDamageRecord> records, ushort skillId, float effectiveDamage)
    {
        for (int i = 0; i < records.Length; i++)
        {
            if (records[i].SkillId == skillId)
            {
                SkillDamageRecord rec = records[i];
                rec.TotalDamage += effectiveDamage;
                rec.HitCount++;
                records[i] = rec;
                return;
            }
        }

        // Chưa có record cho SkillId này -> tạo mới
        records.Add(new SkillDamageRecord
        {
            SkillId = skillId,
            TotalDamage = effectiveDamage,
            HitCount = 1
        });
    }
}

