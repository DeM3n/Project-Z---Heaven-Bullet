using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý dữ liệu tiến trình vĩnh viễn (Meta-Progression):
/// - Quản lý số Linh Thạch / Vàng (Spirit Stones) tích lũy của người chơi.
/// - Quản lý Cây Kỹ Năng Thiên Phú Chỉ Số Chung (Universal Skill Tree) 3 nhánh: Thể Lực, Công Kích, Vận Mệnh.
/// - Tự động lưu/đọc vào PlayerPrefs (hỗ trợ export JSON).
/// - Hỗ trợ tính năng Tẩy Điểm (Respec) hoàn lại 100% Linh Thạch bất kỳ lúc nào.
/// </summary>
public class MetaProgressionService
{
    private const string PREF_KEY_SPIRIT_STONES = "Meta_SpiritStones";
    private const string PREF_KEY_TALENTS = "Meta_Talent_";

    public enum TalentBranch
    {
        Health,     // Nhánh Thể Lực
        Offense,    // Nhánh Công Kích
        Fortune     // Nhánh Vận Mệnh
    }

    [Serializable]
    public class TalentNodeInfo
    {
        public string Id;
        public TalentBranch Branch;
        public int Tier; // 1, 2, 3 hoặc 4 (Apex)
        public string NameEn;
        public string NameVi;
        public string DescriptionEn;
        public string DescriptionVi;
        public int MaxLevel;
        public int CurrentLevel;
        public int[] Costs; // Chi phí cho từng cấp độ
        public string PrerequisiteNodeId; // Node cần có ít nhất 1 điểm trước khi mở khóa

        public bool IsMaxed => CurrentLevel >= MaxLevel;
        public int NextCost => IsMaxed ? 0 : Costs[Mathf.Clamp(CurrentLevel, 0, Costs.Length - 1)];
    }

    private static MetaProgressionService _instance;
    public static MetaProgressionService Instance => _instance ??= new MetaProgressionService();

    public int SpiritStones { get; private set; }
    private readonly Dictionary<string, TalentNodeInfo> _nodes = new Dictionary<string, TalentNodeInfo>();

    public event Action OnDataChanged;

    public MetaProgressionService()
    {
        InitializeNodes();
        LoadData();
    }

    private void InitializeNodes()
    {
        _nodes.Clear();

        // ======================= NHÁNH 1: THỂ LỰC (HEALTH & SURVIVAL) =======================
        AddNode(new TalentNodeInfo
        {
            Id = "hp_max",
            Branch = TalentBranch.Health,
            Tier = 1,
            NameEn = "Vitality Surge",
            NameVi = "Cường Hóa Sinh Lực",
            DescriptionEn = "+15 Max HP per level (Up to +75 HP)",
            DescriptionVi = "+15 Máu tối đa mỗi cấp (Tối đa +75 HP)",
            MaxLevel = 5,
            Costs = new[] { 100, 200, 350, 550, 800 },
            PrerequisiteNodeId = null
        });

        AddNode(new TalentNodeInfo
        {
            Id = "hp_regen",
            Branch = TalentBranch.Health,
            Tier = 2,
            NameEn = "Regeneration",
            NameVi = "Tự Chữa Lành",
            DescriptionEn = "+0.5 HP/s Health Regen (Up to +1.5 HP/s)",
            DescriptionVi = "+0.5 HP/giây tự hồi máu liên tục (Tối đa +1.5 HP/s)",
            MaxLevel = 3,
            Costs = new[] { 300, 600, 1000 },
            PrerequisiteNodeId = "hp_max"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "hp_armor",
            Branch = TalentBranch.Health,
            Tier = 2,
            NameEn = "Iron Skin",
            NameVi = "Hộ Thân Cương Khí",
            DescriptionEn = "-3.5% Damage Taken from all hits (Up to -10.5%)",
            DescriptionVi = "Giảm -3.5% sát thương gánh chịu (Tối đa -10.5%)",
            MaxLevel = 3,
            Costs = new[] { 350, 700, 1200 },
            PrerequisiteNodeId = "hp_max"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "hp_revival",
            Branch = TalentBranch.Health,
            Tier = 3,
            NameEn = "Phoenix Rebirth",
            NameVi = "Đạo Quả: Niết Bàn Trùng Sinh",
            DescriptionEn = "Revive instantly with 50% HP upon death (Once per run)",
            DescriptionVi = "Hồi sinh tức thì với 50% HP khi cạn máu (1 lần mỗi trận)",
            MaxLevel = 1,
            Costs = new[] { 3000 },
            PrerequisiteNodeId = "hp_regen"
        });

        // ======================= NHÁNH 2: CÔNG KÍCH (OFFENSE & COMBAT) =======================
        AddNode(new TalentNodeInfo
        {
            Id = "atk_global",
            Branch = TalentBranch.Offense,
            Tier = 1,
            NameEn = "Universal Might",
            NameVi = "Khí Kình Toàn Diện",
            DescriptionEn = "+6% Global ATK Damage for all weapons (Up to +30%)",
            DescriptionVi = "+6% Tổng sát thương cho toàn bộ vũ khí (Tối đa +30%)",
            MaxLevel = 5,
            Costs = new[] { 100, 200, 350, 550, 800 },
            PrerequisiteNodeId = null
        });

        AddNode(new TalentNodeInfo
        {
            Id = "atk_cdr",
            Branch = TalentBranch.Offense,
            Tier = 2,
            NameEn = "Quick Cast",
            NameVi = "Tốc Kích Quyết",
            DescriptionEn = "-5% Global Weapon Cooldown (Up to -15%)",
            DescriptionVi = "Giảm -5% hồi chiêu chung cho mọi kỹ năng (Tối đa -15%)",
            MaxLevel = 3,
            Costs = new[] { 300, 600, 1000 },
            PrerequisiteNodeId = "atk_global"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "atk_crit",
            Branch = TalentBranch.Offense,
            Tier = 2,
            NameEn = "Fatal Strike",
            NameVi = "Điểm Huyệt Thần Đạo",
            DescriptionEn = "+4% Critical Hit Chance x2 DMG (Up to +12%)",
            DescriptionVi = "+4% Tỉ lệ Bạo kích gây x2 sát thương (Tối đa +12%)",
            MaxLevel = 3,
            Costs = new[] { 350, 700, 1200 },
            PrerequisiteNodeId = "atk_global"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "atk_apex",
            Branch = TalentBranch.Offense,
            Tier = 3,
            NameEn = "Colossal Dominance",
            NameVi = "Đạo Quả: Bạo Kích Cuồng Nộ",
            DescriptionEn = "+15% Additional ATK and Crit DMG increased to x2.5",
            DescriptionVi = "+15% ATK bổ sung và tăng sát thương Bạo kích lên x2.5",
            MaxLevel = 1,
            Costs = new[] { 3000 },
            PrerequisiteNodeId = "atk_crit"
        });

        // ======================= NHÁNH 3: VẬN MỆNH (MOBILITY & ECONOMY) =======================
        AddNode(new TalentNodeInfo
        {
            Id = "util_speed",
            Branch = TalentBranch.Fortune,
            Tier = 1,
            NameEn = "Windwalker",
            NameVi = "Thần Hành Bộ",
            DescriptionEn = "+4% Movement Speed (Up to +20%)",
            DescriptionVi = "+4% Tốc độ di chuyển nhân vật (Tối đa +20%)",
            MaxLevel = 5,
            Costs = new[] { 100, 200, 350, 550, 800 },
            PrerequisiteNodeId = null
        });

        AddNode(new TalentNodeInfo
        {
            Id = "util_exp",
            Branch = TalentBranch.Fortune,
            Tier = 1,
            NameEn = "Enlightened Mind",
            NameVi = "Ngộ Đạo Tâm Kinh",
            DescriptionEn = "+5% EXP Gain from all Orbs (Up to +25%)",
            DescriptionVi = "+5% EXP nhận được từ mọi viên ngọc (Tối đa +25%)",
            MaxLevel = 5,
            Costs = new[] { 120, 250, 450, 700, 1000 },
            PrerequisiteNodeId = null
        });

        AddNode(new TalentNodeInfo
        {
            Id = "util_pickup",
            Branch = TalentBranch.Fortune,
            Tier = 2,
            NameEn = "Spirit Magnet",
            NameVi = "Hút Linh Thuật",
            DescriptionEn = "+20% EXP & Item Magnet Range (Up to +60%)",
            DescriptionVi = "+20% Bán kính nam châm hút ngọc & đồ (Tối đa +60%)",
            MaxLevel = 3,
            Costs = new[] { 250, 500, 900 },
            PrerequisiteNodeId = "util_speed"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "util_drop",
            Branch = TalentBranch.Fortune,
            Tier = 2,
            NameEn = "Fortune Eye",
            NameVi = "Thiên Cơ Vận Khí",
            DescriptionEn = "+12% Drop Rate for Chests, Big Orbs & Items (Up to +36%)",
            DescriptionVi = "+12% Tỉ lệ quái rơi Rương, Ngọc Lớn & Vật phẩm (Tối đa +36%)",
            MaxLevel = 3,
            Costs = new[] { 300, 650, 1100 },
            PrerequisiteNodeId = "util_speed"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "util_gold",
            Branch = TalentBranch.Fortune,
            Tier = 2,
            NameEn = "Goldseeker",
            NameVi = "Tụ Bảo Đạo Thuật",
            DescriptionEn = "+15% Spirit Stones & Gold Gain (Up to +45%)",
            DescriptionVi = "+15% Linh Thạch & Vàng thu được sau trận (Tối đa +45%)",
            MaxLevel = 3,
            Costs = new[] { 300, 600, 1000 },
            PrerequisiteNodeId = "util_speed"
        });

        AddNode(new TalentNodeInfo
        {
            Id = "util_apex",
            Branch = TalentBranch.Fortune,
            Tier = 3,
            NameEn = "Master of Fortune",
            NameVi = "Đạo Quả: Tài Phú Vô Song",
            DescriptionEn = "+2 Free Perk Rerolls in every run & +25% Extra Gold",
            DescriptionVi = "+2 Lượt Reroll bài miễn phí mỗi ván và +25% Vàng thưởng",
            MaxLevel = 1,
            Costs = new[] { 3000 },
            PrerequisiteNodeId = "util_gold"
        });
    }

    private void AddNode(TalentNodeInfo node)
    {
        _nodes[node.Id] = node;
    }

    public IEnumerable<TalentNodeInfo> GetAllNodes() => _nodes.Values;

    public TalentNodeInfo GetNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public bool CanUpgrade(string nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null || node.IsMaxed) return false;
        if (SpiritStones < node.NextCost) return false;

        // Kiểm tra điều kiện mở khóa node trước
        if (!string.IsNullOrEmpty(node.PrerequisiteNodeId))
        {
            var prereq = GetNode(node.PrerequisiteNodeId);
            if (prereq == null || prereq.CurrentLevel <= 0) return false;
        }

        return true;
    }

    public bool IsNodeLocked(string nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null) return false;
        if (string.IsNullOrEmpty(node.PrerequisiteNodeId)) return false;
        var prereq = GetNode(node.PrerequisiteNodeId);
        return prereq == null || prereq.CurrentLevel <= 0;
    }

    public string GetPrerequisiteNodeName(string nodeId, bool isVietnamese)
    {
        var node = GetNode(nodeId);
        if (node == null || string.IsNullOrEmpty(node.PrerequisiteNodeId)) return string.Empty;
        var prereq = GetNode(node.PrerequisiteNodeId);
        if (prereq == null) return string.Empty;
        return isVietnamese ? prereq.NameVi : prereq.NameEn;
    }

    public bool UpgradeNode(string nodeId)
    {
        if (!CanUpgrade(nodeId)) return false;

        var node = GetNode(nodeId);
        int cost = node.NextCost;
        SpiritStones -= cost;
        node.CurrentLevel++;

        SaveData();
        OnDataChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Tẩy điểm hoàn lại 100% Linh Thạch đã tiêu
    /// </summary>
    public void ResetAllTalents()
    {
        int totalRefund = 0;
        foreach (var node in _nodes.Values)
        {
            for (int i = 0; i < node.CurrentLevel; i++)
            {
                if (i < node.Costs.Length)
                {
                    totalRefund += node.Costs[i];
                }
            }
            node.CurrentLevel = 0;
        }

        SpiritStones += totalRefund;
        SaveData();
        OnDataChanged?.Invoke();
    }

    public void AddSpiritStones(int amount)
    {
        if (amount <= 0) return;
        SpiritStones += amount;
        SaveData();
        OnDataChanged?.Invoke();
    }

    public void SetSpiritStones(int amount)
    {
        SpiritStones = Mathf.Max(0, amount);
        SaveData();
        OnDataChanged?.Invoke();
    }

    // ======================= TÍNH TOÁN CHỈ SỐ BUFF CHO GAMEPLAY =======================
    public float GetBonusMaxHP() => GetNodeLevel("hp_max") * 15f;
    public float GetBonusHPRegen() => GetNodeLevel("hp_regen") * 0.5f;
    public float GetDamageReductionPercent() => GetNodeLevel("hp_armor") * 0.035f;
    public bool HasRevival() => GetNodeLevel("hp_revival") > 0;

    public float GetGlobalATKMultiplier() => 1f + (GetNodeLevel("atk_global") * 0.06f) + (GetNodeLevel("atk_apex") > 0 ? 0.15f : 0f);
    public float GetCooldownReductionPercent() => GetNodeLevel("atk_cdr") * 0.05f;
    public float GetCritChancePercent() => GetNodeLevel("atk_crit") * 0.04f;
    public float GetCritDamageMultiplier() => GetNodeLevel("atk_apex") > 0 ? 2.5f : 2.0f;

    public float GetMoveSpeedMultiplier() => 1f + (GetNodeLevel("util_speed") * 0.04f);
    public float GetBonusExpMultiplier() => 1f + (GetNodeLevel("util_exp") * 0.05f);
    public float GetBonusPickupRange() => GetNodeLevel("util_pickup") * 0.20f;
    public float GetBonusDropRatePercent() => GetNodeLevel("util_drop") * 0.12f;
    public float GetGoldMultiplier() => 1f + (GetNodeLevel("util_gold") * 0.15f) + (GetNodeLevel("util_apex") > 0 ? 0.25f : 0f);
    public int GetBonusRerolls() => GetNodeLevel("util_apex") > 0 ? 2 : 0;

    public int GetNodeLevel(string nodeId)
    {
        var node = GetNode(nodeId);
        return node?.CurrentLevel ?? 0;
    }

    public int GetTotalSpentStones()
    {
        int total = 0;
        foreach (var node in _nodes.Values)
        {
            for (int i = 0; i < node.CurrentLevel; i++)
            {
                if (i < node.Costs.Length) total += node.Costs[i];
            }
        }
        return total;
    }

    // ======================= SAVE & LOAD =======================
    public void SaveData()
    {
        PlayerPrefs.SetInt(PREF_KEY_SPIRIT_STONES, SpiritStones);
        foreach (var node in _nodes.Values)
        {
            PlayerPrefs.SetInt(PREF_KEY_TALENTS + node.Id, node.CurrentLevel);
        }
        PlayerPrefs.Save();
    }

    public void LoadData()
    {
        // Khởi đầu cấp 500 Linh Thạch mặc định để người chơi thử trải nghiệm nâng cấp ngay
        SpiritStones = PlayerPrefs.GetInt(PREF_KEY_SPIRIT_STONES, 500);

        foreach (var node in _nodes.Values)
        {
            node.CurrentLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY_TALENTS + node.Id, 0), 0, node.MaxLevel);
        }
    }
}

