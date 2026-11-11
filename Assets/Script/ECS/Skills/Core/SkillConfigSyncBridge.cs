using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Cầu nối Trung Tâm đồng bộ thông số Cân Bằng (Balance Data) từ các ScriptableObject (SSOT)
/// vào các ECS Component Data tương ứng trong Pure ECS World.
/// 
/// Kiến trúc & Trách nhiệm (Single Responsibility Principle):
/// 1. Là điểm tập trung duy nhất (Single Source of Truth Bridge) kết nối tầng ScriptableObject (.asset) với ECS.
/// 2. Loại bỏ hoàn toàn sự trùng lặp code sync cấu hình rải rác ở các VisualBridge (Boomerang, Electric, v.v.).
/// 3. Hỗ trợ Live Tuning trong Editor: Khi Designer thay đổi thông số trên file .asset lúc Play Mode,
///    hệ thống tự động cập nhật xuống ECS World định kỳ mỗi 0.5s mà không cần recompile hay khởi động lại game.
/// 4. Hỗ trợ cơ chế mở rộng (Open/Closed Principle) cho toàn bộ các kỹ năng trong game (Boomerang, Electric, Fireball, SlashSword, FlyingSword, PetalBurst).
/// </summary>
[DefaultExecutionOrder(-50)]
public class SkillConfigSyncBridge : MonoBehaviour
{
    private static SkillConfigSyncBridge _instance;
    public static SkillConfigSyncBridge Instance => _instance;

    private EntityManager _entityManager;
    private bool _initialized;

    // Entity queries cho từng skill
    private EntityQuery _boomerangQuery;
    private EntityQuery _electricQuery;
    private EntityQuery _fireballQuery;
    private EntityQuery _slashSwordQuery;
    private EntityQuery _flyingSwordQuery;
    private EntityQuery _petalBurstQuery;

    private float _pollTimer = 0f;
    private const float EditorPollInterval = 0.5f;
    private const float RuntimePollInterval = 1.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<SkillConfigSyncBridge>() == null)
        {
            var go = new GameObject("[SkillConfigSyncBridge]");
            go.AddComponent<SkillConfigSyncBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitECS();
        SyncAllConfigs();
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;

        _boomerangQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<BoomerangSkillTag>(),
            ComponentType.ReadWrite<BoomerangSkillData>(),
            ComponentType.ReadWrite<BoomerangLevelBalanceData>()
        );

        _electricQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ElectricSkillTag>(),
            ComponentType.ReadWrite<ElectricSkillData>(),
            ComponentType.ReadWrite<ElectricLevelBalanceData>()
        );

        _fireballQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FireballSkillTag>(),
            ComponentType.ReadWrite<FireballSkillData>(),
            ComponentType.ReadWrite<FireballLevelBalanceData>()
        );

        _slashSwordQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SlashSwordSkillTag>(),
            ComponentType.ReadWrite<SlashSwordSkillData>(),
            ComponentType.ReadWrite<SlashSwordLevelBalanceData>()
        );

        _flyingSwordQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FlyingSwordSkillTag>(),
            ComponentType.ReadWrite<FlyingSwordSkillData>(),
            ComponentType.ReadWrite<FlyingSwordLevelBalanceData>()
        );

        _petalBurstQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PetalBurstSkillTag>(),
            ComponentType.ReadWrite<PetalBurstSkillData>(),
            ComponentType.ReadWrite<PetalBurstLevelBalanceData>()
        );

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        float dt = Time.deltaTime;
        _pollTimer -= dt;

#if UNITY_EDITOR
        float interval = EditorPollInterval;
#else
        float interval = RuntimePollInterval;
#endif

        if (_pollTimer <= 0f)
        {
            _pollTimer = interval;
            SyncAllConfigs();
        }
    }

    /// <summary>
    /// Đồng bộ toàn bộ thông số từ tất cả ScriptableObject cấu hình xuống ECS World
    /// </summary>
    [ContextMenu("Force Sync All Skills")]
    public void SyncAllConfigs()
    {
        if (!_initialized && !InitECS()) return;

        SyncBoomerang();
        SyncElectric();
        SyncFireball();
        SyncSlashSword();
        SyncFlyingSword();
        SyncPetalBurst();
    }

    // =========================================================================
    // 1. BOOMERANG CONFIG SYNC
    // =========================================================================
    private void SyncBoomerang()
    {
        if (_boomerangQuery == default || _boomerangQuery.IsEmpty) return;
        var cfg = BoomerangSkillConfigSO.Instance;
        if (cfg == null) return;

        using var entities = _boomerangQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];

            var skillData = _entityManager.GetComponentData<BoomerangSkillData>(e);
            skillData.FlySpeed = cfg.FlySpeed;
            skillData.MaxDistance = cfg.MaxDistance;
            skillData.CatchRadius = cfg.CatchRadius;
            skillData.DetectRange = cfg.DetectRange;
            skillData.EmpoweredMultiplier = cfg.EmpoweredMultiplier;
            _entityManager.SetComponentData(e, skillData);

            var balance = new BoomerangLevelBalanceData
            {
                CooldownLv1 = cfg.GetCooldown(1),
                CooldownLv2 = cfg.GetCooldown(2),
                CooldownLv3 = cfg.GetCooldown(3),
                CooldownLv4 = cfg.GetCooldown(4),

                DamageLv1 = cfg.GetDamage(1),
                DamageLv2 = cfg.GetDamage(2),
                DamageLv3 = cfg.GetDamage(3),
                DamageLv4 = cfg.GetDamage(4),

                HitRadiusLv1 = cfg.GetHitRadius(1),
                HitRadiusLv2 = cfg.GetHitRadius(2),
                HitRadiusLv3 = cfg.GetHitRadius(3),
                HitRadiusLv4 = cfg.GetHitRadius(4),

                CountLv1 = cfg.GetCount(1),
                CountLv2 = cfg.GetCount(2),
                CountLv3 = cfg.GetCount(3),
                CountLv4 = cfg.GetCount(4)
            };
            _entityManager.SetComponentData(e, balance);

            if (_entityManager.HasComponent<SkillRuntimeData>(e))
            {
                var runtime = _entityManager.GetComponentData<SkillRuntimeData>(e);
                if (runtime.Level > 0)
                {
                    runtime.Cooldown = cfg.GetCooldown(runtime.Level);
                    _entityManager.SetComponentData(e, runtime);
                }
            }
        }
    }

    // =========================================================================
    // 2. ELECTRIC CONFIG SYNC
    // =========================================================================
    private void SyncElectric()
    {
        if (_electricQuery == default || _electricQuery.IsEmpty) return;
        var cfg = ElectricSkillConfigSO.Instance;
        if (cfg == null) return;

        using var entities = _electricQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];

            var skillData = _entityManager.GetComponentData<ElectricSkillData>(e);
            skillData.DetectRange = cfg.DetectRange;
            skillData.ChainRange = cfg.ChainRange;
            skillData.ChainDamageRatio = cfg.ChainDamageRatio;
            skillData.StunDuration = cfg.StunDuration;
            _entityManager.SetComponentData(e, skillData);

            var balance = new ElectricLevelBalanceData
            {
                CooldownLv1 = cfg.GetCooldown(1),
                CooldownLv2 = cfg.GetCooldown(2),
                CooldownLv3 = cfg.GetCooldown(3),
                CooldownLv4 = cfg.GetCooldown(4),

                DamageLv1 = cfg.GetDamage(1),
                DamageLv2 = cfg.GetDamage(2),
                DamageLv3 = cfg.GetDamage(3),
                DamageLv4 = cfg.GetDamage(4),

                PrimaryCountLv1 = cfg.GetPrimaryCount(1),
                PrimaryCountLv2 = cfg.GetPrimaryCount(2),
                PrimaryCountLv3 = cfg.GetPrimaryCount(3),
                PrimaryCountLv4 = cfg.GetPrimaryCount(4),

                ChainCountLv1 = cfg.GetChainCount(1),
                ChainCountLv2 = cfg.GetChainCount(2),
                ChainCountLv3 = cfg.GetChainCount(3),
                ChainCountLv4 = cfg.GetChainCount(4)
            };
            _entityManager.SetComponentData(e, balance);

            if (_entityManager.HasComponent<SkillRuntimeData>(e))
            {
                var runtime = _entityManager.GetComponentData<SkillRuntimeData>(e);
                if (runtime.Level > 0)
                {
                    runtime.Cooldown = cfg.GetCooldown(runtime.Level);
                    _entityManager.SetComponentData(e, runtime);
                }
            }
        }
    }

    // =========================================================================
    // 3. FIREBALL CONFIG SYNC
    // =========================================================================
    private void SyncFireball()
    {
        if (_fireballQuery == default || _fireballQuery.IsEmpty) return;
        var cfg = FireballSkillConfigSO.Instance;
        if (cfg == null) return;

        using var entities = _fireballQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];

            var skillData = _entityManager.GetComponentData<FireballSkillData>(e);
            skillData.BaseDamage = cfg.GetDamage(1);
            skillData.FireballSpeed = cfg.FireballSpeed;
            skillData.ExplosionRadius = cfg.GetExplosionRadius(1);
            skillData.SubFireballSpeed = cfg.SubFireballSpeed;
            skillData.SubFireballLifetime = cfg.SubFireballLifetime;
            skillData.SubExplosionRadius = cfg.SubExplosionRadius;
            skillData.FireZoneRadius = cfg.FireZoneRadius;
            skillData.FireZoneDuration = cfg.FireZoneDuration;
            skillData.FireZoneTickInterval = cfg.FireZoneTickInterval;
            skillData.FireZoneDamagePerTick = cfg.FireZoneDamagePerTick;
            skillData.DetectRange = cfg.DetectRange;
            _entityManager.SetComponentData(e, skillData);

            var balance = new FireballLevelBalanceData
            {
                CooldownLv1 = cfg.GetCooldown(1),
                CooldownLv2 = cfg.GetCooldown(2),
                CooldownLv3 = cfg.GetCooldown(3),
                CooldownLv4 = cfg.GetCooldown(4),

                DamageLv1 = cfg.GetDamage(1),
                DamageLv2 = cfg.GetDamage(2),
                DamageLv3 = cfg.GetDamage(3),
                DamageLv4 = cfg.GetDamage(4),

                SubCountLv1 = cfg.GetSubFireballCount(1),
                SubCountLv2 = cfg.GetSubFireballCount(2),
                SubCountLv3 = cfg.GetSubFireballCount(3),
                SubCountLv4 = cfg.GetSubFireballCount(4),

                RadiusLv1 = cfg.GetExplosionRadius(1),
                RadiusLv2 = cfg.GetExplosionRadius(2),
                RadiusLv3 = cfg.GetExplosionRadius(3),
                RadiusLv4 = cfg.GetExplosionRadius(4)
            };
            _entityManager.SetComponentData(e, balance);

            if (_entityManager.HasComponent<SkillRuntimeData>(e))
            {
                var runtime = _entityManager.GetComponentData<SkillRuntimeData>(e);
                if (runtime.Level > 0)
                {
                    runtime.Cooldown = cfg.GetCooldown(runtime.Level);
                    _entityManager.SetComponentData(e, runtime);
                }
            }
        }
    }

    // =========================================================================
    // 4. SLASH SWORD CONFIG SYNC
    // =========================================================================
    private void SyncSlashSword()
    {
        if (_slashSwordQuery == default || _slashSwordQuery.IsEmpty) return;
        var cfg = SlashSwordSkillConfigSO.Instance;
        if (cfg == null) return;

        using var entities = _slashSwordQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];

            var skillData = _entityManager.GetComponentData<SlashSwordSkillData>(e);
            skillData.BaseDamage = cfg.GetDamage(1);
            skillData.ApproachSpeed = cfg.GetApproachSpeed(1);
            skillData.ReturnSpeed = cfg.ReturnSpeed;
            skillData.SlashRadius = cfg.GetSlashRadius(1);
            skillData.SlashAngleDeg = cfg.SlashAngleDeg;
            skillData.DetectRange = cfg.DetectRange;
            skillData.WaveSpeed = cfg.WaveSpeed;
            skillData.WaveDuration = cfg.WaveDuration;
            skillData.WaveDamage = cfg.WaveDamage;
            skillData.WaveRadius = cfg.WaveRadius;
            _entityManager.SetComponentData(e, skillData);

            var balance = new SlashSwordLevelBalanceData
            {
                CooldownLv1 = cfg.GetCooldown(1),
                CooldownLv2 = cfg.GetCooldown(2),
                CooldownLv3 = cfg.GetCooldown(3),
                CooldownLv4 = cfg.GetCooldown(4),

                DamageLv1 = cfg.GetDamage(1),
                DamageLv2 = cfg.GetDamage(2),
                DamageLv3 = cfg.GetDamage(3),
                DamageLv4 = cfg.GetDamage(4),

                SlashRadiusLv1 = cfg.GetSlashRadius(1),
                SlashRadiusLv2 = cfg.GetSlashRadius(2),
                SlashRadiusLv3 = cfg.GetSlashRadius(3),
                SlashRadiusLv4 = cfg.GetSlashRadius(4),

                SwordCountLv1 = cfg.GetSwordCount(1),
                SwordCountLv2 = cfg.GetSwordCount(2),
                SwordCountLv3 = cfg.GetSwordCount(3),
                SwordCountLv4 = cfg.GetSwordCount(4),

                ApproachSpeedLv1 = cfg.GetApproachSpeed(1),
                ApproachSpeedLv2 = cfg.GetApproachSpeed(2),
                ApproachSpeedLv3 = cfg.GetApproachSpeed(3),
                ApproachSpeedLv4 = cfg.GetApproachSpeed(4),

                WaveDamageLv4 = cfg.WaveDamage
            };
            _entityManager.SetComponentData(e, balance);

            if (_entityManager.HasComponent<SkillRuntimeData>(e))
            {
                var runtime = _entityManager.GetComponentData<SkillRuntimeData>(e);
                if (runtime.Level > 0)
                {
                    runtime.Cooldown = cfg.GetCooldown(runtime.Level);
                    _entityManager.SetComponentData(e, runtime);
                }
            }
        }
    }

    // =========================================================================
    // 5. FLYING SWORD CONFIG SYNC
    // =========================================================================
    private void SyncFlyingSword()
    {
        if (_flyingSwordQuery == default || _flyingSwordQuery.IsEmpty) return;
        var cfg = FlyingSwordSkillConfigSO.Instance;
        if (cfg == null) return;

        using var entities = _flyingSwordQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];

            var skillData = _entityManager.GetComponentData<FlyingSwordSkillData>(e);
            skillData.BaseDamage = cfg.GetDamage(1);
            skillData.RushSpeed = cfg.GetRushSpeed(1);
            skillData.ReturnSpeed = cfg.ReturnSpeed;
            skillData.OrbitRadius = cfg.OrbitRadius;
            skillData.OrbitSpeed = cfg.OrbitSpeed;
            skillData.AttackInterval = cfg.AttackInterval;
            skillData.HitRadius = cfg.HitRadius;
            skillData.AoeRadius = cfg.AoeRadius;
            skillData.AoeDamage = cfg.AoeDamage;
            skillData.DetectRange = cfg.DetectRange;
            skillData.MaxLeashRange = cfg.MaxLeashRange;
            skillData.UltimateCooldown = cfg.UltimateCooldown;
            _entityManager.SetComponentData(e, skillData);

            var balance = new FlyingSwordLevelBalanceData
            {
                DamageLv1 = cfg.GetDamage(1),
                DamageLv2 = cfg.GetDamage(2),
                DamageLv3 = cfg.GetDamage(3),
                DamageLv4 = cfg.GetDamage(4),

                RushSpeedLv1 = cfg.GetRushSpeed(1),
                RushSpeedLv2 = cfg.GetRushSpeed(2),
                RushSpeedLv3 = cfg.GetRushSpeed(3),
                RushSpeedLv4 = cfg.GetRushSpeed(4),

                SwordCountLv1 = cfg.GetSwordCount(1),
                SwordCountLv2 = cfg.GetSwordCount(2),
                SwordCountLv3 = cfg.GetSwordCount(3),
                SwordCountLv4 = cfg.GetSwordCount(4)
            };
            _entityManager.SetComponentData(e, balance);
        }
    }

    // =========================================================================
    // 6. PETAL BURST (TIÊN NỮ TÁN HOA) CONFIG SYNC
    // =========================================================================
    private void SyncPetalBurst()
    {
        if (_petalBurstQuery == default || _petalBurstQuery.IsEmpty) return;
        var cfg = PetalBurstSkillConfigSO.Instance;
        if (cfg == null) return;

        using var entities = _petalBurstQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];

            var skillData = _entityManager.GetComponentData<PetalBurstSkillData>(e);
            skillData.BaseDamage = cfg.GetDamage(1);
            skillData.FlySpeed = cfg.FlySpeed;
            skillData.MinSpeedMultiplier = cfg.MinSpeedMultiplier;
            skillData.MaxSpeedMultiplier = cfg.MaxSpeedMultiplier;
            skillData.DragDeceleration = cfg.DragDeceleration;
            skillData.AngleSpreadJitter = cfg.AngleSpreadJitter;
            skillData.HitRadius = cfg.HitRadius;
            skillData.ExplosionRadius = cfg.GetExplosionRadius(1);
            skillData.Duration = cfg.GetDuration(1);
            skillData.PetalCount = cfg.GetCount(1);
            skillData.VortexPullRadius = cfg.VortexPullRadius;
            skillData.VortexPullForce = cfg.VortexPullForce;
            _entityManager.SetComponentData(e, skillData);

            var balance = new PetalBurstLevelBalanceData
            {
                DamageLv1 = cfg.GetDamage(1),
                DamageLv2 = cfg.GetDamage(2),
                DamageLv3 = cfg.GetDamage(3),
                DamageLv4 = cfg.GetDamage(4),

                CooldownLv1 = cfg.GetCooldown(1),
                CooldownLv2 = cfg.GetCooldown(2),
                CooldownLv3 = cfg.GetCooldown(3),
                CooldownLv4 = cfg.GetCooldown(4),

                CountLv1 = cfg.GetCount(1),
                CountLv2 = cfg.GetCount(2),
                CountLv3 = cfg.GetCount(3),
                CountLv4 = cfg.GetCount(4),

                DurationLv1 = cfg.GetDuration(1),
                DurationLv2 = cfg.GetDuration(2),
                DurationLv3 = cfg.GetDuration(3),
                DurationLv4 = cfg.GetDuration(4),

                ExplosionRadiusLv1 = cfg.GetExplosionRadius(1),
                ExplosionRadiusLv2 = cfg.GetExplosionRadius(2),
                ExplosionRadiusLv3 = cfg.GetExplosionRadius(3),
                ExplosionRadiusLv4 = cfg.GetExplosionRadius(4)
            };
            _entityManager.SetComponentData(e, balance);
        }
    }
}
