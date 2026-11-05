using Unity.Entities;
using UnityEngine;

/// <summary>
/// Chuyên trách đọc dữ liệu từ các unmanaged ComponentData trong ECS của người chơi
/// và chuyển đổi sang PlayerHudSnapshot (DTO) cho tầng UI hiển thị.
/// </summary>
public static class PlayerHudSnapshotReader
{
    public static PlayerHudSnapshot Read(EntityManager em, Entity playerEntity, float survivalTime)
    {
        if (playerEntity == Entity.Null || !em.Exists(playerEntity))
        {
            return default;
        }

        // 1. Đọc Máu (HP) & Tự hồi máu (HP Regen)
        float currentHp = 100f;
        float maxHp = 100f;
        if (em.HasComponent<HealthData>(playerEntity))
        {
            var hp = em.GetComponentData<HealthData>(playerEntity);
            currentHp = hp.CurrentHP;
            maxHp = hp.MaxHP;
        }

        float hpRegen = 0f;
        if (em.HasComponent<PlayerHPRegenData>(playerEntity))
        {
            hpRegen = em.GetComponentData<PlayerHPRegenData>(playerEntity).RegenPerSecond;
        }

        // 2. Đọc Khiên (Shield)
        bool hasShield = false;
        int shieldLevel = 0;
        int shieldCharges = 0;
        int shieldHits = 0;
        float rechargeSec = 0f;
        if (em.HasComponent<PlayerShieldData>(playerEntity))
        {
            var shield = em.GetComponentData<PlayerShieldData>(playerEntity);
            hasShield = true;
            shieldLevel = shield.SkillLevel;
            shieldCharges = shield.CurrentCharges;
            shieldHits = shield.HitsRemainingInCurrentShield;
            rechargeSec = Mathf.Max(0f, shield.RechargeCooldown - shield.RechargeTimer);
        }

        // 3. Đọc Cấp độ & EXP
        int level = 1;
        int currentExp = 0;
        int expToNext = 10;
        if (em.HasComponent<PlayerLevelData>(playerEntity))
        {
            var lvl = em.GetComponentData<PlayerLevelData>(playerEntity);
            level = lvl.Level;
            currentExp = lvl.CurrentExp;
            expToNext = lvl.ExpToNextLevel;
        }

        // 4. Đọc trạng thái đợt tổng tấn công Mega Wave
        bool isMegaWaveActive = false;
        int megaWaveRemaining = 0;
        int megaWaveTotal = 0;
        int megaWaveMinute = 0;
        float megaWaveTimeRemaining = 0f;
        int megaWaveKills = 0;

        var waveQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WaveManagerData>());
        if (!waveQuery.IsEmpty)
        {
            var wave = waveQuery.GetSingleton<WaveManagerData>();
            isMegaWaveActive = wave.State == WaveState.MegaWaveActive;
            megaWaveRemaining = wave.MegaWaveRemainingEnemies;
            megaWaveTotal = wave.MegaWaveTotalEnemies;
            megaWaveMinute = wave.ActiveMegaWaveMinute;
            megaWaveTimeRemaining = wave.MegaWaveTimer;
            megaWaveKills = wave.MegaWaveEnemiesKilled;
        }

        return new PlayerHudSnapshot(
            currentHp,
            maxHp,
            hpRegen,
            hasShield,
            shieldLevel,
            shieldCharges,
            shieldHits,
            rechargeSec,
            currentExp,
            expToNext,
            level,
            survivalTime,
            isMegaWaveActive,
            megaWaveRemaining,
            megaWaveTotal,
            megaWaveMinute,
            megaWaveTimeRemaining,
            megaWaveKills
        );
    }
}

