using UnityEngine;

/// <summary>
/// DTO chứa thông tin lựa chọn Perk hiển thị trên thẻ bài popup Level-Up.
/// </summary>
public struct PerkOptionData
{
    public string PerkId;
    public int Level;
    public string Title;
    public string LevelBadge;
    public string Description;
    public Color HighlightColor;
    public bool IsFusion;
}

/// <summary>
/// DTO chứa thông tin Cổ Vật nhận được từ Rương Báu hiển thị trên popup Ancient Chest.
/// </summary>
public struct RelicRewardData
{
    public RelicType Type;
    public string IconEmoji;
    public string Title;
    public string LevelBadge;
    public string Description;
}

/// <summary>
/// DTO chứa thông số tổng kết ván chơi hiển thị trên màn hình Game Over.
/// </summary>
public struct GameOverStats
{
    public float SurvivalTime;
    public int FinalLevel;
    public int StonesEarned;

    public string FormattedTime
    {
        get
        {
            int minutes = (int)(SurvivalTime / 60f);
            int seconds = (int)(SurvivalTime % 60f);
            return $"{minutes:00}:{seconds:00}";
        }
    }
}

