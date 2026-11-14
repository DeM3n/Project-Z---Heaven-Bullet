using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Kho đăng ký toàn bộ các Perk kỹ năng và chỉ số (IPerkDefinition) trong game.
/// Bất cứ kỹ năng mới nào được thêm vào dự án chỉ cần đăng ký 1 class tại đây theo chuẩn OCP.
/// </summary>
public static class PerkRegistry
{
    public static List<IPerkDefinition> CreateDefaultDefinitions(EntityManager em)
    {
        return new List<IPerkDefinition>
        {
            new SwordFusionPerk(em),
            new ThunderBoomerangPerk(em),
            new FlyingSwordPerk(em),
            new SlashSwordPerk(em),
            new ProjectileSkillPerk(em),
            new FireballPerk(em),
            new ShieldPerk(em),
            new BoomerangPerk(em),
            new ElectricPerk(em),
            new PetalBurstPerk(em),
            new HealPerk(),
            new HpRegenPerk(),
            new MaxHpPerk(),
            new MoveSpeedPerk(),
            new PickupRangePerk(),
            new ExpBonusPerk()
        };
    }

    #region --- Perk Kỹ Năng (Weapons / Skills) ---

    // 1. Sword Fusion (Hợp Thể Kiếm Khổng Lồ)
    private class SwordFusionPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;
        private readonly EntityQuery _flyingSwordQuery;
        private readonly EntityQuery _slashSwordQuery;

        public SwordFusionPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<SwordFusionTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
            _flyingSwordQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FlyingSwordSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
            _slashSwordQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SlashSwordSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "SwordFusion";
        public bool IsWeapon => true;
        public int Priority => 100; // Ưu tiên hàng đầu khi đủ điều kiện
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player)
        {
            int flyingLvl = !_flyingSwordQuery.IsEmpty ? _flyingSwordQuery.GetSingleton<SkillRuntimeData>().Level : 0;
            int slashLvl = !_slashSwordQuery.IsEmpty ? _slashSwordQuery.GetSingleton<SkillRuntimeData>().Level : 0;
            bool isFused = !_query.IsEmpty && _query.GetSingleton<SkillRuntimeData>().Level >= 1;

            return flyingLvl >= 4 && slashLvl >= 4 && !isFused;
        }

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.SwordFusionTitle,
                LevelBadge = GameLocalization.SwordFusionBadge,
                Description = GameLocalization.SwordFusionDesc,
                HighlightColor = new Color(1f, 0.85f, 0.15f),
                IsFusion = true,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = 1;
                runtime.Timer = 0.5f;
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 2. Thunder Boomerang Fusion (Boomerang + Electric)
    private class ThunderBoomerangPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;
        private readonly EntityQuery _boomerangQuery;
        private readonly EntityQuery _electricQuery;

        public ThunderBoomerangPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<ThunderBoomerangFusionTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
            _boomerangQuery = em.CreateEntityQuery(ComponentType.ReadOnly<BoomerangSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
            _electricQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ElectricSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "ThunderBoomerang";
        public bool IsWeapon => true;
        public int Priority => 100; // Ưu tiên hàng đầu khi đủ điều kiện
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player)
        {
            int boomLvl = !_boomerangQuery.IsEmpty ? _boomerangQuery.GetSingleton<SkillRuntimeData>().Level : 0;
            int elecLvl = !_electricQuery.IsEmpty ? _electricQuery.GetSingleton<SkillRuntimeData>().Level : 0;
            bool isFused = !_query.IsEmpty && _query.GetSingleton<SkillRuntimeData>().Level >= 1;

            return boomLvl >= 4 && elecLvl >= 4 && !isFused;
        }

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.ThunderBoomerangTitle,
                LevelBadge = GameLocalization.SwordFusionBadge,
                Description = GameLocalization.ThunderBoomerangDesc,
                HighlightColor = new Color(0.2f, 0.95f, 1f),
                IsFusion = true,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = 1;
                runtime.Timer = 0f;
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 3. Flying Sword (Phi Kiếm)
    private class FlyingSwordPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public FlyingSwordPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<FlyingSwordSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "FlyingSword";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int curLvl = GetCurrentLevel();
            int nextLvl = Mathf.Min(4, curLvl + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.FlyingSwordTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.FlyingSwordDesc(nextLvl),
                HighlightColor = new Color(0.2f, 0.9f, 1f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                runtime.Timer = 0.5f;
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 3. Slash Sword (Trảm Yêu Kiếm)
    private class SlashSwordPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public SlashSwordPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<SlashSwordSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "SlashSword";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int curLvl = GetCurrentLevel();
            int nextLvl = Mathf.Min(4, curLvl + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.SlashSwordTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.SlashSwordDesc(nextLvl),
                HighlightColor = new Color(0.85f, 0.45f, 1f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                runtime.Timer = 0.5f;
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 4. Projectile Skill (Đạn Ma Pháp / Arcane Volley)
    private class ProjectileSkillPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public ProjectileSkillPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "ProjectileSkill";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 5;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int curLvl = GetCurrentLevel();
            int nextLvl = curLvl + 1;
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.ProjectileSkillTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl),
                Description = GameLocalization.ProjectileSkillDesc(nextLvl),
                HighlightColor = new Color(1f, 0.75f, 0.2f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level += 1;
                runtime.Timer = 0.5f;
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 5. Fireball (Cầu Lửa Viêm Ma)
    private class FireballPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public FireballPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<FireballSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "Fireball";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int curLvl = GetCurrentLevel();
            int nextLvl = Mathf.Min(4, curLvl + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.FireballTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.FireballDesc(nextLvl),
                HighlightColor = new Color(1f, 0.42f, 0.12f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                runtime.Timer = 0.5f;
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 6. Shield (Khiên Hộ Thể)
    private class ShieldPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public ShieldPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<ShieldSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "Shield";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int nextLvl = Mathf.Min(4, GetCurrentLevel() + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.ShieldTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.ShieldDesc(nextLvl),
                HighlightColor = new Color(0f, 0.9f, 1f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 7. Boomerang (Phi Tiêu Hồi Toàn)
    private class BoomerangPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public BoomerangPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<BoomerangSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "Boomerang";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int nextLvl = Mathf.Min(4, GetCurrentLevel() + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.BoomerangTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.BoomerangDesc(nextLvl),
                HighlightColor = new Color(0.15f, 0.95f, 0.65f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 8. Electric (Lôi Điện Liên Hoàn)
    private class ElectricPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public ElectricPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<ElectricSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "Electric";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int nextLvl = Mathf.Min(4, GetCurrentLevel() + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.ElectricTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.ElectricDesc(nextLvl),
                HighlightColor = new Color(0f, 0.9f, 1f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    // 8. Fairy Petal Burst (Tiên Nữ Tán Hoa)
    private class PetalBurstPerk : IPerkDefinition
    {
        private readonly EntityQuery _query;

        public PetalBurstPerk(EntityManager em)
        {
            _query = em.CreateEntityQuery(ComponentType.ReadOnly<PetalBurstSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>(), ComponentType.Exclude<CloneSkillTag>());
        }

        public string PerkId => "PetalBurst";
        public bool IsWeapon => true;
        public int Priority => 0;
        public float Weight => 1f;

        private int GetCurrentLevel() => !_query.IsEmpty ? _query.GetSingleton<SkillRuntimeData>().Level : 0;

        public bool IsAvailable(EntityManager em, Entity player) => GetCurrentLevel() < 4;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            int nextLvl = Mathf.Min(4, GetCurrentLevel() + 1);
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = nextLvl,
                Title = GameLocalization.PetalBurstTitle,
                LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                Description = GameLocalization.PetalBurstDesc(nextLvl),
                HighlightColor = new Color(1.0f, 0.42f, 0.72f),
                IsFusion = false,
                IsWeapon = true
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (!_query.IsEmpty)
            {
                Entity skillEntity = _query.GetSingletonEntity();
                var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
                runtime.Level = Mathf.Min(4, runtime.Level + 1);
                em.SetComponentData(skillEntity, runtime);
            }
        }
    }

    #endregion

    #region --- Perk Chỉ Số Người Chơi (Player Stats) ---

    // 8. Heal (Tiên Dược Hồi Máu)
    private class HealPerk : IPerkDefinition
    {
        public string PerkId => "Heal";
        public bool IsWeapon => false;
        public int Priority => 0;
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player) => true;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.HealTitle,
                LevelBadge = GameLocalization.RecoveryBadge,
                Description = GameLocalization.HealDesc,
                HighlightColor = new Color(0.3f, 1f, 0.4f),
                IsFusion = false,
                IsWeapon = false
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (em.HasComponent<HealthData>(player))
            {
                var hp = em.GetComponentData<HealthData>(player);
                hp.CurrentHP = Mathf.Min(hp.MaxHP, hp.CurrentHP + hp.MaxHP * 0.4f);
                em.SetComponentData(player, hp);
            }
        }
    }

    // 9. Max HP (Thể Lực Cương Thể)
    private class MaxHpPerk : IPerkDefinition
    {
        public string PerkId => "MaxHp";
        public bool IsWeapon => false;
        public int Priority => 0;
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player) => true;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.MaxHpTitle,
                LevelBadge = GameLocalization.UpgradeBadge,
                Description = GameLocalization.MaxHpDesc,
                HighlightColor = new Color(1f, 0.35f, 0.45f),
                IsFusion = false,
                IsWeapon = false
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (em.HasComponent<HealthData>(player))
            {
                var hp = em.GetComponentData<HealthData>(player);
                hp.MaxHP += 30f;
                hp.CurrentHP += 30f;
                em.SetComponentData(player, hp);
            }
        }
    }

    // 10. Move Speed (Thần Hành Thuật)
    private class MoveSpeedPerk : IPerkDefinition
    {
        public string PerkId => "MoveSpeed";
        public bool IsWeapon => false;
        public int Priority => 0;
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player) => true;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.MoveSpeedTitle,
                LevelBadge = GameLocalization.UpgradeBadge,
                Description = GameLocalization.MoveSpeedDesc,
                HighlightColor = new Color(0.9f, 0.5f, 1f),
                IsFusion = false,
                IsWeapon = false
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (em.HasComponent<MovementData>(player))
            {
                var move = em.GetComponentData<MovementData>(player);
                move.MoveSpeed *= 1.15f;
                em.SetComponentData(player, move);
            }
        }
    }

    // 11. Pickup Range (Hút Linh Thạch)
    private class PickupRangePerk : IPerkDefinition
    {
        public string PerkId => "PickupRange";
        public bool IsWeapon => false;
        public int Priority => 0;
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player) => true;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.PickupRangeTitle,
                LevelBadge = GameLocalization.UtilityBadge,
                Description = GameLocalization.PickupRangeDesc,
                HighlightColor = new Color(0.2f, 0.8f, 0.9f),
                IsFusion = false,
                IsWeapon = false
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (em.HasComponent<PickupRangeData>(player))
            {
                var pr = em.GetComponentData<PickupRangeData>(player);
                pr.Range += 1.5f;
                em.SetComponentData(player, pr);
            }
        }
    }

    // 12. HP Regen (Hồi Xuân Thuật)
    private class HpRegenPerk : IPerkDefinition
    {
        public string PerkId => "HpRegen";
        public bool IsWeapon => false;
        public int Priority => 0;
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player) => true;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.HpRegenTitle,
                LevelBadge = GameLocalization.RecoveryBadge,
                Description = GameLocalization.HpRegenDesc,
                HighlightColor = new Color(0.2f, 0.95f, 0.4f),
                IsFusion = false,
                IsWeapon = false
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (em.HasComponent<PlayerHPRegenData>(player))
            {
                var regen = em.GetComponentData<PlayerHPRegenData>(player);
                regen.RegenPerSecond += 0.5f;
                em.SetComponentData(player, regen);
            }
            else
            {
                em.AddComponentData(player, new PlayerHPRegenData
                {
                    RegenPerSecond = 0.5f,
                    Timer = 0f
                });
            }
        }
    }

    // 13. Exp Bonus (Ngọc Tủy Tụ Khí / Spirit Crystal Essence - +10% EXP)
    private class ExpBonusPerk : IPerkDefinition
    {
        public string PerkId => "ExpBonus";
        public bool IsWeapon => false;
        public int Priority => 0;
        public float Weight => 1f;

        public bool IsAvailable(EntityManager em, Entity player) => true;

        public PerkOptionData GetOptionData(EntityManager em, Entity player)
        {
            return new PerkOptionData
            {
                PerkId = PerkId,
                Level = 1,
                Title = GameLocalization.ExpBonusTitle,
                LevelBadge = GameLocalization.UtilityBadge,
                Description = GameLocalization.ExpBonusDesc,
                HighlightColor = new Color(0.35f, 0.85f, 1f),
                IsFusion = false,
                IsWeapon = false
            };
        }

        public void Apply(EntityManager em, Entity player)
        {
            if (em.HasComponent<PlayerRelicData>(player))
            {
                var relic = em.GetComponentData<PlayerRelicData>(player);
                relic.ExpMultiplier *= 1.10f; // +10% EXP cho ván này
                em.SetComponentData(player, relic);
            }
        }
    }

    #endregion
}

