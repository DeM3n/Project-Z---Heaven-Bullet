using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thống kê kỹ năng phục vụ hiển thị trên tab Magics / DMG Stats của Pause Menu.
/// </summary>
public class SkillDamageStatItem
{
    public string SkillId;
    public string Title;
    public int Level;
    public int MaxLevel;
    public float Damage;
    public int HitCount;
    public float Percentage;
    public float Dps;
    public Color ThemeColor;
}

/// <summary>
/// Thống kê Cổ Vật Bị Động đang sở hữu.
/// </summary>
public class RelicStatItem
{
    public string Name;
    public int Level;
    public string Description;
}

/// <summary>
/// Snapshot dữ liệu bất biến chứa CHÍNH XÁC các chỉ số thực tế đang hoạt động trong game:
/// Loại bỏ hoàn toàn các chỉ số ảo (duration, size, cooldown, mana gain, amp attack, evasion).
/// </summary>
public struct PauseMenuSnapshot
{
    public int Level;
    public float SurvivalTime;
    public int EnemiesKilled;

    // --- CỘT 1: SINH TỒN & PHÒNG THỦ (6 CHỈ SỐ THỰC TẾ) ---
    public float CurrentHP;
    public float MaxHP;

    public bool HasShield;
    public int ShieldCharges;
    public int ShieldLevel;
    public int ShieldHitsRemaining;
    public float ShieldRechargeSec;

    public float HpRegenPerSec;
    public float DamageReductionPercent;

    public float MoveSpeed;
    public float MoveSpeedPercent;

    public bool HasRevival;
    public bool CanRevive;

    // --- CỘT 2: TẤN CÔNG & TIỆN ÍCH (6 CHỈ SỐ THỰC TẾ) ---
    public float GlobalDamageMultiplier;

    public float PickupRange;
    public float PickupBonusPercent;

    public float LifeHeartHealAmount;

    public int BonusRerolls;

    public float DropRateBonusPercent;

    public float GoldMultiplier;

    // --- TỔNG KẾT & DANH SÁCH ---
    public float TotalDamage;
    public float TotalDps;

    public List<SkillDamageStatItem> SkillStats;
    public List<RelicStatItem> Relics;

    public string FormattedSurvivalTime
    {
        get
        {
            int min = (int)(SurvivalTime / 60f);
            int sec = (int)(SurvivalTime % 60f);
            return $"{min:00} : {sec:00}";
        }
    }
}
