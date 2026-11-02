using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Định nghĩa cấu hình chung của một kỹ năng (Single Source of Truth),
/// được dùng chung giữa hệ thống HUD Level-Up và Dev Control Panel.
/// </summary>
public class SharedSkillDefinition
{
    public string SkillId { get; set; }
    public Func<string> TitleGetter { get; set; }
    public int MaxLevel { get; set; }
    public Color ThemeColor { get; set; }
    public bool IsFusion { get; set; }

    public string DisplayTitle => TitleGetter != null ? TitleGetter() : SkillId;
}

/// <summary>
/// Danh mục kỹ năng trung tâm (Single Source of Truth):
/// Thống nhất ID, tên hiển thị song ngữ, cấp tối đa và màu sắc chủ đạo.
/// </summary>
public static class SkillDefinitionRegistry
{
    public static readonly List<SharedSkillDefinition> AllSkills = new List<SharedSkillDefinition>
    {
        new SharedSkillDefinition
        {
            SkillId = "FlyingSword",
            TitleGetter = () => GameLocalization.FlyingSwordTitle,
            MaxLevel = 4,
            ThemeColor = new Color(0.2f, 0.9f, 1f),
            IsFusion = false
        },
        new SharedSkillDefinition
        {
            SkillId = "SlashSword",
            TitleGetter = () => GameLocalization.SlashSwordTitle,
            MaxLevel = 4,
            ThemeColor = new Color(0.85f, 0.45f, 1f),
            IsFusion = false
        },
        new SharedSkillDefinition
        {
            SkillId = "ProjectileSkill",
            TitleGetter = () => GameLocalization.ProjectileSkillTitle,
            MaxLevel = 5,
            ThemeColor = new Color(1f, 0.75f, 0.2f),
            IsFusion = false
        },
        new SharedSkillDefinition
        {
            SkillId = "Fireball",
            TitleGetter = () => GameLocalization.FireballTitle,
            MaxLevel = 4,
            ThemeColor = new Color(1f, 0.42f, 0.12f),
            IsFusion = false
        },
        new SharedSkillDefinition
        {
            SkillId = "Shield",
            TitleGetter = () => GameLocalization.ShieldTitle,
            MaxLevel = 4,
            ThemeColor = new Color(0f, 0.9f, 1f),
            IsFusion = false
        },
        new SharedSkillDefinition
        {
            SkillId = "Boomerang",
            TitleGetter = () => GameLocalization.BoomerangTitle,
            MaxLevel = 4,
            ThemeColor = new Color(0.15f, 0.95f, 0.65f),
            IsFusion = false
        },
        new SharedSkillDefinition
        {
            SkillId = "SwordFusion",
            TitleGetter = () => GameLocalization.SwordFusionTitle,
            MaxLevel = 1,
            ThemeColor = new Color(1f, 0.85f, 0.15f),
            IsFusion = true
        }
    };

    public static SharedSkillDefinition GetById(string skillId)
    {
        return AllSkills.Find(s => s.SkillId == skillId);
    }
}
