using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý vòng đời của ván đấu (Run Lifecycle), bao gồm:
/// - Đếm giờ sinh tồn (Survival Timer).
/// - Phát hiện sự kiện người chơi chết (PlayerDeadTag) và tính thưởng Linh Thạch sinh tồn.
/// - Dọn dẹp entities tàn dư từ ván trước khi Restart/Retry (CleanupOldRunEntities).
/// - Điều hướng chuyển Scene (Restart ván đấu, trở về MainMenu).
/// </summary>
public class RunLifecycleService
{
    private readonly EntityManager _entityManager;
    private readonly EntityQuery _playerDeadQuery;

    private float _survivalTime = 0f;
    private int _stonesEarnedThisRun = 0;

    public static RunLifecycleService Instance { get; private set; }

    public float SurvivalTime => _survivalTime;
    public int StonesEarnedThisRun => _stonesEarnedThisRun;

    public RunLifecycleService(EntityManager em)
    {
        Instance = this;
        _entityManager = em;
        _playerDeadQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<PlayerDeadTag>()
        );
    }

    public void ResetRunState()
    {
        _survivalTime = 0f;
        _stonesEarnedThisRun = 0;

        // Reset PlayerRunStats
        var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerRunStats>());
        if (!query.IsEmpty)
        {
            var entity = query.GetSingletonEntity();
            _entityManager.SetComponentData(entity, new PlayerRunStats());
        }

        // Reset SkillDamageRecord trên RunStats singleton (chỉ Clear buffer, KHÔNG destroy entity)
        var runStatsQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RunStatsTag>());
        if (!runStatsQuery.IsEmpty)
        {
            var statsEntity = runStatsQuery.GetSingletonEntity();
            if (_entityManager.HasBuffer<SkillDamageRecord>(statsEntity))
            {
                var buffer = _entityManager.GetBuffer<SkillDamageRecord>(statsEntity);
                buffer.Clear();
            }
        }

        // Reset WaveManagerData singleton
        var waveQuery = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>());
        if (!waveQuery.IsEmpty)
        {
            var waveEntity = waveQuery.GetSingletonEntity();
            _entityManager.SetComponentData(waveEntity, new WaveManagerData
            {
                State = WaveState.NormalTimeline,
                ElapsedGameTime = 0f,
                IsTimerPaused = false,
                ActiveMegaWaveMinute = 0,
                MegaWaveDuration = 60f,
                MegaWaveTimer = 0f,
                MegaWaveSubSpawnTimer = 0f,
                MegaWaveSurgeStage = 0,
                MegaWaveEnemiesKilled = 0,
                MegaWaveRemainingEnemies = 0,
                MegaWaveTotalEnemies = 0,
                Triggered3Min = 0,
                Triggered6Min = 0,
                Triggered9Min = 0
            });
        }
    }

    public void AddStones(int amount)
    {
        _stonesEarnedThisRun += amount;
    }

    public void UpdateTimer(float deltaTime)
    {
        if (Time.timeScale > 0f)
        {
            // Kiểm tra trạng thái đóng băng thời gian của Mega Wave
            var waveQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<WaveManagerData>());
            bool isTimerPaused = false;
            if (!waveQuery.IsEmpty)
            {
                isTimerPaused = waveQuery.GetSingleton<WaveManagerData>().IsTimerPaused;
            }

            if (!isTimerPaused)
            {
                _survivalTime += deltaTime;
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem người chơi đã chết hay chưa (gắn PlayerDeadTag).
    /// </summary>
    public bool CheckPlayerDead()
    {
        return !_playerDeadQuery.IsEmpty;
    }

    /// <summary>
    /// Tính toán thưởng Linh Thạch sinh tồn và tạo GameOverStats khi người chơi hy sinh.
    /// </summary>
    public GameOverStats CalculateGameOverStats(int finalPlayerLevel)
    {
        float goldMult = MetaProgressionService.Instance != null ? MetaProgressionService.Instance.GetGoldMultiplier() : 1f;
        int survivalReward = Mathf.RoundToInt((_survivalTime / 60f) * 15f * goldMult);

        if (survivalReward > 0)
        {
            _stonesEarnedThisRun += survivalReward;
            MetaProgressionService.Instance?.AddSpiritStones(survivalReward);
        }

        return new GameOverStats
        {
            SurvivalTime = _survivalTime,
            FinalLevel = finalPlayerLevel,
            StonesEarned = _stonesEarnedThisRun
        };
    }

    /// <summary>
    /// Dọn sạch quái vật, đạn, ngọc EXP, vật phẩm rơi, kiếm và trạng thái chết còn sót lại từ ván trước khi Retry/Restart trận đấu.
    /// </summary>
    public void CleanupOldRunEntities()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var em = world.EntityManager;

        // 1. Dọn các thực thể dynamic spawn tại runtime
        using (var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<ProjectileTag>(),
                ComponentType.ReadOnly<EnemyProjectileTag>(),
                ComponentType.ReadOnly<ExpOrbTag>(),
                ComponentType.ReadOnly<PickupItemTag>(),
                ComponentType.ReadOnly<FlyingSwordTag>(),
                ComponentType.ReadOnly<SlashSwordTag>(),
                ComponentType.ReadOnly<SwordWaveTag>(),
                ComponentType.ReadOnly<GiantSwordTag>(),
                ComponentType.ReadOnly<FireballTag>(),
                ComponentType.ReadOnly<SubFireballTag>(),
                ComponentType.ReadOnly<BoomerangTag>(),
                ComponentType.ReadOnly<BoomerangCatchEvent>(),
                ComponentType.ReadOnly<ElectricArcEvent>(),
                ComponentType.ReadOnly<ChestOpenEvent>()
            }
        }))
        {
            em.DestroyEntity(query);
        }

        // 2. Dọn PlayerDeadTag nếu còn dính trên player entity
        using (var deadQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<PlayerDeadTag>()))
        {
            var deadPlayers = deadQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < deadPlayers.Length; i++)
            {
                em.RemoveComponent<PlayerDeadTag>(deadPlayers[i]);
            }
            deadPlayers.Dispose();
        }
    }

    public void RestartCurrentScene()
    {
        PauseService.ClearAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        PauseService.ClearAll();
        SceneManager.LoadScene("MainMenuScene");
    }
}

