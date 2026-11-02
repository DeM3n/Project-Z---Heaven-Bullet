using UnityEngine;

/// <summary>
/// DTO Snapshot bất biến (immutable) chứa trạng thái hiển thị của người chơi trên HUD,
/// giúp View hoàn toàn tách biệt khỏi các unmanaged component của ECS.
/// </summary>
public readonly struct PlayerHudSnapshot
{
    public readonly float CurrentHP;
    public readonly float MaxHP;
    public readonly float HpPercent;

    public readonly bool HasShield;
    public readonly int ShieldLevel;
    public readonly int ShieldCharges;
    public readonly int ShieldHitsRemaining;
    public readonly float ShieldRechargeRemainingSec;

    public readonly int CurrentExp;
    public readonly int ExpToNextLevel;
    public readonly float ExpPercent;

    public readonly int Level;
    public readonly float SurvivalTimeSeconds;

    public PlayerHudSnapshot(
        float currentHp,
        float maxHp,
        bool hasShield,
        int shieldLevel,
        int charges,
        int hitsRemaining,
        float rechargeSec,
        int currentExp,
        int expToNext,
        int level,
        float survivalTime)
    {
        CurrentHP = currentHp;
        MaxHP = Mathf.Max(1f, maxHp);
        HpPercent = Mathf.Clamp01(CurrentHP / MaxHP);

        HasShield = hasShield;
        ShieldLevel = shieldLevel;
        ShieldCharges = charges;
        ShieldHitsRemaining = hitsRemaining;
        ShieldRechargeRemainingSec = rechargeSec;

        CurrentExp = currentExp;
        ExpToNextLevel = Mathf.Max(1, expToNext);
        ExpPercent = Mathf.Clamp01((float)CurrentExp / ExpToNextLevel);

        Level = level;
        SurvivalTimeSeconds = survivalTime;
    }

    public string FormattedHpText
    {
        get
        {
            string hpString = $"{Mathf.Max(0, (int)CurrentHP)} / {(int)MaxHP}";
            if (HasShield && ShieldLevel > 0)
            {
                if (ShieldCharges > 0)
                {
                    string hitBadge = (ShieldLevel >= 4) ? $"({ShieldHitsRemaining}h)" : "";
                    hpString += $"  <color=#00e5ff>[🛡️ x{ShieldCharges}{hitBadge}]</color>";
                }
                else
                {
                    int remainSec = Mathf.Max(1, Mathf.CeilToInt(ShieldRechargeRemainingSec));
                    hpString += $"  <color=#80deea>[🛡️ {remainSec}s]</color>";
                }
            }
            return hpString;
        }
    }

    public string FormattedExpText => $"{CurrentExp} / {ExpToNextLevel} ({Mathf.RoundToInt(ExpPercent * 100f)}%)";
    public string FormattedLevelBadge => $"LV.{Level}";
    public string FormattedSurvivalTimer
    {
        get
        {
            int minutes = (int)(SurvivalTimeSeconds / 60f);
            int seconds = (int)(SurvivalTimeSeconds % 60f);
            return $"{minutes:00}:{seconds:00}";
        }
    }
}

