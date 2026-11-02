using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
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
            }
        };
    }
}

#endif
