using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Quản lý toàn bộ nghiệp vụ Cổ Vật (Relic) và phần thưởng khi mở Rương Báu (Ancient Chest),
/// bao gồm phát hiện ChestOpenEvent, sinh Relic ngẫu nhiên, áp dụng hiệu ứng và thưởng Linh Thạch.
/// </summary>
public class ChestRewardService
{
    private readonly EntityManager _entityManager;
    private readonly EntityQuery _chestEventQuery;
    private readonly EntityQuery _projectileSkillQuery;
    private readonly EntityQuery _flyingSwordSkillQuery;
    private readonly EntityQuery _slashSwordSkillQuery;
    private readonly EntityQuery _fireballSkillQuery;
    private readonly EntityQuery _boomerangSkillQuery;
    private readonly EntityQuery _swordFusionSkillQuery;

    private readonly List<IRelicDefinition> _relicDefinitions;

    public ChestRewardService(EntityManager em)
    {
        _entityManager = em;
        _chestEventQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ChestOpenEvent>());

        _projectileSkillQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSkillTag>(), ComponentType.ReadWrite<ProjectileSkillData>());
        _flyingSwordSkillQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FlyingSwordSkillTag>(), ComponentType.ReadWrite<FlyingSwordSkillData>());
        _slashSwordSkillQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SlashSwordSkillTag>(), ComponentType.ReadWrite<SlashSwordSkillData>());
        _fireballSkillQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FireballSkillTag>(), ComponentType.ReadWrite<FireballSkillData>());
        _boomerangSkillQuery = em.CreateEntityQuery(ComponentType.ReadOnly<BoomerangSkillTag>(), ComponentType.ReadWrite<BoomerangSkillData>());
        _swordFusionSkillQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SwordFusionTag>(), ComponentType.ReadWrite<SwordFusionSkillData>());

        _relicDefinitions = new List<IRelicDefinition>
        {
            new WindrunnerBootsRelic(),
            new SpiritMagnetRelic(),
            new HeartCrystalRelic(),
            new PowerCharmRelic(this),
            new FortuneDiceRelic()
        };
    }

    /// <summary>
    /// Kiểm tra xem có sự kiện nhặt rương (ChestOpenEvent) hay không.
    /// Nếu có, tiêu thụ (tiêu hủy) event entity và trả về true.
    /// </summary>
    public bool CheckAndConsumeChestEvent()
    {
        if (_chestEventQuery.IsEmpty) return false;

        var events = _chestEventQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < events.Length; i++)
        {
            _entityManager.DestroyEntity(events[i]);
        }
        events.Dispose();

        return true;
    }

    /// <summary>
    /// Chọn ngẫu nhiên 1 Cổ Vật trong danh sách
    /// </summary>
    public IRelicDefinition SelectRandomRelic()
    {
        int index = UnityEngine.Random.Range(0, _relicDefinitions.Count);
        return _relicDefinitions[index];
    }

    public IRelicDefinition GetRelicByType(RelicType type)
    {
        return _relicDefinitions.Find(r => r.Type == type) ?? _relicDefinitions[0];
    }

    /// <summary>
    /// Thu nhận phần thưởng Cổ Vật và cộng Linh Thạch
    /// </summary>
    public (int stonesEarned, int extraRerolls) CollectReward(IRelicDefinition relic, Entity playerEntity)
    {
        relic.Apply(_entityManager, playerEntity);

        // Tính toán thưởng Linh Thạch nhân hệ số Vận Khí từ cây Thiên Phú
        float goldMult = MetaProgressionService.Instance != null ? MetaProgressionService.Instance.GetGoldMultiplier() : 1f;
        int stonesEarned = Mathf.RoundToInt(35 * goldMult);
        MetaProgressionService.Instance?.AddSpiritStones(stonesEarned);

        int extraRerolls = (relic.Type == RelicType.FortuneDice) ? 2 : 0;
        return (stonesEarned, extraRerolls);
    }

    /// <summary>
    /// Nhân sát thương toàn bộ các kỹ năng người chơi khi nhận Cổ Kiếm Phù (PowerCharm)
    /// </summary>
    public void ScaleAllSkillsDamage(float mult)
    {
        if (!_projectileSkillQuery.IsEmpty)
        {
            var e = _projectileSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<ProjectileSkillData>(e))
            {
                var data = _entityManager.GetComponentData<ProjectileSkillData>(e);
                data.ProjectileDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_flyingSwordSkillQuery.IsEmpty)
        {
            var e = _flyingSwordSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<FlyingSwordSkillData>(e))
            {
                var data = _entityManager.GetComponentData<FlyingSwordSkillData>(e);
                data.BaseDamage *= mult;
                data.AoeDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_slashSwordSkillQuery.IsEmpty)
        {
            var e = _slashSwordSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<SlashSwordSkillData>(e))
            {
                var data = _entityManager.GetComponentData<SlashSwordSkillData>(e);
                data.BaseDamage *= mult;
                data.WaveDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_fireballSkillQuery.IsEmpty)
        {
            var e = _fireballSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<FireballSkillData>(e))
            {
                var data = _entityManager.GetComponentData<FireballSkillData>(e);
                data.BaseDamage *= mult;
                data.FireZoneDamagePerTick *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_boomerangSkillQuery.IsEmpty)
        {
            var e = _boomerangSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<BoomerangSkillData>(e))
            {
                var data = _entityManager.GetComponentData<BoomerangSkillData>(e);
                data.BaseDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_swordFusionSkillQuery.IsEmpty)
        {
            var e = _swordFusionSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<SwordFusionSkillData>(e))
            {
                var data = _entityManager.GetComponentData<SwordFusionSkillData>(e);
                data.ImpactDamage *= mult;
                data.WaveDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
    }

    private static PlayerRelicData GetOrCreateRelicData(EntityManager em, Entity player)
    {
        if (em.HasComponent<PlayerRelicData>(player))
        {
            return em.GetComponentData<PlayerRelicData>(player);
        }
        var data = new PlayerRelicData { DamageMultiplier = 1.0f };
        em.AddComponentData(player, data);
        return data;
    }

    #region --- Định nghĩa 5 Cổ Vật (Relic Strategy Definitions) ---

    // 1. Giày Gió (Windrunner Boots)
    private class WindrunnerBootsRelic : IRelicDefinition
    {
        public RelicType Type => RelicType.WindrunnerBoots;
        public string RelicId => "WindrunnerBoots";
        public float Weight => 1f;

        public RelicRewardData GetRewardData(EntityManager em, Entity player)
        {
            int lvl = em.HasComponent<PlayerRelicData>(player) ? em.GetComponentData<PlayerRelicData>(player).BootsLevel + 1 : 1;
            return new RelicRewardData
            {
                Type = Type,
                IconEmoji = "👢",
                Title = GameLocalization.RelicBootsTitle,
                LevelBadge = $"LV.{lvl}",
                Description = GameLocalization.RelicBootsDesc(lvl)
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            var relic = GetOrCreateRelicData(em, player);
            relic.BootsLevel++;
            em.SetComponentData(player, relic);

            if (em.HasComponent<MovementData>(player))
            {
                var mov = em.GetComponentData<MovementData>(player);
                mov.MoveSpeed *= 1.15f;
                em.SetComponentData(player, mov);
            }
        }
    }

    // 2. Tụ Linh Châu (Spirit Magnet)
    private class SpiritMagnetRelic : IRelicDefinition
    {
        public RelicType Type => RelicType.SpiritMagnet;
        public string RelicId => "SpiritMagnet";
        public float Weight => 1f;

        public RelicRewardData GetRewardData(EntityManager em, Entity player)
        {
            int lvl = em.HasComponent<PlayerRelicData>(player) ? em.GetComponentData<PlayerRelicData>(player).MagnetLevel + 1 : 1;
            return new RelicRewardData
            {
                Type = Type,
                IconEmoji = "🧲",
                Title = GameLocalization.RelicMagnetTitle,
                LevelBadge = $"LV.{lvl}",
                Description = GameLocalization.RelicMagnetDesc(lvl)
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            var relic = GetOrCreateRelicData(em, player);
            relic.MagnetLevel++;
            em.SetComponentData(player, relic);

            if (em.HasComponent<PickupRangeData>(player))
            {
                var pr = em.GetComponentData<PickupRangeData>(player);
                pr.Range *= 1.50f;
                em.SetComponentData(player, pr);
            }
        }
    }

    // 3. Tim Pha Lê (Heart Crystal)
    private class HeartCrystalRelic : IRelicDefinition
    {
        public RelicType Type => RelicType.HeartCrystal;
        public string RelicId => "HeartCrystal";
        public float Weight => 1f;

        public RelicRewardData GetRewardData(EntityManager em, Entity player)
        {
            int lvl = em.HasComponent<PlayerRelicData>(player) ? em.GetComponentData<PlayerRelicData>(player).VitalityLevel + 1 : 1;
            return new RelicRewardData
            {
                Type = Type,
                IconEmoji = "💎",
                Title = GameLocalization.RelicVitalityTitle,
                LevelBadge = $"LV.{lvl}",
                Description = GameLocalization.RelicVitalityDesc(lvl)
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            var relic = GetOrCreateRelicData(em, player);
            relic.VitalityLevel++;
            em.SetComponentData(player, relic);

            if (em.HasComponent<HealthData>(player))
            {
                var hp = em.GetComponentData<HealthData>(player);
                hp.MaxHP += 30f;
                hp.CurrentHP = hp.MaxHP;
                em.SetComponentData(player, hp);
            }
        }
    }

    // 4. Cổ Kiếm Phù (Power Charm)
    private class PowerCharmRelic : IRelicDefinition
    {
        private readonly ChestRewardService _service;

        public PowerCharmRelic(ChestRewardService service)
        {
            _service = service;
        }

        public RelicType Type => RelicType.PowerCharm;
        public string RelicId => "PowerCharm";
        public float Weight => 1f;

        public RelicRewardData GetRewardData(EntityManager em, Entity player)
        {
            int lvl = em.HasComponent<PlayerRelicData>(player) ? em.GetComponentData<PlayerRelicData>(player).PowerLevel + 1 : 1;
            return new RelicRewardData
            {
                Type = Type,
                IconEmoji = "⚔️",
                Title = GameLocalization.RelicPowerTitle,
                LevelBadge = $"LV.{lvl}",
                Description = GameLocalization.RelicPowerDesc(lvl)
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            var relic = GetOrCreateRelicData(em, player);
            relic.PowerLevel++;
            relic.DamageMultiplier *= 1.15f;
            em.SetComponentData(player, relic);

            _service.ScaleAllSkillsDamage(1.15f);
        }
    }

    // 5. Xúc Xắc May Mắn (Fortune Dice)
    private class FortuneDiceRelic : IRelicDefinition
    {
        public RelicType Type => RelicType.FortuneDice;
        public string RelicId => "FortuneDice";
        public float Weight => 1f;

        public RelicRewardData GetRewardData(EntityManager em, Entity player)
        {
            int lvl = em.HasComponent<PlayerRelicData>(player) ? em.GetComponentData<PlayerRelicData>(player).FortuneDiceLevel + 1 : 1;
            return new RelicRewardData
            {
                Type = Type,
                IconEmoji = "🎲",
                Title = GameLocalization.RelicDiceTitle,
                LevelBadge = $"LV.{lvl}",
                Description = GameLocalization.RelicDiceDesc(lvl)
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            var relic = GetOrCreateRelicData(em, player);
            relic.FortuneDiceLevel++;
            em.SetComponentData(player, relic);
        }
    }

    #endregion
}

