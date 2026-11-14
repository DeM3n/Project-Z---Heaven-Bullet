using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Đảm bảo toàn bộ Entity kỹ năng (và fusions) tồn tại trong ECS World với Level 0,
/// lấy dữ liệu chuẩn từ ScriptableObject (Single Source of Truth) và đồng bộ qua SkillConfigSyncBridge.
/// Tránh phụ thuộc vào OnUpdate của CastSystems chạy trước frame đầu tiên.
/// </summary>
public static class SkillBootstrapHelper
{
    public static void EnsureSkillEntitiesExist(EntityManager em, Entity player)
    {
        if (player == Entity.Null || !em.Exists(player)) return;

        bool anyCreated = false;

        // 1. Boomerang (Lấy chuẩn từ BoomerangSkillConfigSO)
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BoomerangSkillTag>()))
        {
            if (q.IsEmpty)
            {
                anyCreated = true;
                Entity e = em.CreateEntity();
                em.AddComponent<BoomerangSkillTag>(e);
                em.AddComponentData(e, new SkillOwnerData { Owner = player });

                var cfg = BoomerangSkillConfigSO.Instance;

                em.AddComponentData(e, new SkillRuntimeData
                {
                    Cooldown = cfg != null ? cfg.GetCooldown(1) : 8.1f,
                    Timer = 0.5f,
                    Level = 0,
                    SkillId = "boomerang"
                });

                em.AddComponentData(e, new BoomerangSkillData
                {
                    BoomerangPrefab = Entity.Null,
                    BaseDamage = cfg != null ? cfg.GetDamage(1) : 35f,
                    FlySpeed = cfg != null ? cfg.FlySpeed : 12.5f,
                    MaxDistance = cfg != null ? cfg.MaxDistance : 7.5f,
                    HitRadius = cfg != null ? cfg.GetHitRadius(1) : 0.55f,
                    CatchRadius = cfg != null ? cfg.CatchRadius : 1.4f,
                    DetectRange = cfg != null ? cfg.DetectRange : 9.5f,
                    EmpoweredMultiplier = cfg != null ? cfg.EmpoweredMultiplier : 1.6f,
                    BoomerangCount = cfg != null ? cfg.GetCount(1) : 1
                });

                em.AddComponentData(e, new BoomerangLevelBalanceData
                {
                    CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 8.1f,
                    CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 7.2f,
                    CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 6.3f,
                    CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 5.4f,

                    DamageLv1 = cfg != null ? cfg.GetDamage(1) : 35f,
                    DamageLv2 = cfg != null ? cfg.GetDamage(2) : 55f,
                    DamageLv3 = cfg != null ? cfg.GetDamage(3) : 55f,
                    DamageLv4 = cfg != null ? cfg.GetDamage(4) : 80f,

                    HitRadiusLv1 = cfg != null ? cfg.GetHitRadius(1) : 0.55f,
                    HitRadiusLv2 = cfg != null ? cfg.GetHitRadius(2) : 0.85f,
                    HitRadiusLv3 = cfg != null ? cfg.GetHitRadius(3) : 0.85f,
                    HitRadiusLv4 = cfg != null ? cfg.GetHitRadius(4) : 0.95f,

                    CountLv1 = cfg != null ? cfg.GetCount(1) : 1,
                    CountLv2 = cfg != null ? cfg.GetCount(2) : 1,
                    CountLv3 = cfg != null ? cfg.GetCount(3) : 2,
                    CountLv4 = cfg != null ? cfg.GetCount(4) : 2
                });
            }
        }

        // 2. Electric (Lấy chuẩn từ ElectricSkillConfigSO)
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ElectricSkillTag>()))
        {
            if (q.IsEmpty)
            {
                anyCreated = true;
                Entity e = em.CreateEntity();
                em.AddComponent<ElectricSkillTag>(e);
                em.AddComponentData(e, new SkillOwnerData { Owner = player });

                var cfg = ElectricSkillConfigSO.Instance;

                em.AddComponentData(e, new SkillRuntimeData
                {
                    Cooldown = cfg != null ? cfg.GetCooldown(1) : 5.4f,
                    Timer = 0.5f,
                    Level = 0,
                    SkillId = "electric"
                });

                em.AddComponentData(e, new ElectricSkillData
                {
                    BaseDamage = cfg != null ? cfg.GetDamage(1) : 60f,
                    DetectRange = cfg != null ? cfg.DetectRange : 9.5f,
                    ChainRange = cfg != null ? cfg.ChainRange : 6.0f,
                    ChainDamageRatio = cfg != null ? cfg.ChainDamageRatio : 0.30f,
                    PrimaryBoltCount = cfg != null ? cfg.GetPrimaryCount(1) : 1,
                    ChainCount = cfg != null ? cfg.GetChainCount(1) : 1,
                    CanBounceBack = false,
                    StunDuration = cfg != null ? cfg.StunDuration : 0.5f
                });

                em.AddComponentData(e, new ElectricLevelBalanceData
                {
                    CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 5.4f,
                    CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 4.86f,
                    CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 4.374f,
                    CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 4.374f,

                    DamageLv1 = cfg != null ? cfg.GetDamage(1) : 60f,
                    DamageLv2 = cfg != null ? cfg.GetDamage(2) : 90f,
                    DamageLv3 = cfg != null ? cfg.GetDamage(3) : 90f,
                    DamageLv4 = cfg != null ? cfg.GetDamage(4) : 120f,

                    PrimaryCountLv1 = cfg != null ? cfg.GetPrimaryCount(1) : 1,
                    PrimaryCountLv2 = cfg != null ? cfg.GetPrimaryCount(2) : 1,
                    PrimaryCountLv3 = cfg != null ? cfg.GetPrimaryCount(3) : 2,
                    PrimaryCountLv4 = cfg != null ? cfg.GetPrimaryCount(4) : 2,

                    ChainCountLv1 = cfg != null ? cfg.GetChainCount(1) : 1,
                    ChainCountLv2 = cfg != null ? cfg.GetChainCount(2) : 2,
                    ChainCountLv3 = cfg != null ? cfg.GetChainCount(3) : 2,
                    ChainCountLv4 = cfg != null ? cfg.GetChainCount(4) : 4
                });
            }
        }

        // 3. Fireball (Lấy chuẩn từ FireballSkillConfigSO)
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<FireballSkillTag>()))
        {
            if (q.IsEmpty)
            {
                anyCreated = true;
                Entity e = em.CreateEntity();
                em.AddComponent<FireballSkillTag>(e);
                em.AddComponentData(e, new SkillOwnerData { Owner = player });

                var cfg = FireballSkillConfigSO.Instance;

                em.AddComponentData(e, new SkillRuntimeData
                {
                    Cooldown = cfg != null ? cfg.GetCooldown(1) : 5.4f,
                    Timer = 0.5f,
                    Level = 0,
                    SkillId = "fireball"
                });

                em.AddComponentData(e, new FireballSkillData
                {
                    FireballPrefab = Entity.Null,
                    SubFireballPrefab = Entity.Null,
                    BaseDamage = cfg != null ? cfg.GetDamage(1) : 45f,
                    FireballSpeed = cfg != null ? cfg.FireballSpeed : 9.5f,
                    ExplosionRadius = cfg != null ? cfg.GetExplosionRadius(1) : 1.8f,
                    SubFireballSpeed = cfg != null ? cfg.SubFireballSpeed : 11.0f,
                    SubFireballLifetime = cfg != null ? cfg.SubFireballLifetime : 0.45f,
                    SubExplosionRadius = cfg != null ? cfg.SubExplosionRadius : 1.2f,
                    FireZoneRadius = cfg != null ? cfg.FireZoneRadius : 1.8f,
                    FireZoneDuration = cfg != null ? cfg.FireZoneDuration : 3.5f,
                    FireZoneTickInterval = cfg != null ? cfg.FireZoneTickInterval : 0.25f,
                    FireZoneDamagePerTick = cfg != null ? cfg.FireZoneDamagePerTick : 16f,
                    DetectRange = cfg != null ? cfg.DetectRange : 9.5f
                });

                em.AddComponentData(e, new FireballLevelBalanceData
                {
                    CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 5.4f,
                    CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 3.6f,
                    CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 2.7f,
                    CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 2.25f,

                    DamageLv1 = cfg != null ? cfg.GetDamage(1) : 45f,
                    DamageLv2 = cfg != null ? cfg.GetDamage(2) : 68f,
                    DamageLv3 = cfg != null ? cfg.GetDamage(3) : 95f,
                    DamageLv4 = cfg != null ? cfg.GetDamage(4) : 120f,

                    SubCountLv1 = cfg != null ? cfg.GetSubFireballCount(1) : 1,
                    SubCountLv2 = cfg != null ? cfg.GetSubFireballCount(2) : 2,
                    SubCountLv3 = cfg != null ? cfg.GetSubFireballCount(3) : 3,
                    SubCountLv4 = cfg != null ? cfg.GetSubFireballCount(4) : 4,

                    RadiusLv1 = cfg != null ? cfg.GetExplosionRadius(1) : 1.8f,
                    RadiusLv2 = cfg != null ? cfg.GetExplosionRadius(2) : 2.1f,
                    RadiusLv3 = cfg != null ? cfg.GetExplosionRadius(3) : 2.4f,
                    RadiusLv4 = cfg != null ? cfg.GetExplosionRadius(4) : 2.8f
                });
            }
        }

        // 4. PetalBurst (Lấy chuẩn từ PetalBurstSkillConfigSO)
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PetalBurstSkillTag>()))
        {
            if (q.IsEmpty)
            {
                anyCreated = true;
                Entity e = em.CreateEntity();
                em.AddComponent<PetalBurstSkillTag>(e);
                em.AddComponentData(e, new SkillOwnerData { Owner = player });

                var cfg = PetalBurstSkillConfigSO.Instance;

                em.AddComponentData(e, new SkillRuntimeData
                {
                    Cooldown = cfg != null ? cfg.GetCooldown(1) : 3.5f,
                    Timer = 0.5f,
                    Level = 0,
                    SkillId = "PetalBurst"
                });

                em.AddComponentData(e, new PetalBurstSkillData
                {
                    BaseDamage = cfg != null ? cfg.GetDamage(1) : 22f,
                    FlySpeed = cfg != null ? cfg.FlySpeed : 8.5f,
                    MinSpeedMultiplier = cfg != null ? cfg.MinSpeedMultiplier : 0.65f,
                    MaxSpeedMultiplier = cfg != null ? cfg.MaxSpeedMultiplier : 1.35f,
                    DragDeceleration = cfg != null ? cfg.DragDeceleration : 7.0f,
                    AngleSpreadJitter = cfg != null ? cfg.AngleSpreadJitter : 0.65f,
                    HitRadius = cfg != null ? cfg.HitRadius : 0.38f,
                    ExplosionRadius = cfg != null ? cfg.GetExplosionRadius(1) : 0.92f,
                    Duration = cfg != null ? cfg.GetDuration(1) : 0.85f,
                    PetalCount = cfg != null ? cfg.GetCount(1) : 6,
                    VortexPullRadius = cfg != null ? cfg.VortexPullRadius : 2.6f,
                    VortexPullForce = cfg != null ? cfg.VortexPullForce : 8.5f
                });

                em.AddComponentData(e, new PetalBurstLevelBalanceData
                {
                    DamageLv1 = cfg != null ? cfg.GetDamage(1) : 22f,
                    DamageLv2 = cfg != null ? cfg.GetDamage(2) : 32f,
                    DamageLv3 = cfg != null ? cfg.GetDamage(3) : 48f,
                    DamageLv4 = cfg != null ? cfg.GetDamage(4) : 70f,

                    CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 3.5f,
                    CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 3.0f,
                    CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 2.6f,
                    CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 2.2f,

                    CountLv1 = cfg != null ? cfg.GetCount(1) : 6,
                    CountLv2 = cfg != null ? cfg.GetCount(2) : 9,
                    CountLv3 = cfg != null ? cfg.GetCount(3) : 13,
                    CountLv4 = cfg != null ? cfg.GetCount(4) : 18,

                    DurationLv1 = cfg != null ? cfg.GetDuration(1) : 0.85f,
                    DurationLv2 = cfg != null ? cfg.GetDuration(2) : 0.95f,
                    DurationLv3 = cfg != null ? cfg.GetDuration(3) : 1.10f,
                    DurationLv4 = cfg != null ? cfg.GetDuration(4) : 1.25f,

                    ExplosionRadiusLv1 = cfg != null ? cfg.GetExplosionRadius(1) : 0.92f,
                    ExplosionRadiusLv2 = cfg != null ? cfg.GetExplosionRadius(2) : 1.03f,
                    ExplosionRadiusLv3 = cfg != null ? cfg.GetExplosionRadius(3) : 1.19f,
                    ExplosionRadiusLv4 = cfg != null ? cfg.GetExplosionRadius(4) : 1.46f
                });
            }
        }

        // 5. Shield
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ShieldSkillTag>()))
        {
            if (q.IsEmpty)
            {
                anyCreated = true;
                Entity e = em.CreateEntity();
                em.AddComponent<ShieldSkillTag>(e);
                em.AddComponentData(e, new SkillOwnerData { Owner = player });
                em.AddComponentData(e, new SkillRuntimeData
                {
                    Cooldown = 27f,
                    Timer = 0f,
                    Level = 0,
                    SkillId = "shield"
                });
                em.AddComponentData(e, new ShieldSkillData
                {
                    BaseCooldown = 27f,
                    Lv2Cooldown = 16.2f,
                    Lv3Cooldown = 14.4f,
                    Lv4Cooldown = 12.6f,
                    ExplosionRadius = 4.5f,
                    ExplosionDamage = 100f,
                    KnockbackForce = 2.5f
                });
            }
        }

        // Đảm bảo Player có PlayerShieldData
        if (!em.HasComponent<PlayerShieldData>(player))
        {
            em.AddComponentData(player, new PlayerShieldData
            {
                SkillLevel = 0,
                MaxCharges = 1,
                CurrentCharges = 0,
                HitsPerShield = 1,
                HitsRemainingInCurrentShield = 0,
                RechargeCooldown = 27f,
                RechargeTimer = 0f
            });
        }

        // 6. ThunderBoomerang
        using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ThunderBoomerangFusionTag>()))
        {
            if (q.IsEmpty)
            {
                anyCreated = true;
                Entity e = em.CreateEntity();
                em.AddComponent<ThunderBoomerangFusionTag>(e);
                em.AddComponentData(e, new SkillOwnerData { Owner = player });
                em.AddComponentData(e, new SkillRuntimeData
                {
                    Cooldown = 0f,
                    Timer = 0f,
                    Level = 0,
                    SkillId = "thunder_boomerang"
                });
                em.AddComponentData(e, new ThunderBoomerangSkillData
                {
                    BoltDamage = 90f,
                    DetectRange = 8.5f,
                    ChainRange = 6.0f,
                    ChainCount = 2,
                    BoltsPerDischarge = 2,
                    MaxDischarges = 2,
                    InitialDelay = 0.5f,
                    DischargeInterval = 1.0f,
                    StunDuration = 0.5f
                });
            }
        }

        // Tự động kích hoạt đồng bộ toàn bộ SO SSOT qua SkillConfigSyncBridge nếu vừa tạo mới Entity
        if (anyCreated && SkillConfigSyncBridge.Instance != null)
        {
            SkillConfigSyncBridge.Instance.SyncAllConfigs();
        }
    }
}
