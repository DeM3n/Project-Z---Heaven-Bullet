using System;
using System.Collections.Generic;

/// <summary>
/// Kho dữ liệu Bách Khoa Toàn Thư (Codex Database):
/// Lưu trữ toàn bộ thông tin chi tiết về Quái vật, Kỹ năng/Đạn bắn và Cổ vật bị động
/// phục vụ giao diện Sách tra cứu trong Main Menu.
/// </summary>
public static class CodexDatabase
{
    public enum EntryCategory
    {
        Enemies,
        Skills,
        Relics
    }

    public class CodexEntry
    {
        public string Id;
        public EntryCategory Category;
        public string TitleEn;
        public string TitleVi;
        public string SubtitleEn;
        public string SubtitleVi;
        public string DescriptionEn;
        public string DescriptionVi;
        public Dictionary<string, string> StatsEn = new Dictionary<string, string>();
        public Dictionary<string, string> StatsVi = new Dictionary<string, string>();
        public string MechanicsEn;
        public string MechanicsVi;
        public string TipsEn;
        public string TipsVi;
        public string SpritePath; // Resource hoặc sprite name
    }

    private static List<CodexEntry> _allEntries;

    public static List<CodexEntry> GetAllEntries()
    {
        if (_allEntries == null)
        {
            InitializeDatabase();
        }
        return _allEntries;
    }

    public static List<CodexEntry> GetEntriesByCategory(EntryCategory category)
    {
        var list = new List<CodexEntry>();
        foreach (var entry in GetAllEntries())
        {
            if (entry.Category == category) list.Add(entry);
        }
        return list;
    }

    private static void InitializeDatabase()
    {
        _allEntries = new List<CodexEntry>();

        // =========================================================================
        // 1. QUÁI VẬT (ENEMIES)
        // =========================================================================
        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_chaser",
            Category = EntryCategory.Enemies,
            TitleEn = "Chaser Demon",
            TitleVi = "U Hồn Kiếm Nô",
            SubtitleEn = "Standard Melee Swarm",
            SubtitleVi = "Quái Cận Chiến Cơ Bản",
            DescriptionEn = "Corrupted sword thralls that relentlessly march toward any living essence.",
            DescriptionVi = "Những kiếm nô bị tà khí tha hóa, liên tục áp sát người chơi theo bầy đàn đông đảo.",
            StatsEn = new Dictionary<string, string>
            {
                { "Base HP", "32" },
                { "Movement Speed", "2.2 m/s" },
                { "Contact Damage", "10 DMG / tick" },
                { "EXP Dropped", "Small / Medium Orb" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Máu Cơ Bản", "32 HP" },
                { "Tốc Độ Chạy", "2.2 m/s" },
                { "Sát Thương Chạm", "10 DMG / nhịp" },
                { "Rớt Kinh Nghiệm", "Ngọc EXP Nhỏ / Vừa" }
            },
            MechanicsEn = "Spawns in heavy clusters. Gains +30% HP per minute of survival time.",
            MechanicsVi = "Xuất hiện theo từng cụm lớn. Máu tăng thêm +30% mỗi phút trận đấu.",
            TipsEn = "Easy to wipe with Slash Sword sweeps or Fireball cluster explosions.",
            TipsVi = "Rất dễ quét sạch bằng vệt chém diện rộng của Trảm Kiếm hoặc vụ nổ Cầu Lửa."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_shooter",
            Category = EntryCategory.Enemies,
            TitleEn = "Shadow Archer",
            TitleVi = "Hắc Y Xạ Thủ",
            SubtitleEn = "Ranged Kiting Fiend",
            SubtitleVi = "Quái Bắn Xa Du Kích",
            DescriptionEn = "Cunning archers armed with corrupted spirit bows, shooting from safe distances.",
            DescriptionVi = "Xạ thủ quỷ quyệt luôn giữ khoảng cách an toàn 5m và bắn những phát tên hắc ám hiểm hóc.",
            StatsEn = new Dictionary<string, string>
            {
                { "Base HP", "28" },
                { "Movement Speed", "1.8 m/s (Kiting)" },
                { "Attack Range", "5.0 meters" },
                { "Projectile Damage", "15 DMG per arrow" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Máu Cơ Bản", "28 HP" },
                { "Tốc Độ Di Chuyển", "1.8 m/s (Giữ Cự Ly)" },
                { "Tầm Bắn", "5.0 mét" },
                { "Sát Thương Đạn", "15 DMG mỗi mũi tên" }
            },
            MechanicsEn = "Flees if the player approaches too close and shoots dark arrows on cooldown.",
            MechanicsVi = "Tháo chạy lùi lại nếu người chơi áp sát và bắn đạn ma đạo khi đủ hồi chiêu.",
            TipsEn = "Flying Swords excel at locking onto them outside the melee range.",
            TipsVi = "Phi Kiếm bay tự do là vũ khí khắc chế tốt nhất để triệt hạ chúng từ xa."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_bomber",
            Category = EntryCategory.Enemies,
            TitleEn = "Hellfire Bomber",
            TitleVi = "Hỏa Bộc Quỷ",
            SubtitleEn = "Suicide Explosive Unit",
            SubtitleVi = "Quái Cảm Tử Phát Nổ",
            DescriptionEn = "Volatile creatures infused with demonic gunpowder, seeking self-destruction.",
            DescriptionVi = "Sinh vật chứa đầy hỏa dược ma giới, điên cuồng lao thẳng vào mục tiêu để tự kích nổ.",
            StatsEn = new Dictionary<string, string>
            {
                { "Base HP", "46" },
                { "Movement Speed", "2.5 m/s (Sprinting)" },
                { "Trigger Radius", "1.5 meters" },
                { "Explosion Damage", "45 DMG (AOE)" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Máu Cơ Bản", "46 HP" },
                { "Tốc Độ Chạy", "2.5 m/s (Lao Cực Nhanh)" },
                { "Cự Ly Kích Nổ", "1.5 mét" },
                { "Sát Thương Nổ", "45 DMG diện rộng" }
            },
            MechanicsEn = "Detonates automatically within 1.5m, dealing massive damage to the player.",
            MechanicsVi = "Tự nổ khi vào bán kính 1.5m, gây lượng sát thương khủng khiếp làm tụt máu rất nhanh.",
            TipsEn = "Defeat them before they get close, or trigger their fuse and dodge immediately.",
            TipsVi = "Tiêu diệt từ xa hoặc chủ động nhử chúng kích hoạt nổ rồi lùi lại né vùng đỏ."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_tank",
            Category = EntryCategory.Enemies,
            TitleEn = "Iron Golem",
            TitleVi = "Cự Thiết Giáp",
            SubtitleEn = "Armored Meat Shield",
            SubtitleVi = "Hộ Vệ Giáp Thép Siêu Trâu",
            DescriptionEn = "Colossal iron-clad behemoths acting as moving walls for surrounding demons.",
            DescriptionVi = "Những hộ vệ khổng lồ mình đồng da sắt, che chắn cho quái vật phía sau.",
            StatsEn = new Dictionary<string, string>
            {
                { "Base HP", "95 HP (Very High)" },
                { "Movement Speed", "1.4 m/s" },
                { "Contact Damage", "18 DMG" },
                { "EXP Dropped", "Guaranteed Big Ruby Orb" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Máu Cơ Bản", "95 HP (Cực Dày)" },
                { "Tốc Độ Chạy", "1.4 m/s" },
                { "Sát Thương Chạm", "18 DMG" },
                { "Rớt Kinh Nghiệm", "Chắc chắn rớt Ngọc Lớn Ruby" }
            },
            MechanicsEn = "Absorbs dozens of player attacks before falling. Drops valuable high-grade EXP.",
            MechanicsVi = "Chắn nhiều luồng đạn của người chơi. Khi chết luôn để lại ngọc EXP giá trị cao.",
            TipsEn = "Focus piercing weapons like Slash Sword's wave or Fireball AOE to melt them.",
            TipsVi = "Dùng kiếm khí xuyên thấu hoặc vùng dung nham Cầu Lửa Cấp 4 để thiêu đốt."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_goblin",
            Category = EntryCategory.Enemies,
            TitleEn = "Treasure Goblin",
            TitleVi = "Yêu Tinh Trộm Bảo",
            SubtitleEn = "Rare Wandering Thief",
            SubtitleVi = "Quái Đặc Biệt Rơi Bão Thưởng",
            DescriptionEn = "A sneaky creature carrying a sack of treasures, minding its own business until startled.",
            DescriptionVi = "Tên trộm mang theo bao tải bảo vật, lang thang và bỏ chạy thục mạng khi bị tấn công.",
            StatsEn = new Dictionary<string, string>
            {
                { "Base HP", "120 HP" },
                { "Escape Speed", "3.8 m/s (Fastest)" },
                { "Escape Timer", "22 Seconds" },
                { "Rewards", "Ancient Chest + Rain of Gems" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Máu Cơ Bản", "120 HP" },
                { "Tốc Độ Tẩu Thoát", "3.8 m/s (Nhanh Nhất)" },
                { "Thời Gian Thoát", "22 Giây" },
                { "Phần Thưởng", "Rương Cổ Vật + Mưa Linh Thạch" }
            },
            MechanicsEn = "Stands idle until player comes within 7.5m. Flees for 22s before teleporting away.",
            MechanicsVi = "Đứng yên cho đến khi người chơi đến gần 7.5m. Tẩu thoát trong 22s rồi biến mất.",
            TipsEn = "Follow the HUD arrow pointer and unleash all burst skills to kill it in time!",
            TipsVi = "Theo dấu mũi tên trên màn hình và xả toàn bộ chiêu thức để hạ gục trước khi hết giờ!"
        });

        // =========================================================================
        // 2. KỸ NĂNG & ĐẠN BẮN (WEAPONS & SKILLS)
        // =========================================================================
        _allEntries.Add(new CodexEntry
        {
            Id = "skill_flying_sword",
            Category = EntryCategory.Skills,
            TitleEn = "Flying Sword",
            TitleVi = "Phi Kiếm Hộ Thể",
            SubtitleEn = "Autonomous Rushing Blades",
            SubtitleVi = "Kiếm Bay Xuất Kích Liên Hoàn",
            DescriptionEn = "Spiritual swords orbiting the player, autonomously dashing from foe to foe.",
            DescriptionVi = "Những thanh kiếm bay xoay quanh người chơi, tự động lao liên hoàn từ quái này sang quái khác.",
            StatsEn = new Dictionary<string, string>
            {
                { "Level 1", "1 Blade, 13.5 DMG, 9.45 m/s rush speed" },
                { "Level 2", "2 Blades, 18.2 DMG (+35%)" },
                { "Level 3", "3 Blades, 23.0 DMG, 13.2 m/s rush speed" },
                { "Level 4 (Ultimate)", "4 Blades, 85 DMG AOE Blast (3.5m) every 6s, destroys all bullets" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Cấp 1", "1 Kiếm, 13.5 DMG, tốc độ lướt 9.45 m/s" },
                { "Cấp 2", "2 Kiếm, 18.2 DMG (+35% sát thương)" },
                { "Cấp 3", "3 Kiếm, 23.0 DMG, tốc độ lướt vọt 13.2 m/s" },
                { "Cấp 4 (Ultimate)", "4 Kiếm, nổ AOE 85 DMG (3.5m) mỗi 6s, tiêu hủy 100% đạn địch" }
            },
            MechanicsEn = "Continuous combat state. Switches targets intelligently without 180° ping-pong jitter.",
            MechanicsVi = "Tấn công liên hoàn. Tự động chuyển hướng mượt mà giữa các quái, không bị giật lùi.",
            TipsEn = "Evolves into Sword Tempest with Ancient Sword Talisman.",
            TipsVi = "Kết hợp với Cổ Kiếm Phù để tiến hóa thành Vạn Kiếm Quy Tông."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_slash_sword",
            Category = EntryCategory.Skills,
            TitleEn = "Slash Sword",
            TitleVi = "Trảm Kiếm Bán Nguyệt",
            SubtitleEn = "Close-Combat Cleave & Waves",
            SubtitleVi = "Vung Kiếm Quét & Bắn Kiếm Khí",
            DescriptionEn = "Giant cleaving blades orbiting close to the player, executing devastating arc slashes.",
            DescriptionVi = "Linh kiếm cận chiến vung những đường chém bán nguyệt dũng mãnh quét sạch mọi kẻ địch áp sát.",
            StatsEn = new Dictionary<string, string>
            {
                { "Level 1", "2 Blades, 28 DMG, 155° sweep, 3.2m radius" },
                { "Level 2", "3 Blades, 60 DMG" },
                { "Level 3", "4 Blades, 92 DMG, 3.9m sweep radius" },
                { "Level 4 (Wave)", "5 Blades, 124 DMG + Fires Piercing Sword Wave (65 DMG)" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Cấp 1", "2 Kiếm, 28 DMG, góc quét 155°, bán kính 3.2m" },
                { "Cấp 2", "3 Kiếm, 60 DMG" },
                { "Cấp 3", "4 Kiếm, 92 DMG, bán kính chém rộng 3.9m" },
                { "Cấp 4 (Kiếm Khí)", "5 Kiếm, 124 DMG + Bắn luồng Kiếm Khí xuyên thấu (65 DMG)" }
            },
            MechanicsEn = "Applies wide AOE sweep. Level 4 fires a piercing energy projectile cutting through all enemies.",
            MechanicsVi = "Chém quét diện rộng cực mạnh. Cấp 4 bắn luồng kiếm khí lưỡi liềm bay xuyên thấu toàn bộ quái.",
            TipsEn = "Combine with Flying Sword Lv4 to trigger Celestial Giant Sword Fusion!",
            TipsVi = "Kết hợp cùng Flying Sword Lv4 để kích hoạt Hợp Thể Cự Kiếm Hoàng Kim!"
        });

        var fb = FireballSkillConfigSO.Instance;
        float fbCd1 = fb != null ? fb.GetCooldown(1) : 2.86f;
        float fbCd2 = fb != null ? fb.GetCooldown(2) : 2.53f;
        float fbCd3 = fb != null ? fb.GetCooldown(3) : 2.20f;
        float fbCd4 = fb != null ? fb.GetCooldown(4) : 1.98f;
        float fbDmg1 = fb != null ? fb.GetDamage(1) : 45f;
        float fbDmg2 = fb != null ? fb.GetDamage(2) : 68f;
        float fbDmg3 = fb != null ? fb.GetDamage(3) : 95f;
        float fbDmg4 = fb != null ? fb.GetDamage(4) : 120f;
        int fbSub1 = fb != null ? fb.GetSubFireballCount(1) : 1;
        int fbSub2 = fb != null ? fb.GetSubFireballCount(2) : 2;
        int fbSub3 = fb != null ? fb.GetSubFireballCount(3) : 3;
        int fbSub4 = fb != null ? fb.GetSubFireballCount(4) : 4;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_fireball",
            Category = EntryCategory.Skills,
            TitleEn = "Hellfire Orb",
            TitleVi = "Cầu Lửa Địa Ngục",
            SubtitleEn = "Cluster Burst & Fire Zone",
            SubtitleVi = "Nổ Chùm & Vùng Lửa Thiêu Đốt",
            DescriptionEn = "Blazing demonic spheres targeting enemies and exploding into clusters of sub-fireballs.",
            DescriptionVi = "Cầu lửa hừng hực nhắm vào quái vật, phát nổ chùm thành nhiều cầu lửa con lan tỏa.",
            StatsEn = new Dictionary<string, string>
            {
                { "Level 1", $"{fbDmg1:0} DMG, {fbSub1} Cluster sub-fireball (50% DMG), {fbCd1:0.0}s cooldown" },
                { "Level 2", $"{fbDmg2:0} DMG, {fbSub2} Cluster sub-fireballs, {fbCd2:0.0}s cooldown" },
                { "Level 3 (Twin)", $"Twin Orbs lock 2 separate targets, {fbSub3} clusters, {fbCd3:0.0}s cooldown" },
                { "Level 4 (Inferno)", $"{fbDmg4:0} DMG, {fbSub4} clusters, {fbCd4:0.0}s cooldown + 3.5s Fire Zone" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Cấp 1", $"{fbDmg1:0} DMG, nổ chùm {fbSub1} cầu con, hồi chiêu {fbCd1:0.0}s" },
                { "Cấp 2", $"{fbDmg2:0} DMG, {fbSub2} cầu con, hồi chiêu {fbCd2:0.0}s" },
                { "Cấp 3 (Song Hỏa)", $"Song Hỏa ngắm độc lập 2 quái, {fbSub3} cầu con, hồi chiêu {fbCd3:0.0}s" },
                { "Cấp 4 (Hỏa Vực)", $"{fbDmg4:0} DMG, {fbSub4} cầu con, hồi chiêu {fbCd4:0.0}s + Vùng Lửa 3.5s" }
            },
            MechanicsEn = "High burst explosion. Level 3 prevents miss targeting, Level 4 controls ground choke points.",
            MechanicsVi = "Sát thương nổ bộc phá cực lớn. Cấp 3 không bao giờ trượt quái, Cấp 4 kiểm soát mặt đất hoàn hảo.",
            TipsEn = "Deadly against stationary Tank groups and tightly packed swarms.",
            TipsVi = "Cực kỳ hiệu quả khi tiêu diệt cụm Tank và đàn quái tự sát bầy đàn."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_shield",
            Category = EntryCategory.Skills,
            TitleEn = "Cyan Aegis",
            TitleVi = "Khiên Hộ Thể",
            SubtitleEn = "Defensive Barrier & Shatter Nova",
            SubtitleVi = "Chắn Đòn Tuyệt Đối & Phản Pháo",
            DescriptionEn = "Hexagonal crystalline barrier that negates incoming damage completely.",
            DescriptionVi = "Lớp màng năng lượng lục giác bảo vệ nhân vật, chặn đứng 100% mọi đòn tấn công nguy hiểm.",
            StatsEn = new Dictionary<string, string>
            {
                { "Level 1", "1 Shield layer (blocks 1 hit), 30s recovery" },
                { "Level 2", "2 Shield layers, 18s recovery" },
                { "Level 3", "2 Shield layers, 16s recovery" },
                { "Level 4 (Shatter)", "14s recovery + Shatter Nova (100 AOE DMG, 4.5m, clears bullets)" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Cấp 1", "1 Lớp khiên (đỡ 1 đòn), hồi sau 30s" },
                { "Cấp 2", "2 Lớp khiên độc lập, hồi sau 18s" },
                { "Cấp 3", "2 Lớp khiên độc lập, hồi sau 16s" },
                { "Cấp 4 (Phản Pháo)", "Hồi sau 14s + Nổ vỡ khiên 100 AOE DMG (4.5m), quét sạch đạn địch" }
            },
            MechanicsEn = "Each layer recovers on an independent timer. Shatter triggers when last shield breaks.",
            MechanicsVi = "Từng lớp khiên đếm hồi độc lập. Cấp 4 kích hoạt vụ nổ phản pháo cứu nguy khi vỡ khiên.",
            TipsEn = "Essential defense when surviving high-density Bomber horde waves.",
            TipsVi = "Vũ khí phòng ngự sinh tồn tối thượng trước những đợt bão đạn và quái tự sát."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_giant_fusion",
            Category = EntryCategory.Skills,
            TitleEn = "Celestial Giant Sword",
            TitleVi = "Cự Kiếm Hoàng Kim Hợp Thể",
            SubtitleEn = "Ultimate Fusion Skill",
            SubtitleVi = "Kỹ Năng Hợp Thể Tối Thượng",
            DescriptionEn = "A celestial golden blade dropping from the heavens, crushing all below.",
            DescriptionVi = "Thanh kiếm khổng lồ màu vàng kim từ trời cao cắm thẳng xuống đất, chấn động trời đất.",
            StatsEn = new Dictionary<string, string>
            {
                { "Requirement", "Flying Sword Lv4 + Slash Sword Lv4" },
                { "Cooldown", "Periodic trigger every 5.5 seconds" },
                { "Impact Damage", "220 AOE DMG (4.5m radius shockwave)" },
                { "Radiant Waves", "Fires 5 Piercing Sword Waves (75 DMG) at 72° angles" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Điều Kiện", "Flying Sword Cấp 4 + Slash Sword Cấp 4" },
                { "Hồi Chiêu", "Định kỳ rơi mỗi 5.5 giây" },
                { "Sát Thương Cắm Đất", "220 AOE DMG (Sóng chấn động 4.5m)" },
                { "Kiếm Khí Lan Tỏa", "Bắn tỏa 5 luồng Kiếm Khí (75 DMG) theo góc đều 72°" }
            },
            MechanicsEn = "High impact area denial. Clears the screen of mobs and fires 5 piercing waves outward.",
            MechanicsVi = "Uy lực hủy diệt diện rộng, quét sạch bầy quái quanh tâm cắm kiếm và bắn tỏa 5 kiếm khí.",
            TipsEn = "Select the Fusion Perk card as soon as both swords reach Level 4!",
            TipsVi = "Hãy chọn thẻ bài Hợp Thể ngay khi cả hai kỹ năng kiếm đạt Cấp 4!"
        });

        // =========================================================================
        // 3. CỔ VẬT BỊ ĐỘNG (RELICS)
        // =========================================================================
        _allEntries.Add(new CodexEntry
        {
            Id = "relic_boots",
            Category = EntryCategory.Relics,
            TitleEn = "Windrunner Boots",
            TitleVi = "Thần Hành Hài (Giày Gió)",
            SubtitleEn = "Passive Relic: Mobility",
            SubtitleVi = "Cổ Vật: Tốc Độ Di Chuyển",
            DescriptionEn = "Boots woven from ethereal clouds, lightening the user's steps.",
            DescriptionVi = "Đôi giày bện từ mây ngũ sắc, gia tăng tốc độ lướt đi của nhân vật.",
            StatsEn = new Dictionary<string, string>
            {
                { "Effect", "+15% Movement Speed per level" },
                { "Max Level", "Level 5 (+75% Speed)" },
                { "Synergy", "Slash Sword Evolution" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Hiệu Ứng", "+15% Tốc độ di chuyển mỗi cấp" },
                { "Cấp Tối Đa", "Cấp 5 (+75% Tốc chạy)" },
                { "Hợp Kỹ", "Tiến hóa Trảm Kiếm Hư Không" }
            },
            MechanicsEn = "Directly modifies player movement velocity. Crucial for outrunning sprinters.",
            MechanicsVi = "Cộng trực tiếp vào vận tốc di chuyển, giúp thoát khỏi vòng vây của quái nhanh.",
            TipsEn = "Prioritize this if you struggle with dodging Bomber suicide charges.",
            TipsVi = "Ưu tiên nhặt nếu bạn gặp khó khăn trong việc né tránh quái nổ tự sát."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "relic_magnet",
            Category = EntryCategory.Relics,
            TitleEn = "Spirit Magnet Orb",
            TitleVi = "Tụ Linh Châu",
            SubtitleEn = "Passive Relic: Collection",
            SubtitleVi = "Cổ Vật: Bán Kính Thu Thập",
            DescriptionEn = "A jade orb that resonates with celestial crystals and draws them from afar.",
            DescriptionVi = "Viên ngọc bích phát ra từ trường hút mọi ngọc kinh nghiệm và vật phẩm rơi trên đất.",
            StatsEn = new Dictionary<string, string>
            {
                { "Effect", "+50% Magnet Pickup Radius per level" },
                { "Max Level", "Level 5 (+250% Range)" },
                { "Synergy", "Cyan Aegis Evolution" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Hiệu Ứng", "+50% Bán kính hút ngọc EXP mỗi cấp" },
                { "Cấp Tối Đa", "Cấp 5 (+250% Tầm hút)" },
                { "Hợp Kỹ", "Tiến hóa Khiên Hộ Thể" }
            },
            MechanicsEn = "Enables harvesting EXP safely without venturing directly into hostile enemy hordes.",
            MechanicsVi = "Cho phép thu thập kinh nghiệm an toàn từ xa mà không cần lao vào giữa bầy quái.",
            TipsEn = "Dramatically speeds up leveling pace and snowballing power in the mid-game.",
            TipsVi = "Gia tăng tốc độ lên cấp vượt bậc, giúp tích lũy sức mạnh rất nhanh ở giữa trận."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "relic_heart",
            Category = EntryCategory.Relics,
            TitleEn = "Crystal Heart",
            TitleVi = "Tim Pha Lê",
            SubtitleEn = "Passive Relic: Longevity",
            SubtitleVi = "Cổ Vật: Sinh Mệnh Tối Đa",
            DescriptionEn = "A pulsing crimson crystal granting boundless vigor and instant vitality.",
            DescriptionVi = "Khối pha lê đỏ rực đập theo nhịp tim, ban phát sinh lực dồi dào và hồi phục tức thời.",
            StatsEn = new Dictionary<string, string>
            {
                { "Effect", "+30 Max HP and restores full HP upon upgrade" },
                { "Max Level", "Level 5 (+150 Max HP)" },
                { "Synergy", "Hellfire Orb Evolution" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Hiệu Ứng", "+30 Máu tối đa và HỒI ĐẦY MÁU ngay khi nhặt" },
                { "Cấp Tối Đa", "Cấp 5 (+150 Max HP)" },
                { "Hợp Kỹ", "Tiến hóa Cầu Lửa Phượng Hoàng" }
            },
            MechanicsEn = "Acts as an emergency heal while simultaneously raising the health ceiling.",
            MechanicsVi = "Vừa tăng ngưỡng máu tối đa, vừa đóng vai trò như bình máu cứu nguy khẩn cấp.",
            TipsEn = "Pick this up when low on health to instantly recover 100% HP during intense fights.",
            TipsVi = "Chọn thẻ bài này lúc sắp cạn máu để lật ngược tình thế với thanh máu đầy 100%!"
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "relic_talisman",
            Category = EntryCategory.Relics,
            TitleEn = "Ancient Sword Talisman",
            TitleVi = "Cổ Kiếm Phù",
            SubtitleEn = "Passive Relic: Raw Power",
            SubtitleVi = "Cổ Vật: Sát Thương Uy Lực",
            DescriptionEn = "An inscribed talisman boosting the sharp edge of all physical and spiritual blades.",
            DescriptionVi = "Lá bùa cổ khắc ấn chú kiếm đạo, gia tăng sức công phá cho toàn bộ đòn đánh.",
            StatsEn = new Dictionary<string, string>
            {
                { "Effect", "+15% Global Damage Multiplier per level" },
                { "Max Level", "Level 5 (+75% Total Damage)" },
                { "Synergy", "Flying Sword Evolution" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Hiệu Ứng", "+15% Hệ số nhân sát thương tổng thể mỗi cấp" },
                { "Cấp Tối Đa", "Cấp 5 (+75% Tổng sát thương)" },
                { "Hợp Kỹ", "Tiến hóa Phi Kiếm Vạn Kiếm Quy Tông" }
            },
            MechanicsEn = "Multiplies damage output across ALL weapons including Fireball, Swords and Waves.",
            MechanicsVi = "Khuếch đại sát thương của TẤT CẢ vũ khí (Phi Kiếm, Trảm Kiếm, Cầu Lửa, Cự Kiếm).",
            TipsEn = "The premier relic for breaking through late-game armored elite hordes.",
            TipsVi = "Cổ vật quan trọng nhất để xé tan các đợt quái giáp dày ở nửa cuối trận đấu."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "relic_dice",
            Category = EntryCategory.Relics,
            TitleEn = "Fortune's Dice",
            TitleVi = "Xúc Xắc Vận Mệnh",
            SubtitleEn = "Passive Relic: Luck & Rerolls",
            SubtitleVi = "Cổ Vật: May Mắn & Đổi Bài",
            DescriptionEn = "A die carved from meteor stone that bends probability in the wielder's favor.",
            DescriptionVi = "Viên xúc xắc tạc từ thiên thạch, xoay chuyển vận may ban tặng thêm lượt chọn lại bài.",
            StatsEn = new Dictionary<string, string>
            {
                { "Effect", "+2 Extra Rerolls per level & +10% High-grade drop luck" },
                { "Max Level", "Level 5 (+10 Extra Rerolls)" },
                { "Synergy", "Arcane Volley Evolution" }
            },
            StatsVi = new Dictionary<string, string>
            {
                { "Hiệu Ứng", "+2 Lượt Reroll đổi bài mỗi cấp & +10% May mắn rớt đồ quý" },
                { "Cấp Tối Đa", "Cấp 5 (+10 Lượt Reroll)" },
                { "Hợp Kỹ", "Tiến hóa Thiên Cơ Đan Mạc" }
            },
            MechanicsEn = "Allows rerolling bad level-up options to sculpt the exact build you desire.",
            MechanicsVi = "Cho phép đổi những lựa chọn kỹ năng không mong muốn để hoàn thiện build chuẩn xác.",
            TipsEn = "Take at least 1 level early to guarantee finding your core weapon combinations.",
            TipsVi = "Nên lấy ít nhất 1 cấp ở đầu trận để dễ dàng tìm kiếm những mảnh ghép kỹ năng cần thiết."
        });
    }
}

