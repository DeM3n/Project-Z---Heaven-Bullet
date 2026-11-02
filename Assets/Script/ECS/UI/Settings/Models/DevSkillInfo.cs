using UnityEngine;

/// <summary>
/// POCO thuần mô tả thông tin hiển thị của một kỹ năng trên Dev Control Panel,
/// tuyệt đối không chứa EntityQuery hay tham chiếu đến ECS.
/// </summary>
public class DevSkillInfo
{
    public string SkillId { get; }
    public string DisplayName { get; }
    public int MaxLevel { get; }
    public Color ThemeColor { get; }
    public bool IsFusion { get; }

    public DevSkillInfo(string skillId, string displayName, int maxLevel, Color themeColor, bool isFusion = false)
    {
        SkillId = skillId;
        DisplayName = displayName;
        MaxLevel = maxLevel;
        ThemeColor = themeColor;
        IsFusion = isFusion;
    }
}
