using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Chuyên trách trích xuất và đóng gói TOÀN BỘ các chỉ số thực tế trong game sang PauseMenuSnapshot.
/// Chỉ truy vấn những component thực sự tồn tại trong ECS.
/// </summary>
public static class PauseMenuDataProvider
{
    public static PauseMenuSnapshot Capture()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return default;
        var em = world.EntityManager;

        // 1. Tìm Player Entity
        Entity playerEntity = Entity.Null;
        using (var playerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>()))
        {
            if (!playerQuery.IsEmpty)
            {
                playerEntity = playerQuery.GetSingletonEntity();
            }
        }

        if (playerEntity == Entity.Null || !em.Exists(playerEntity))
        {
            return default;
        }

        // 2. Thông tin cơ bản & sinh tồn
        int level = 1;
        if (em.HasComponent<PlayerLevelData>(playerEntity))
        {
            level = em.GetComponentData<PlayerLevelData>(playerEntity).Level;
        }

        float survivalTime = RunLifecycleService.Instance != null ? RunLifecycleService.Instance.SurvivalTime : 0f;

        // 3. Số quái đã diệt (PlayerRunStats)
        int enemiesKilled = 0;
        if (em.HasComponent<PlayerRunStats>(playerEntity))
        {
            enemiesKilled = em.GetComponentData<PlayerRunStats>(playerEntity).EnemiesKilled;
        }
        else
        {
            using (var statsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerRunStats>()))
            {
                if (!statsQuery.IsEmpty)
                {
                    enemiesKilled = statsQuery.GetSingleton<PlayerRunStats>().EnemiesKilled;
                }
            }
        }

        // 4. Máu (HealthData)
        float currentHp = 100f;
        float maxHp = 100f;
        if (em.HasComponent<HealthData>(playerEntity))
        {
            var hp = em.GetComponentData<HealthData>(playerEntity);
            currentHp = hp.CurrentHP;
            maxHp = hp.MaxHP;
        }

        // 5. Khiên Hộ Thể (PlayerShieldData)
        bool hasShield = false;
        int shieldCharges = 0;
        int shieldLevel = 0;
        int shieldHits = 0;
        float shieldRechargeSec = 0f;
        if (em.HasComponent<PlayerShieldData>(playerEntity))
        {
            var shield = em.GetComponentData<PlayerShieldData>(playerEntity);
            hasShield = shield.SkillLevel > 0;
            shieldCharges = shield.CurrentCharges;
            shieldLevel = shield.SkillLevel;
            shieldHits = shield.HitsRemainingInCurrentShield;
            shieldRechargeSec = math.max(0f, shield.RechargeCooldown - shield.RechargeTimer);
        }

        // 6. Tự Hồi Máu (PlayerHPRegenData)
        float hpRegen = 0f;
        if (em.HasComponent<PlayerHPRegenData>(playerEntity))
        {
            hpRegen = em.GetComponentData<PlayerHPRegenData>(playerEntity).RegenPerSecond;
        }

        // 7. Giáp / Giảm Sát Thương (PlayerDefenseData)
        float damageReduction = 0f;
        if (em.HasComponent<PlayerDefenseData>(playerEntity))
        {
            damageReduction = em.GetComponentData<PlayerDefenseData>(playerEntity).DamageReductionPercent * 100f;
        }

        // 8. Tốc độ di chuyển (MovementData)
        float moveSpeed = 5.0f;
        float moveSpeedPercent = 100f;
        if (em.HasComponent<MovementData>(playerEntity))
        {
            moveSpeed = em.GetComponentData<MovementData>(playerEntity).MoveSpeed;
            moveSpeedPercent = (moveSpeed / 5f) * 100f; // 5.0f là tốc độ gốc
        }

        // 9. Hồi Sinh (PlayerRevivalData)
        bool hasRevival = false;
        bool canRevive = false;
        if (em.HasComponent<PlayerRevivalData>(playerEntity))
        {
            var rev = em.GetComponentData<PlayerRevivalData>(playerEntity);
            hasRevival = true;
            canRevive = rev.CanRevive && !rev.HasRevived;
        }

        // 10. Cổ Vật Bị Động (PlayerRelicData)
        float damageMultiplier = 1.0f;
        int bootsLvl = 0, magnetLvl = 0, vitalityLvl = 0, powerLvl = 0, diceLvl = 0;
        if (em.HasComponent<PlayerRelicData>(playerEntity))
        {
            var relic = em.GetComponentData<PlayerRelicData>(playerEntity);
            damageMultiplier = relic.DamageMultiplier;
            bootsLvl = relic.BootsLevel;
            magnetLvl = relic.MagnetLevel;
            vitalityLvl = relic.VitalityLevel;
            powerLvl = relic.PowerLevel;
            diceLvl = relic.FortuneDiceLevel;
        }

        // 11. Bán kính nhặt ngọc & đồ (PickupRangeData)
        float pickupRange = 2.0f;
        float pickupBonusPercent = 0f;
        if (em.HasComponent<PickupRangeData>(playerEntity))
        {
            pickupRange = em.GetComponentData<PickupRangeData>(playerEntity).Range;
            pickupBonusPercent = ((pickupRange / 2f) - 1.0f) * 100f; // 2.0f là bán kính nhặt gốc
        }

        // 12. Vận Khí & Thưởng Linh Thạch (PlayerFortuneData)
        float dropRateBonus = 0f;
        float goldMultiplier = 1.0f;
        if (em.HasComponent<PlayerFortuneData>(playerEntity))
        {
            var fort = em.GetComponentData<PlayerFortuneData>(playerEntity);
            dropRateBonus = fort.DropRateBonus * 100f;
            goldMultiplier = fort.GoldBonusMultiplier;
        }

        // 13. Lượt đổi thẻ (Bonus Rerolls)
        int bonusRerolls = diceLvl * 2;
        if (MetaProgressionService.Instance != null)
        {
            bonusRerolls += MetaProgressionService.Instance.GetBonusRerolls();
        }

        // 14. Danh sách Kỹ năng & Thống kê sát thương thực tế từ Singleton RunStats
        var skillStats = new List<SkillDamageStatItem>();
        float totalDamage = 0f;

        var recordsMap = new Dictionary<ushort, (double damage, int hits)>();
        using (var statsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RunStatsTag>()))
        {
            if (!statsQuery.IsEmpty)
            {
                Entity statsEntity = statsQuery.GetSingletonEntity();
                if (em.HasBuffer<SkillDamageRecord>(statsEntity))
                {
                    var buffer = em.GetBuffer<SkillDamageRecord>(statsEntity);
                    for (int b = 0; b < buffer.Length; b++)
                    {
                        var rec = buffer[b];
                        recordsMap[rec.SkillId] = (rec.TotalDamage, rec.HitCount);
                        totalDamage += (float)rec.TotalDamage;
                    }
                }
            }
        }

        foreach (var def in SkillDefinitionRegistry.AllSkills)
        {
            int skillLevel = GetSkillLevel(em, def.SkillId);
            float dmg = 0f;
            int hits = 0;
            if (recordsMap.TryGetValue(def.NumericId, out var stat))
            {
                dmg = (float)stat.damage;
                hits = stat.hits;
            }

            float percentage = totalDamage > 0f ? (dmg / totalDamage * 100f) : 0f;
            float dps = survivalTime > 0.5f ? (dmg / survivalTime) : dmg;

            skillStats.Add(new SkillDamageStatItem
            {
                SkillId = def.SkillId,
                Title = def.DisplayTitle,
                Level = skillLevel,
                MaxLevel = def.MaxLevel,
                Damage = dmg,
                HitCount = hits,
                Percentage = percentage,
                Dps = dps,
                ThemeColor = def.ThemeColor
            });
        }

        // 15. Danh sách Cổ Vật (Relics)
        var relics = new List<RelicStatItem>
        {
            new RelicStatItem { Name = GameLocalization.RelicBootsTitle, Level = bootsLvl, Description = GameLocalization.RelicBootsDesc(bootsLvl) },
            new RelicStatItem { Name = GameLocalization.RelicMagnetTitle, Level = magnetLvl, Description = GameLocalization.RelicMagnetDesc(magnetLvl) },
            new RelicStatItem { Name = GameLocalization.RelicVitalityTitle, Level = vitalityLvl, Description = GameLocalization.RelicVitalityDesc(vitalityLvl) },
            new RelicStatItem { Name = GameLocalization.RelicPowerTitle, Level = powerLvl, Description = GameLocalization.RelicPowerDesc(powerLvl) },
            new RelicStatItem { Name = GameLocalization.RelicDiceTitle, Level = diceLvl, Description = GameLocalization.RelicDiceDesc(diceLvl) }
        };

        return new PauseMenuSnapshot
        {
            Level = level,
            SurvivalTime = survivalTime,
            EnemiesKilled = enemiesKilled,

            CurrentHP = currentHp,
            MaxHP = maxHp,
            HasShield = hasShield,
            ShieldCharges = shieldCharges,
            ShieldLevel = shieldLevel,
            ShieldHitsRemaining = shieldHits,
            ShieldRechargeSec = shieldRechargeSec,
            HpRegenPerSec = hpRegen,
            DamageReductionPercent = damageReduction,
            MoveSpeed = moveSpeed,
            MoveSpeedPercent = moveSpeedPercent,
            HasRevival = hasRevival,
            CanRevive = canRevive,

            GlobalDamageMultiplier = damageMultiplier,
            PickupRange = pickupRange,
            PickupBonusPercent = pickupBonusPercent,
            LifeHeartHealAmount = 25f,
            BonusRerolls = bonusRerolls,
            DropRateBonusPercent = dropRateBonus,
            GoldMultiplier = goldMultiplier,

            TotalDamage = totalDamage,
            TotalDps = survivalTime > 0.5f ? (totalDamage / survivalTime) : totalDamage,

            SkillStats = skillStats,
            Relics = relics
        };
    }

    private static int GetSkillLevel(EntityManager em, string skillId)
    {
        EntityQuery query = skillId switch
        {
            "FlyingSword" => em.CreateEntityQuery(ComponentType.ReadOnly<FlyingSwordSkillTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            "SlashSword" => em.CreateEntityQuery(ComponentType.ReadOnly<SlashSwordSkillTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            "ProjectileSkill" => em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSkillTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            "Fireball" => em.CreateEntityQuery(ComponentType.ReadOnly<FireballSkillTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            "Shield" => em.CreateEntityQuery(ComponentType.ReadOnly<ShieldSkillTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            "Boomerang" => em.CreateEntityQuery(ComponentType.ReadOnly<BoomerangSkillTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            "SwordFusion" => em.CreateEntityQuery(ComponentType.ReadOnly<SwordFusionTag>(), ComponentType.ReadOnly<SkillRuntimeData>()),
            _ => default
        };

        if (query == default || query.IsEmpty) return 0;
        Entity e = query.GetSingletonEntity();
        if (!em.HasComponent<SkillRuntimeData>(e)) return 0;
        return em.GetComponentData<SkillRuntimeData>(e).Level;
    }
}
