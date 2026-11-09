using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR || DEBUG

/// <summary>
/// Snapshot trạng thái tiến trình Wave trận đấu cung cấp cho Dev UI.
/// </summary>
public struct DevWaveStatus
{
    public float ElapsedTime;
    public WaveState State;
    public int ActiveMegaWaveMinute;
    public float MegaWaveTimer;
    public byte SurgeStage;
    public int EnemiesKilled;
    public bool IsTimerPaused;
}

/// <summary>
/// Quản lý toàn bộ nghiệp vụ gian lận (Cheats) phục vụ kiểm thử game:
/// - God Mode: Sử dụng cơ chế miễn thương (CurrentTimer = float.MaxValue) của PlayerDamageResolutionSystem.
/// - Speed Boost: Dùng hệ số nhân/chia (multiplier), không ghi đè giá trị gốc.
/// - Kill All: Đưa máu về 0 để đi qua Death pipeline và rớt ngọc EXP bình thường.
/// - Wave & Timeline Controller: Kích hoạt tức thời Mega Wave 3, 6, 9, nhảy thời gian, spawn vật phẩm.
/// - WasCheatedThisRun: Tự động vô hiệu hóa tích lũy Linh Thạch nếu ván chơi có sử dụng cheat.
/// </summary>
public class DevCheatService
{
    public bool IsGodMode { get; private set; }
    public bool IsSpeedBoosted { get; private set; }
    public bool WasCheatedThisRun { get; private set; }

    private EntityManager EntityManager
    {
        get
        {
            var world = World.DefaultGameObjectInjectionWorld;
            return (world != null && world.IsCreated) ? world.EntityManager : default;
        }
    }

    public void Reset()
    {
        IsGodMode = false;
        IsSpeedBoosted = false;
        WasCheatedThisRun = false;
    }

    #region --- Player Cheats ---

    public void ToggleGodMode()
    {
        IsGodMode = !IsGodMode;
        WasCheatedThisRun = true;

        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadWrite<PlayerInvulnerabilityData>(), ComponentType.ReadWrite<HealthData>());
        if (query.IsEmpty) return;

        Entity player = query.GetSingletonEntity();
        var invuln = em.GetComponentData<PlayerInvulnerabilityData>(player);
        invuln.CurrentTimer = IsGodMode ? float.MaxValue : 0f;
        em.SetComponentData(player, invuln);

        HealFull();
    }

    public void ToggleSpeedBoost()
    {
        IsSpeedBoosted = !IsSpeedBoosted;
        WasCheatedThisRun = true;

        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadWrite<MovementData>());
        if (query.IsEmpty) return;

        Entity player = query.GetSingletonEntity();
        var move = em.GetComponentData<MovementData>(player);

        if (IsSpeedBoosted)
        {
            move.MoveSpeed *= 2.2f;
        }
        else
        {
            move.MoveSpeed /= 2.2f;
        }

        em.SetComponentData(player, move);
    }

    public void HealFull()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadWrite<HealthData>());
        if (query.IsEmpty) return;

        Entity player = query.GetSingletonEntity();
        var hp = em.GetComponentData<HealthData>(player);
        hp.CurrentHP = hp.MaxHP;
        em.SetComponentData(player, hp);
    }

    public void LevelUpPlayer()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadWrite<PlayerLevelData>());
        if (query.IsEmpty) return;

        Entity player = query.GetSingletonEntity();
        var lvl = em.GetComponentData<PlayerLevelData>(player);
        lvl.Level += 1;
        lvl.CurrentExp = 0;
        em.SetComponentData(player, lvl);

        // +10 Máu Tối Đa (Max HP) cho Player khi Level Up
        if (em.HasComponent<HealthData>(player))
        {
            var hp = em.GetComponentData<HealthData>(player);
            hp.MaxHP += 10f;
            hp.CurrentHP += 10f;
            em.SetComponentData(player, hp);
        }
    }

    public void KillAllEnemies()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyTag>(), ComponentType.ReadWrite<HealthData>());
        if (query.IsEmpty) return;

        using var enemies = query.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (em.Exists(enemies[i]) && em.HasComponent<HealthData>(enemies[i]))
            {
                var hp = em.GetComponentData<HealthData>(enemies[i]);
                hp.CurrentHP = 0f; // Đi qua DeathCleanupSystem để rơi ngọc EXP
                em.SetComponentData(enemies[i], hp);
            }
        }
    }

    public void AddDevRegen()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadWrite<PlayerHPRegenData>());
        if (query.IsEmpty) return;

        Entity player = query.GetSingletonEntity();
        var regen = em.GetComponentData<PlayerHPRegenData>(player);
        regen.RegenPerSecond += 0.5f;
        em.SetComponentData(player, regen);
    }

    public void AddSpiritStones(int amount)
    {
        WasCheatedThisRun = true;
        MetaProgressionService.Instance?.AddSpiritStones(amount);
    }

    public void SpawnTreasureChest()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var pQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>());
        if (pQuery.IsEmpty) return;

        float3 playerPos = pQuery.GetSingleton<LocalTransform>().Position;
        Entity chest = em.CreateEntity();
        em.AddComponentData(chest, LocalTransform.FromPosition(playerPos + new float3(1.2f, 0.4f, 0f)));
        em.AddComponentData(chest, new PickupItemTag());
        em.AddComponentData(chest, new PickupData
        {
            Type = PickupType.TreasureChest,
            Value = 0f,
            Radius = 0.9f,
            FlySpeed = 0f
        });
    }

    public void SpawnExpMagnet()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var pQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>());
        if (pQuery.IsEmpty) return;

        float3 playerPos = pQuery.GetSingleton<LocalTransform>().Position;
        Entity magnet = em.CreateEntity();
        em.AddComponentData(magnet, LocalTransform.FromPosition(playerPos + new float3(-1.2f, 0.4f, 0f)));
        em.AddComponentData(magnet, new PickupItemTag());
        em.AddComponentData(magnet, new PickupData
        {
            Type = PickupType.ExpMagnet,
            Value = 0f,
            Radius = 0.8f,
            FlySpeed = 0f
        });
    }

    #endregion

    #region --- Wave & Timeline Controller ---

    public DevWaveStatus GetWaveStatus()
    {
        var em = EntityManager;
        if (em == default) return default;

        using var query = em.CreateEntityQuery(ComponentType.ReadOnly<WaveManagerData>());
        if (query.IsEmpty) return default;

        var wave = query.GetSingleton<WaveManagerData>();
        return new DevWaveStatus
        {
            ElapsedTime = wave.ElapsedGameTime,
            State = wave.State,
            ActiveMegaWaveMinute = wave.ActiveMegaWaveMinute,
            MegaWaveTimer = wave.MegaWaveTimer,
            SurgeStage = wave.MegaWaveSurgeStage,
            EnemiesKilled = wave.MegaWaveEnemiesKilled,
            IsTimerPaused = wave.IsTimerPaused
        };
    }

    public void TriggerInstantMegaWave(int minute)
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>(), ComponentType.ReadWrite<SpawnerData>());
        if (query.IsEmpty) return;

        Entity spawner = query.GetSingletonEntity();
        var wave = em.GetComponentData<WaveManagerData>(spawner);
        var spawnData = em.GetComponentData<SpawnerData>(spawner);

        float waveTime = minute * 60f;
        wave.ElapsedGameTime = waveTime;
        spawnData.ElapsedGameTime = waveTime;
        wave.State = WaveState.MegaWaveActive;
        wave.ActiveMegaWaveMinute = minute;
        wave.MegaWaveDuration = 60f;
        wave.MegaWaveTimer = 60f;
        wave.MegaWaveSubSpawnTimer = 0.05f; // Bắt đầu sinh các đợt quái dồn dập ngay frame tiếp theo
        wave.MegaWaveSurgeStage = 0;
        wave.MegaWaveEnemiesKilled = 0;
        wave.MegaWaveRemainingEnemies = 0;
        wave.MegaWaveTotalEnemies = 0;
        wave.IsTimerPaused = false;

        if (minute == 3) wave.Triggered3Min = 1;
        else if (minute == 6) { wave.Triggered3Min = 1; wave.Triggered6Min = 1; }
        else if (minute == 9) { wave.Triggered3Min = 1; wave.Triggered6Min = 1; wave.Triggered9Min = 1; }

        em.SetComponentData(spawner, wave);
        em.SetComponentData(spawner, spawnData);

        if (RunLifecycleService.Instance != null)
        {
            var field = typeof(RunLifecycleService).GetField("_survivalTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(RunLifecycleService.Instance, waveTime);
        }
    }

    public void EndCurrentMegaWave()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>());
        if (query.IsEmpty) return;

        Entity spawner = query.GetSingletonEntity();
        var wave = em.GetComponentData<WaveManagerData>(spawner);

        if (wave.State == WaveState.MegaWaveActive)
        {
            wave.State = WaveState.NormalTimeline;
            wave.MegaWaveTimer = 0f;
            em.SetComponentData(spawner, wave);

            // Trao thưởng sống sót qua 1 phút: Rương Cổ Vật + Nam Châm EXP
            SpawnTreasureChest();
            SpawnExpMagnet();
        }
    }

    public void FastForwardToWave(int minute)
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>(), ComponentType.ReadWrite<SpawnerData>());
        if (query.IsEmpty) return;

        Entity spawner = query.GetSingletonEntity();
        var wave = em.GetComponentData<WaveManagerData>(spawner);
        var spawnData = em.GetComponentData<SpawnerData>(spawner);

        float targetTime = math.max(0f, (minute * 60f) - 3f); // Đặt thời gian đến 3 giây trước đợt Mega Wave
        wave.ElapsedGameTime = targetTime;
        spawnData.ElapsedGameTime = targetTime;
        wave.State = WaveState.NormalTimeline;
        wave.IsTimerPaused = false;
        wave.MegaWaveTimer = 0f;
        wave.MegaWaveSubSpawnTimer = 0f;
        wave.MegaWaveSurgeStage = 0;
        wave.MegaWaveEnemiesKilled = 0;
        wave.MegaWaveRemainingEnemies = 0;
        wave.MegaWaveTotalEnemies = 0;

        if (targetTime < 180f) wave.Triggered3Min = 0;
        if (targetTime < 360f) wave.Triggered6Min = 0;
        if (targetTime < 540f) wave.Triggered9Min = 0;

        em.SetComponentData(spawner, wave);
        em.SetComponentData(spawner, spawnData);

        if (RunLifecycleService.Instance != null)
        {
            var field = typeof(RunLifecycleService).GetField("_survivalTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(RunLifecycleService.Instance, targetTime);
        }
    }

    public void AdjustTimelineSeconds(float deltaSeconds)
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>(), ComponentType.ReadWrite<SpawnerData>());
        if (query.IsEmpty) return;

        Entity spawner = query.GetSingletonEntity();
        var wave = em.GetComponentData<WaveManagerData>(spawner);
        var spawnData = em.GetComponentData<SpawnerData>(spawner);

        float targetTime = math.max(0f, wave.ElapsedGameTime + deltaSeconds);
        wave.ElapsedGameTime = targetTime;
        spawnData.ElapsedGameTime = targetTime;

        if (targetTime < 180f) wave.Triggered3Min = 0;
        if (targetTime < 360f) wave.Triggered6Min = 0;
        if (targetTime < 540f) wave.Triggered9Min = 0;

        em.SetComponentData(spawner, wave);
        em.SetComponentData(spawner, spawnData);

        if (RunLifecycleService.Instance != null)
        {
            var field = typeof(RunLifecycleService).GetField("_survivalTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(RunLifecycleService.Instance, targetTime);
        }
    }

    public void ResetTimeline()
    {
        WasCheatedThisRun = true;
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>(), ComponentType.ReadWrite<SpawnerData>());
        if (query.IsEmpty) return;

        Entity spawner = query.GetSingletonEntity();
        var wave = em.GetComponentData<WaveManagerData>(spawner);
        var spawnData = em.GetComponentData<SpawnerData>(spawner);

        wave.ElapsedGameTime = 0f;
        spawnData.ElapsedGameTime = 0f;
        wave.State = WaveState.NormalTimeline;
        wave.IsTimerPaused = false;
        wave.MegaWaveTimer = 0f;
        wave.MegaWaveSubSpawnTimer = 0f;
        wave.MegaWaveSurgeStage = 0;
        wave.MegaWaveEnemiesKilled = 0;
        wave.Triggered3Min = 0;
        wave.Triggered6Min = 0;
        wave.Triggered9Min = 0;

        em.SetComponentData(spawner, wave);
        em.SetComponentData(spawner, spawnData);

        if (RunLifecycleService.Instance != null)
        {
            var field = typeof(RunLifecycleService).GetField("_survivalTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(RunLifecycleService.Instance, 0f);
        }
    }

    public void ToggleWaveTimerPause()
    {
        var em = EntityManager;
        if (em == default) return;

        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<WaveManagerData>());
        if (query.IsEmpty) return;

        Entity spawner = query.GetSingletonEntity();
        var wave = em.GetComponentData<WaveManagerData>(spawner);
        wave.IsTimerPaused = !wave.IsTimerPaused;
        em.SetComponentData(spawner, wave);
    }

    #endregion

    #region --- Action Registries for Dev UI Tabs ---

    /// <summary>
    /// Danh sách các hành động điều khiển đợt quái (Tab 1: Waves & Mega Wave).
    /// </summary>
    public List<DevAction> GetWaveActions()
    {
        return new List<DevAction>
        {
            new DevAction
            {
                ActionId = "TriggerMega3",
                LabelGetter = () => GameLocalization.DevTriggerMegaWave(3),
                IsToggle = false,
                NormalColor = new Color(0.38f, 0.16f, 0.05f, 1f),
                BorderColor = new Color(1f, 0.65f, 0.2f),
                ExecuteAction = () => TriggerInstantMegaWave(3)
            },
            new DevAction
            {
                ActionId = "TriggerMega6",
                LabelGetter = () => GameLocalization.DevTriggerMegaWave(6),
                IsToggle = false,
                NormalColor = new Color(0.35f, 0.08f, 0.3f, 1f),
                BorderColor = new Color(0.9f, 0.35f, 1f),
                ExecuteAction = () => TriggerInstantMegaWave(6)
            },
            new DevAction
            {
                ActionId = "TriggerMega9",
                LabelGetter = () => GameLocalization.DevTriggerMegaWave(9),
                IsToggle = false,
                NormalColor = new Color(0.4f, 0.05f, 0.08f, 1f),
                BorderColor = new Color(1f, 0.25f, 0.35f),
                ExecuteAction = () => TriggerInstantMegaWave(9)
            },
            new DevAction
            {
                ActionId = "EndMegaWave",
                LabelGetter = () => GameLocalization.DevEndMegaWave,
                IsToggle = false,
                NormalColor = new Color(0.28f, 0.22f, 0.05f, 1f),
                BorderColor = new Color(1f, 0.88f, 0.25f),
                ExecuteAction = EndCurrentMegaWave
            },
            new DevAction
            {
                ActionId = "JumpWave3",
                LabelGetter = () => GameLocalization.DevFastForwardWave(3),
                IsToggle = false,
                NormalColor = new Color(0.15f, 0.25f, 0.35f, 1f),
                BorderColor = new Color(0.35f, 0.75f, 1f),
                ExecuteAction = () => FastForwardToWave(3)
            },
            new DevAction
            {
                ActionId = "JumpWave6",
                LabelGetter = () => GameLocalization.DevFastForwardWave(6),
                IsToggle = false,
                NormalColor = new Color(0.18f, 0.2f, 0.38f, 1f),
                BorderColor = new Color(0.5f, 0.65f, 1f),
                ExecuteAction = () => FastForwardToWave(6)
            },
            new DevAction
            {
                ActionId = "JumpWave9",
                LabelGetter = () => GameLocalization.DevFastForwardWave(9),
                IsToggle = false,
                NormalColor = new Color(0.25f, 0.15f, 0.35f, 1f),
                BorderColor = new Color(0.75f, 0.5f, 1f),
                ExecuteAction = () => FastForwardToWave(9)
            },
            new DevAction
            {
                ActionId = "AddMinute",
                LabelGetter = () => GameLocalization.DevAddMinute,
                IsToggle = false,
                NormalColor = new Color(0.12f, 0.25f, 0.22f, 1f),
                BorderColor = new Color(0.35f, 0.85f, 0.7f),
                ExecuteAction = () => AdjustTimelineSeconds(60f)
            },
            new DevAction
            {
                ActionId = "SubMinute",
                LabelGetter = () => GameLocalization.DevSubMinute,
                IsToggle = false,
                NormalColor = new Color(0.25f, 0.15f, 0.18f, 1f),
                BorderColor = new Color(0.85f, 0.45f, 0.5f),
                ExecuteAction = () => AdjustTimelineSeconds(-60f)
            },
            new DevAction
            {
                ActionId = "ResetTimeline",
                LabelGetter = () => GameLocalization.DevResetTimeline,
                IsToggle = false,
                NormalColor = new Color(0.22f, 0.18f, 0.15f, 1f),
                BorderColor = new Color(0.85f, 0.7f, 0.45f),
                ExecuteAction = ResetTimeline
            },
            new DevAction
            {
                ActionId = "TogglePauseTimer",
                LabelGetter = () => {
                    var st = GetWaveStatus();
                    return GameLocalization.DevTogglePauseTimer(st.IsTimerPaused);
                },
                IsToggle = true,
                NormalColor = new Color(0.25f, 0.15f, 0.05f, 1f),
                BorderColor = new Color(1f, 0.75f, 0.2f),
                ExecuteAction = ToggleWaveTimerPause
            }
        };
    }

    /// <summary>
    /// Danh sách các công cụ gian lận người chơi và tiện ích (Tab 2: Cheats & Utilities).
    /// </summary>
    public List<DevAction> GetDevActions()
    {
        return new List<DevAction>
        {
            new DevAction
            {
                ActionId = "HealFull",
                LabelGetter = () => GameLocalization.DevHealFull,
                IsToggle = false,
                NormalColor = new Color(0.35f, 0.08f, 0.12f, 1f),
                BorderColor = new Color(1f, 0.4f, 0.5f),
                ExecuteAction = HealFull
            },
            new DevAction
            {
                ActionId = "GodMode",
                LabelGetter = () => GameLocalization.DevGodMode(IsGodMode),
                IsToggle = true,
                NormalColor = new Color(0.12f, 0.25f, 0.38f, 1f),
                BorderColor = new Color(0.3f, 0.85f, 1f),
                ExecuteAction = ToggleGodMode,
                StateGetter = () => IsGodMode ? "[ ON ]" : "[ OFF ]"
            },
            new DevAction
            {
                ActionId = "LevelUp",
                LabelGetter = () => GameLocalization.DevLevelUp,
                IsToggle = false,
                NormalColor = new Color(0.28f, 0.22f, 0.06f, 1f),
                BorderColor = new Color(1f, 0.9f, 0.35f),
                ExecuteAction = LevelUpPlayer
            },
            new DevAction
            {
                ActionId = "KillAll",
                LabelGetter = () => GameLocalization.DevKillAll,
                IsToggle = false,
                NormalColor = new Color(0.35f, 0.12f, 0.32f, 1f),
                BorderColor = new Color(1f, 0.45f, 0.95f),
                ExecuteAction = KillAllEnemies
            },
            new DevAction
            {
                ActionId = "SpeedBoost",
                LabelGetter = () => GameLocalization.DevSpeedBoost(IsSpeedBoosted),
                IsToggle = true,
                NormalColor = new Color(0.12f, 0.3f, 0.18f, 1f),
                BorderColor = new Color(0.4f, 1f, 0.55f),
                ExecuteAction = ToggleSpeedBoost,
                StateGetter = () => IsSpeedBoosted ? "[ ON ]" : "[ OFF ]"
            },
            new DevAction
            {
                ActionId = "AddRegen",
                LabelGetter = () => GameLocalization.DevAddRegen,
                IsToggle = false,
                NormalColor = new Color(0.12f, 0.32f, 0.18f, 1f),
                BorderColor = new Color(0.4f, 1f, 0.55f),
                ExecuteAction = AddDevRegen
            },
            new DevAction
            {
                ActionId = "CameraZoom",
                LabelGetter = () => {
                    float size = CameraFollowPlayer.Instance != null ? CameraFollowPlayer.Instance.OrthographicSize : 7.2f;
                    return GameLocalization.Get($"📷 Camera Zoom: {size:F1}", $"📷 Tầm nhìn Camera: {size:F1}");
                },
                IsToggle = false,
                NormalColor = new Color(0.15f, 0.22f, 0.35f, 1f),
                BorderColor = new Color(0.45f, 0.75f, 1f),
                ExecuteAction = () => {
                    if (CameraFollowPlayer.Instance == null) return;
                    float cur = CameraFollowPlayer.Instance.OrthographicSize;
                    float next = (cur < 6.8f) ? 7.2f : ((cur < 8.0f) ? 8.5f : 6.0f);
                    CameraFollowPlayer.Instance.OrthographicSize = next;
                }
            },
            new DevAction
            {
                ActionId = "AddSpiritStones",
                LabelGetter = () => GameLocalization.DevAddSpiritStones,
                IsToggle = false,
                NormalColor = new Color(0.15f, 0.22f, 0.35f, 1f),
                BorderColor = new Color(0.35f, 0.8f, 1f),
                ExecuteAction = () => AddSpiritStones(1000)
            },
            new DevAction
            {
                ActionId = "SpawnChest",
                LabelGetter = () => GameLocalization.DevSpawnChest,
                IsToggle = false,
                NormalColor = new Color(0.32f, 0.25f, 0.05f, 1f),
                BorderColor = new Color(1f, 0.85f, 0.25f),
                ExecuteAction = SpawnTreasureChest
            },
            new DevAction
            {
                ActionId = "SpawnMagnet",
                LabelGetter = () => GameLocalization.DevSpawnMagnet,
                IsToggle = false,
                NormalColor = new Color(0.28f, 0.12f, 0.32f, 1f),
                BorderColor = new Color(0.85f, 0.45f, 1f),
                ExecuteAction = SpawnExpMagnet
            }
        };
    }

    #endregion
}

#endif
