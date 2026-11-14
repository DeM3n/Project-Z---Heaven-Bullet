using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct PlayerMetaAppliedTag : IComponentData {}

/// <summary>
/// Component quản lý khả năng Hồi Sinh (Phoenix Rebirth) từ Đạo Quả Cây Thiên Phú
/// </summary>
public struct PlayerRevivalData : IComponentData
{
    public bool CanRevive;
    public bool HasRevived;
}

/// <summary>
/// Component quản lý giảm sát thương nhận vào (Armor / Damage Reduction) từ Cây Thiên Phú
/// </summary>
public struct PlayerDefenseData : IComponentData
{
    public float DamageReductionPercent;
}

/// <summary>
/// Component quản lý vận khí rơi đồ và hệ số thưởng Linh Thạch từ Cây Thiên Phú
/// </summary>
public struct PlayerFortuneData : IComponentData
{
    public float DropRateBonus;
    public float GoldBonusMultiplier;
}

/// <summary>
/// Hệ thống tự động đọc dữ liệu Cây Thiên Phú vĩnh viễn (MetaProgressionService)
/// và áp dụng các chỉ số buff nền tảng (HP, ATK, MoveSpeed, Pickup Range, Revival, Regen)
/// ngay khi Player Entity được sinh ra trong màn chơi.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct PlayerMetaProgressionApplySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (health, movement, pickup, relic, entity) in
                 SystemAPI.Query<RefRW<HealthData>, RefRW<MovementData>, RefRW<PickupRangeData>, RefRW<PlayerRelicData>>()
                     .WithAll<PlayerTag>()
                     .WithNone<PlayerMetaAppliedTag>()
                     .WithEntityAccess())
        {
            var meta = MetaProgressionService.Instance;
            if (meta != null)
            {
                // 1. Buff Máu tối đa (Max HP)
                float bonusHP = meta.GetBonusMaxHP();
                if (bonusHP > 0f)
                {
                    health.ValueRW.MaxHP += bonusHP;
                    health.ValueRW.CurrentHP = health.ValueRO.MaxHP;
                }

                // 2. Buff Tốc độ di chuyển (MoveSpeed)
                float speedMult = meta.GetMoveSpeedMultiplier();
                movement.ValueRW.MoveSpeed *= speedMult;

                // 3. Buff Sát thương tổng thể (Global ATK)
                float atkMult = meta.GetGlobalATKMultiplier();
                relic.ValueRW.DamageMultiplier *= atkMult;

                // 3b. Buff Hệ số nhân EXP (Exp Multiplier từ Ngộ Đạo Tâm Kinh)
                float expMult = meta.GetBonusExpMultiplier();
                relic.ValueRW.ExpMultiplier *= expMult;

                // 4. Buff Bán kính nhặt ngọc & đồ (Pickup Range)
                float pickupBonus = meta.GetBonusPickupRange();
                pickup.ValueRW.Range *= (1f + pickupBonus);

                // 5. Thêm / Cộng dồn Tự Hồi Máu (HP Regen) nếu có điểm
                float regenPerSec = meta.GetBonusHPRegen();
                if (regenPerSec > 0f)
                {
                    if (SystemAPI.HasComponent<PlayerHPRegenData>(entity))
                    {
                        var regen = SystemAPI.GetComponent<PlayerHPRegenData>(entity);
                        regen.RegenPerSecond += regenPerSec;
                        SystemAPI.SetComponent(entity, regen);
                    }
                    else
                    {
                        ecb.AddComponent(entity, new PlayerHPRegenData
                        {
                            RegenPerSecond = regenPerSec,
                            Timer = 0f
                        });
                    }
                }

                // 6. Thêm Đạo Quả Hồi Sinh (Revival) nếu đã mở khóa
                if (meta.HasRevival())
                {
                    ecb.AddComponent(entity, new PlayerRevivalData
                    {
                        CanRevive = true,
                        HasRevived = false
                    });
                }

                // 7. Thêm Giáp hộ thân (Damage Reduction)
                float dmgRed = meta.GetDamageReductionPercent();
                if (dmgRed > 0f)
                {
                    ecb.AddComponent(entity, new PlayerDefenseData
                    {
                        DamageReductionPercent = dmgRed
                    });
                }

                // 8. Thêm Vận Khí & Thưởng Linh Thạch (Drop Rate & Gold Multiplier)
                ecb.AddComponent(entity, new PlayerFortuneData
                {
                    DropRateBonus = meta.GetBonusDropRatePercent(),
                    GoldBonusMultiplier = meta.GetGoldMultiplier()
                });

                Debug.Log($"[PlayerMetaProgressionApplySystem] Applied Meta Buffs -> MaxHP: {health.ValueRO.MaxHP}, MoveSpeed: {movement.ValueRO.MoveSpeed:F1}, ATK Multiplier: {relic.ValueRO.DamageMultiplier:F2}, Armor: {dmgRed * 100:F1}%, DropBonus: +{meta.GetBonusDropRatePercent() * 100:F1}%");
            }

            // Đánh dấu đã áp dụng, không chạy lặp lại
            ecb.AddComponent<PlayerMetaAppliedTag>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
