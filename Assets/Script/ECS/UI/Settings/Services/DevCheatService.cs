using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR || DEBUG

/// <summary>
/// Quản lý toàn bộ nghiệp vụ gian lận (Cheats) phục vụ kiểm thử game:
/// - God Mode: Sử dụng cơ chế miễn thương (CurrentTimer = float.MaxValue) của PlayerDamageResolutionSystem.
/// - Speed Boost: Dùng hệ số nhân/chia (multiplier), không ghi đè giá trị gốc.
/// - Kill All: Đưa máu về 0 để đi qua Death pipeline và rớt ngọc EXP bình thường.
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
                ActionId = "AddRegen",
                LabelGetter = () => GameLocalization.DevAddRegen,
                IsToggle = false,
                NormalColor = new Color(0.12f, 0.32f, 0.18f, 1f),
                BorderColor = new Color(0.4f, 1f, 0.55f),
                ExecuteAction = AddDevRegen
            },
            new DevAction
            {
                ActionId = "JumpWave3",
                LabelGetter = () => GameLocalization.DevJumpWave3,
                IsToggle = false,
                NormalColor = new Color(0.38f, 0.18f, 0.08f, 1f),
                BorderColor = new Color(1f, 0.6f, 0.2f),
                ExecuteAction = () => FastForwardToWave(3)
            },
            new DevAction
            {
                ActionId = "JumpWave6",
                LabelGetter = () => GameLocalization.DevJumpWave6,
                IsToggle = false,
                NormalColor = new Color(0.32f, 0.08f, 0.35f, 1f),
                BorderColor = new Color(0.85f, 0.3f, 1f),
                ExecuteAction = () => FastForwardToWave(6)
            }
        };
    }
}

#endif
