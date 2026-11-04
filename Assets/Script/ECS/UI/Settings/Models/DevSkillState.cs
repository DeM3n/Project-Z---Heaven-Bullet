/// <summary>
/// DTO chứa trạng thái cấp độ hiện tại của một kỹ năng để View render,
/// hoàn toàn tách rời khỏi các unmanaged component của ECS.
/// </summary>
public readonly struct DevSkillState
{
    public readonly string SkillId;
    public readonly int Level;
    public readonly bool IsActive;
    public readonly bool IsFusion;

    public DevSkillState(string skillId, int level, bool isActive = true, bool isFusion = false)
    {
        SkillId = skillId;
        Level = level;
        IsActive = isActive;
        IsFusion = isFusion;
    }

    public string FormattedLevelText => IsActive ? $"[ LV.{Level} ]" : "[ OFF ]";
    public string FormattedFusionText => (Level >= 1 && IsActive) ? "[ ACTIVE ✦ ]" : "[ DISABLED ]";
}

