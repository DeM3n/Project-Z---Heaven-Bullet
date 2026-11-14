using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kho dữ liệu Bách Khoa Toàn Thư (Codex Database):
/// Lưu trữ toàn bộ thông tin chi tiết về Quái vật, Kỹ năng/Đạn bắn và Cổ vật bị động.
/// Đảm bảo Single Source of Truth (SSOT): Tự động đồng bộ thông số thời gian thực từ
/// các ScriptableObject (FlyingSword, SlashSword, Fireball, Boomerang, Electric).
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
        public string BadgeEn;
        public string BadgeVi;
        public string IconCode;
        public Color ThemeColor = Color.white;
        public string DescriptionEn;
        public string DescriptionVi;

        // Bảng 4 chỉ số hiển thị dạng Card Matrix (2x2)
        public string[] ParamLabelsEn = new string[4];
        public string[] ParamValuesEn = new string[4];
        public string[] ParamLabelsVi = new string[4];
        public string[] ParamValuesVi = new string[4];

        // Tiến trình cấp độ hoặc công thức hợp thể
        public string ProgressionSummaryEn;
        public string ProgressionSummaryVi;

        // Legacy / Fallback stats dictionary
        public Dictionary<string, string> StatsEn = new Dictionary<string, string>();
        public Dictionary<string, string> StatsVi = new Dictionary<string, string>();

        public string MechanicsEn;
        public string MechanicsVi;
        public string TipsEn;
        public string TipsVi;
        public string SpritePath;
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

    public static void InvalidateCache()
    {
        _allEntries = null;
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
            BadgeEn = "SWARM • MELEE",
            BadgeVi = "CẬN CHIẾN BẦY ĐÀN",
            IconCode = "CD",
            ThemeColor = new Color(0.9f, 0.3f, 0.3f, 1f),
            DescriptionEn = "Corrupted sword thralls that relentlessly march toward any living essence in dense hordes.",
            DescriptionVi = "Những kiếm nô bị tà khí tha hóa, liên tục áp sát người chơi theo bầy đàn đông đảo.",
            ParamLabelsEn = new string[] { "BASE HP", "MOVEMENT SPEED", "CONTACT DAMAGE", "EXP DROPPED" },
            ParamValuesEn = new string[] { "32 HP", "2.2 m/s", "10 DMG / tick", "Small / Med Orb" },
            ParamLabelsVi = new string[] { "MÁU CƠ BẢN", "TỐC ĐỘ CHẠY", "SÁT THƯƠNG CHẠM", "KINH NGHIỆM" },
            ParamValuesVi = new string[] { "32 HP", "2.2 m/s", "10 DMG / nhịp", "Ngọc Nhỏ / Vừa" },
            ProgressionSummaryEn = "Spawns in heavy clusters • Scales +30% HP per minute of survival time",
            ProgressionSummaryVi = "Xuất hiện theo từng cụm lớn • Máu tăng thêm +30% mỗi phút trận đấu",
            MechanicsEn = "Relentlessly paths toward the player. Lacks evasion, swarming from all edges of the screen.",
            MechanicsVi = "Liên tục tìm đường lao thẳng về người chơi. Không có kỹ năng né, áp đảo bằng số lượng từ mọi rìa màn hình.",
            TipsEn = "Easy to wipe with wide Slash Sword sweeps or Fireball cluster explosions.",
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
            BadgeEn = "RANGED • KITING",
            BadgeVi = "TẦM XA DU KÍCH",
            IconCode = "SA",
            ThemeColor = new Color(0.6f, 0.4f, 0.9f, 1f),
            DescriptionEn = "Cunning archers armed with corrupted spirit bows, shooting piercing dark arrows from safe distances.",
            DescriptionVi = "Xạ thủ quỷ quyệt luôn duy trì khoảng cách an toàn và bắn những phát tên hắc ám hiểm hóc.",
            ParamLabelsEn = new string[] { "BASE HP", "MOVEMENT SPEED", "ATTACK RANGE", "ARROW DAMAGE" },
            ParamValuesEn = new string[] { "28 HP", "1.8 m/s", "5.0 meters", "10 DMG (2.6s CD)" },
            ParamLabelsVi = new string[] { "MÁU CƠ BẢN", "TỐC ĐỘ CHẠY", "TẦM BẮN TỐI ĐA", "SÁT THƯƠNG TÊN" },
            ParamValuesVi = new string[] { "28 HP", "1.8 m/s", "5.0 mét", "10 DMG (hồi 2.6s)" },
            ProgressionSummaryEn = "Maintains 5m distance • Flees backward when cornered • Fires periodic projectiles",
            ProgressionSummaryVi = "Tự động lùi giữ cự ly 5m • Bỏ chạy khi bị áp sát • Định kỳ bắn tên ám ảnh",
            MechanicsEn = "Flees if the player approaches too close and shoots dark arrows on cooldown.",
            MechanicsVi = "Tháo chạy lùi lại nếu người chơi áp sát và bắn đạn ma đạo khi đủ hồi chiêu.",
            TipsEn = "Flying Swords and Chain Lightning excel at locking onto them outside melee range.",
            TipsVi = "Phi Kiếm bay tự do và Lôi Điện là vũ khí khắc chế tốt nhất để triệt hạ chúng từ xa."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_bomber",
            Category = EntryCategory.Enemies,
            TitleEn = "Hellfire Bomber",
            TitleVi = "Hỏa Bộc Quỷ",
            SubtitleEn = "Suicide Explosive Unit",
            SubtitleVi = "Quái Cảm Tử Phát Nổ",
            BadgeEn = "KAMIKAZE • BURST",
            BadgeVi = "CẢM TỬ BỘC PHÁ",
            IconCode = "HB",
            ThemeColor = new Color(1f, 0.45f, 0.15f, 1f),
            DescriptionEn = "Volatile creatures infused with demonic gunpowder, furiously sprinting to self-destruct upon contact.",
            DescriptionVi = "Sinh vật chứa đầy hỏa dược ma giới, tăng tốc điên cuồng lao thẳng vào mục tiêu để tự kích nổ.",
            ParamLabelsEn = new string[] { "BASE HP", "SPRINT SPEED", "BLAST RADIUS", "EXPLOSION DMG" },
            ParamValuesEn = new string[] { "46 HP", "3.2 m/s (Fast)", "2.8 meters", "25 AOE DMG" },
            ParamLabelsVi = new string[] { "MÁU CƠ BẢN", "TỐC ĐỘ BỘC TỐC", "BÁN KÍNH NỔ", "SÁT THƯƠNG NỔ" },
            ParamValuesVi = new string[] { "46 HP", "3.2 m/s (Nhanh)", "2.8 mét", "25 DMG diện rộng" },
            ProgressionSummaryEn = "Normal speed (2.4m/s) -> Sprints (3.2m/s) within 6m -> Detonates within 1.5m",
            ProgressionSummaryVi = "Tốc độ gốc 2.4m/s -> Bộc tốc 3.2m/s khi vào cự ly 6m -> Nổ tung khi cách 1.5m",
            MechanicsEn = "Sprints toward the player when within 6m and detonates after a brief 0.3s fuse time.",
            MechanicsVi = "Tăng tốc chạy cực nhanh khi tới gần 6m và kích hoạt kíp nổ sau 0.3s.",
            TipsEn = "Listen for their hissing fuse sound. Backpedal or activate Cyan Aegis to absorb the blast safely.",
            TipsVi = "Lắng nghe tiếng rít kích nổ, lùi lại giữ khoảng cách hoặc kích hoạt Khiên Hộ Thể để triệt tiêu cú nổ."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_tank",
            Category = EntryCategory.Enemies,
            TitleEn = "Iron Golem",
            TitleVi = "Thiết Giáp Cự Nhân",
            SubtitleEn = "Heavy Armored Juggernaut",
            SubtitleVi = "Quái Tank Giáp Sắt Hạng Nặng",
            BadgeEn = "JUGGERNAUT • TANK",
            BadgeVi = "GIÁP SẮT HẠNG NẶNG",
            IconCode = "IG",
            ThemeColor = new Color(0.4f, 0.65f, 0.85f, 1f),
            DescriptionEn = "Colossal walking statues clad in thick steel plates, absorbing immense punishment before yielding.",
            DescriptionVi = "Bức tượng khổng lồ khoác giáp thép dày, chịu lượng sát thương cực lớn mở đường cho bầy quái nhỏ.",
            ParamLabelsEn = new string[] { "BASE HP", "MOVEMENT SPEED", "CONTACT DAMAGE", "EXP DROPPED" },
            ParamValuesEn = new string[] { "160 HP", "1.2 m/s (Slow)", "18 DMG / tick", "Guaranteed Big Orb" },
            ParamLabelsVi = new string[] { "MÁU CƠ BẢN", "TỐC ĐỘ CHẠY", "SÁT THƯƠNG CHẠM", "KINH NGHIỆM" },
            ParamValuesVi = new string[] { "160 HP", "1.2 m/s (Chậm)", "18 DMG / nhịp", "Chắc chắn Ngọc Lớn" },
            ProgressionSummaryEn = "Spawns during intense wave peaks • Provides mobile cover for ranged archers",
            ProgressionSummaryVi = "Xuất hiện trong các đợt cao trào • Làm lá chắn di động che chở cho xạ thủ",
            MechanicsEn = "Extremely high durability with slow movement. Shields weaker monsters walking behind it.",
            MechanicsVi = "Độ chống chịu cực cao nhưng di chuyển chậm chạp. Che chở các quái máu yếu phía sau.",
            TipsEn = "Melt them with Fireball's lingering Fire Zone or focused Chain Lightning bounces.",
            TipsVi = "Thiêu đốt chúng nhanh chóng bằng Vùng Lửa của Cầu Lửa hoặc Lôi Điện nảy ngược ở Cấp 4."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "enemy_goblin",
            Category = EntryCategory.Enemies,
            TitleEn = "Treasure Goblin",
            TitleVi = "Yêu Tinh Kho Báu",
            SubtitleEn = "Rare Wandering Thief",
            SubtitleVi = "Kẻ Trộm Kho Báu Quý Hiếm",
            BadgeEn = "ELITE • BOUNTY",
            BadgeVi = "KHO BÁU QUÝ HIẾM",
            IconCode = "TG",
            ThemeColor = new Color(1f, 0.82f, 0.2f, 1f),
            DescriptionEn = "A cowardly, greedy imp carrying a bulging sack of Ancient Chests and shimmering gems.",
            DescriptionVi = "Yêu quái tham lam mang túi báu vật to tướng, nhút nhát bỏ chạy thục mạng khi có người đến gần.",
            ParamLabelsEn = new string[] { "BASE HP", "FLEE SPEED", "ESCAPE TIMER", "REWARDS" },
            ParamValuesEn = new string[] { "120 HP", "3.8 m/s (Fastest)", "22 Seconds", "Ancient Chest + Gems" },
            ParamLabelsVi = new string[] { "MÁU CƠ BẢN", "TỐC ĐỘ CHẠY", "THỜI GIAN THOÁT", "PHẦN THƯỞNG" },
            ParamValuesVi = new string[] { "120 HP", "3.8 m/s (Nhanh nhất)", "22 Giây", "Rương Cổ Vật + Linh Thạch" },
            ProgressionSummaryEn = "Idle until approached within 7.5m -> Flees for 22s -> Teleports away forever",
            ProgressionSummaryVi = "Đứng yên đến khi người chơi vào 7.5m -> Chạy trốn 22s -> Biến mất vĩnh viễn",
            MechanicsEn = "Stands idle until player approaches within 7.5m. Flees for 22s before teleporting away.",
            MechanicsVi = "Đứng yên cho đến khi người chơi đến gần 7.5m. Tẩu thoát trong 22s rồi biến mất.",
            TipsEn = "Follow the HUD arrow pointer and unleash all burst skills to kill it in time!",
            TipsVi = "Theo dấu mũi tên chỉ dẫn trên màn hình và xả toàn bộ chiêu thức để hạ gục trước khi hết giờ!"
        });

        // =========================================================================
        // 2. KỸ NĂNG & VŨ KHÍ (WEAPONS & SKILLS) - SINGLE SOURCE OF TRUTH (SSOT)
        // =========================================================================
        var fs = FlyingSwordSkillConfigSO.Instance;
        int fsCount1 = fs != null ? fs.GetSwordCount(1) : 1;
        int fsCount4 = fs != null ? fs.GetSwordCount(4) : 4;
        float fsDmg1 = fs != null ? fs.GetDamage(1) : 13.5f;
        float fsDmg4 = fs != null ? fs.GetDamage(4) : 27.7f;
        float fsSpd1 = fs != null ? fs.GetRushSpeed(1) : 9.45f;
        float fsSpd4 = fs != null ? fs.GetRushSpeed(4) : 15.0f;
        float fsAoeDmg = fs != null ? fs.AoeDamage : 85f;
        float fsAoeRad = fs != null ? fs.AoeRadius : 3.5f;
        float fsUltCd = fs != null ? fs.UltimateCooldown : 6.0f;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_flying_sword",
            Category = EntryCategory.Skills,
            TitleEn = "Flying Sword",
            TitleVi = "Phi Kiếm Hộ Thể",
            SubtitleEn = "Autonomous Rushing Blades",
            SubtitleVi = "Kiếm Bay Xuất Kích Liên Hoàn",
            BadgeEn = "AUTONOMOUS • RUSH",
            BadgeVi = "TỰ ĐỘNG XUẤT KÍCH",
            IconCode = "FS",
            ThemeColor = new Color(1f, 0.85f, 0.35f, 1f),
            DescriptionEn = "Spiritual swords orbiting the player, autonomously dashing from foe to foe with relentless agility.",
            DescriptionVi = "Những thanh linh kiếm xoay quanh người chơi, tự động lao vọt liên hoàn từ quái này sang quái khác.",
            ParamLabelsEn = new string[] { "BASE DAMAGE", "RUSH SPEED", "SWORD COUNT", "ULTIMATE BLAST" },
            ParamValuesEn = new string[] { $"{fsDmg1:0.0} - {fsDmg4:0.0} DMG", $"{fsSpd1:0.0} - {fsSpd4:0.0} m/s", $"{fsCount1} - {fsCount4} Blades", $"{fsAoeDmg:0} DMG ({fsAoeRad:0.0}m / {fsUltCd:0}s CD)" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG", "TỐC ĐỘ LƯỚT", "SỐ LƯỢNG KIẾM", "NỔ ULTIMATE AOE" },
            ParamValuesVi = new string[] { $"{fsDmg1:0.0} - {fsDmg4:0.0} DMG", $"{fsSpd1:0.0} - {fsSpd4:0.0} m/s", $"{fsCount1} - {fsCount4} Thanh", $"{fsAoeDmg:0} DMG ({fsAoeRad:0.0}m / hồi {fsUltCd:0}s)" },
            ProgressionSummaryEn = $"Lv1: {fsCount1} Blade • Lv2: 2 Blades • Lv3: 3 Blades • Lv4: {fsCount4} Blades + AOE Blast ({fsAoeDmg:0} DMG, Clears Bullets)",
            ProgressionSummaryVi = $"Cấp 1: {fsCount1} Kiếm • Cấp 2: 2 Kiếm • Cấp 3: 3 Kiếm • Cấp 4: {fsCount4} Kiếm + Nổ AOE ({fsAoeDmg:0} DMG, Quét sạch đạn)",
            MechanicsEn = "Swords patrol in an orbit until enemies enter detection range, then execute continuous target chaining.",
            MechanicsVi = "Kiếm bay quanh người bảo vệ đến khi phát hiện quái, sau đó liên tục chuyển mục tiêu lướt chém không ngừng nghỉ.",
            TipsEn = "Combines with Slash Sword Lv4 to unleash the Celestial Giant Sword Fusion!",
            TipsVi = "Kết hợp cùng Slash Sword Cấp 4 để kích hoạt Hợp Thể Cự Kiếm Hoàng Kim!"
        });

        var ss = SlashSwordSkillConfigSO.Instance;
        int ssCount1 = ss != null ? ss.GetSwordCount(1) : 2;
        int ssCount4 = ss != null ? ss.GetSwordCount(4) : 5;
        float ssDmg1 = ss != null ? ss.GetDamage(1) : 38f;
        float ssDmg4 = ss != null ? ss.GetDamage(4) : 134f;
        float ssRad1 = ss != null ? ss.GetSlashRadius(1) : 3.2f;
        float ssRad4 = ss != null ? ss.GetSlashRadius(4) : 4.25f;
        float ssCd1 = ss != null ? ss.GetCooldown(1) : 3.6f;
        float ssCd4 = ss != null ? ss.GetCooldown(4) : 1.8f;
        float ssWaveDmg = ss != null ? ss.WaveDamage : 65f;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_slash_sword",
            Category = EntryCategory.Skills,
            TitleEn = "Slash Sword",
            TitleVi = "Trảm Kiếm Bán Nguyệt",
            SubtitleEn = "Close-Combat Cleave & Waves",
            SubtitleVi = "Vung Kiếm Quét & Bắn Kiếm Khí",
            BadgeEn = "CLEAVE • MELEE AOE",
            BadgeVi = "CHÉM QUÉT BÁN NGUYỆT",
            IconCode = "SS",
            ThemeColor = new Color(0.85f, 0.55f, 1f, 1f),
            DescriptionEn = "Giant cleaving blades orbiting close to the player, executing devastating arc slashes.",
            DescriptionVi = "Linh kiếm cận chiến vung những đường chém bán nguyệt dũng mãnh quét sạch mọi kẻ địch áp sát.",
            ParamLabelsEn = new string[] { "SLASH DAMAGE", "COOLDOWN", "SLASH RADIUS", "SWORD COUNT" },
            ParamValuesEn = new string[] { $"{ssDmg1:0} - {ssDmg4:0} DMG", $"{ssCd1:0.0}s - {ssCd4:0.0}s", $"{ssRad1:0.0}m - {ssRad4:0.0}m (155°)", $"{ssCount1} - {ssCount4} Blades" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG CHÉM", "HỒI CHIÊU", "BÁN KÍNH CHÉM", "SỐ LƯỢNG KIẾM" },
            ParamValuesVi = new string[] { $"{ssDmg1:0} - {ssDmg4:0} DMG", $"{ssCd1:0.0}s - {ssCd4:0.0}s", $"{ssRad1:0.0}m - {ssRad4:0.0}m (155°)", $"{ssCount1} - {ssCount4} Thanh" },
            ProgressionSummaryEn = $"Lv1: {ssCount1} Blades • Lv2: 3 Blades • Lv3: 4 Blades • Lv4: {ssCount4} Blades + Piercing Sword Wave ({ssWaveDmg:0} DMG)",
            ProgressionSummaryVi = $"Cấp 1: {ssCount1} Kiếm • Cấp 2: 3 Kiếm • Cấp 3: 4 Kiếm • Cấp 4: {ssCount4} Kiếm + Bắn Kiếm Khí Xuyên Thấu ({ssWaveDmg:0} DMG)",
            MechanicsEn = "Blades rotate outward in an expanding 155° arc. Level 4 projects an energy wave passing through all foes.",
            MechanicsVi = "Lưỡi kiếm mở rộng theo cung chém 155°. Ở Cấp 4, mỗi đòn chém phóng một lưỡi kiếm khí bay xuyên thấu mọi quái.",
            TipsEn = "Pair with Windrunner Boots to kite while slicing down swarming hordes.",
            TipsVi = "Kết hợp cùng Thần Hành Hài để thả diều và càn quét mọi đợt quái tự sát bầy đàn."
        });

        var fb = FireballSkillConfigSO.Instance;
        float fbCd1 = fb != null ? fb.GetCooldown(1) : 2.86f;
        float fbCd4 = fb != null ? fb.GetCooldown(4) : 1.98f;
        float fbDmg1 = fb != null ? fb.GetDamage(1) : 45f;
        float fbDmg4 = fb != null ? fb.GetDamage(4) : 120f;
        int fbSub1 = fb != null ? fb.GetSubFireballCount(1) : 1;
        int fbSub4 = fb != null ? fb.GetSubFireballCount(4) : 4;
        float fbRad1 = fb != null ? fb.GetExplosionRadius(1) : 1.8f;
        float fbRad4 = fb != null ? fb.GetExplosionRadius(4) : 2.8f;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_fireball",
            Category = EntryCategory.Skills,
            TitleEn = "Hellfire Orb",
            TitleVi = "Cầu Lửa Địa Ngục",
            SubtitleEn = "Cluster Burst & Fire Zone",
            SubtitleVi = "Nổ Chùm & Vùng Lửa Thiêu Đốt",
            BadgeEn = "AOE • CLUSTER BURST",
            BadgeVi = "NỔ CHÙM DIỆN RỘNG",
            IconCode = "FB",
            ThemeColor = new Color(1f, 0.45f, 0.15f, 1f),
            DescriptionEn = "Blazing demonic spheres targeting enemies and violently exploding into clusters of sub-fireballs.",
            DescriptionVi = "Cầu lửa hừng hực nhắm vào quái vật, phát nổ chùm thành nhiều cầu lửa con lan tỏa thiêu đốt chiến trường.",
            ParamLabelsEn = new string[] { "EXPLOSION DMG", "COOLDOWN", "EXPLOSION RADIUS", "CLUSTER COUNT" },
            ParamValuesEn = new string[] { $"{fbDmg1:0} - {fbDmg4:0} DMG", $"{fbCd1:0.0}s - {fbCd4:0.0}s", $"{fbRad1:0.0}m - {fbRad4:0.0}m", $"{fbSub1} - {fbSub4} Sub-Orbs" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG NỔ", "HỒI CHIÊU", "BÁN KÍNH NỔ", "SỐ CẦU LỬA CON" },
            ParamValuesVi = new string[] { $"{fbDmg1:0} - {fbDmg4:0} DMG", $"{fbCd1:0.0}s - {fbCd4:0.0}s", $"{fbRad1:0.0}m - {fbRad4:0.0}m", $"{fbSub1} - {fbSub4} Quả con" },
            ProgressionSummaryEn = $"Lv1: {fbSub1} Sub-Orb • Lv2: 2 Sub-Orbs • Lv3: Twin Primary Orbs • Lv4: {fbSub4} Sub-Orbs + 3.5s Lingering Fire Zone",
            ProgressionSummaryVi = $"Cấp 1: {fbSub1} Cầu con • Cấp 2: 2 Cầu con • Cấp 3: Song Hỏa độc lập • Cấp 4: {fbSub4} Cầu con + Vùng Lửa Đốt 3.5s",
            MechanicsEn = "Primary orb bursts on impact, scattering sub-fireballs at 50% damage. Level 4 leaves a burning pool.",
            MechanicsVi = "Cầu lửa mẹ phát nổ khi va chạm, bắn các cầu con gây 50% sát thương. Cấp 4 để lại vùng lửa đốt liên tục.",
            TipsEn = "Extremely effective against clustered Tank enemies and tight choke points.",
            TipsVi = "Cực kỳ hiệu quả khi tiêu diệt cụm Thiết Giáp Cự Nhân và những điểm nghẽn chật hẹp."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_shield",
            Category = EntryCategory.Skills,
            TitleEn = "Cyan Aegis",
            TitleVi = "Khiên Hộ Thể",
            SubtitleEn = "Defensive Barrier & Shatter Nova",
            SubtitleVi = "Chắn Đòn Tuyệt Đối & Phản Pháo",
            BadgeEn = "DEFENSE • BARRIER",
            BadgeVi = "PHÒNG THỦ TUYỆT ĐỐI",
            IconCode = "CA",
            ThemeColor = new Color(0.35f, 0.85f, 1f, 1f),
            DescriptionEn = "Hexagonal crystalline barrier that completely negates incoming damage.",
            DescriptionVi = "Lớp màng năng lượng lục giác bảo vệ nhân vật, chặn đứng 100% mọi đòn tấn công nguy hiểm.",
            ParamLabelsEn = new string[] { "MAX CHARGES", "RECHARGE CD", "DAMAGE BLOCKED", "SHATTER NOVA" },
            ParamValuesEn = new string[] { "1 - 2 Shields", "27.0s -> 12.6s", "100% Negation", "100 DMG (4.5m) at Lv4" },
            ParamLabelsVi = new string[] { "SỐ LỚP KHIÊN", "HỒI KHIÊN", "CHẶN ĐÒN", "NỔ PHẢN PHÁO" },
            ParamValuesVi = new string[] { "1 - 2 Lớp độc lập", "27.0s -> 12.6s", "Triệt tiêu 100%", "100 DMG (4.5m) ở Cấp 4" },
            ProgressionSummaryEn = "Lv1: 1 Layer (27s CD) • Lv2: 2 Layers (16.2s CD) • Lv3: 2 Layers (14.4s CD) • Lv4: Shatter Nova (Clears Bullets)",
            ProgressionSummaryVi = "Cấp 1: 1 Lớp (27s) • Cấp 2: 2 Lớp (16.2s) • Cấp 3: 2 Lớp (14.4s) • Cấp 4: Nổ vỡ khiên (Quét sạch đạn)",
            MechanicsEn = "Each layer recovers on an independent timer. Shatter Nova triggers when the last shield breaks.",
            MechanicsVi = "Từng lớp khiên đếm hồi độc lập. Cấp 4 kích hoạt vụ nổ phản pháo quét sạch đạn địch khi vỡ khiên.",
            TipsEn = "Essential defense when surviving high-density Bomber horde waves and boss attacks.",
            TipsVi = "Vũ khí phòng ngự sinh tồn tối thượng trước những đợt bão đạn và quái tự sát bầy đàn."
        });

        var bm = BoomerangSkillConfigSO.Instance;
        float bmDmg1 = bm != null ? bm.GetDamage(1) : 35f;
        float bmDmg4 = bm != null ? bm.GetDamage(4) : 80f;
        float bmCd1 = bm != null ? bm.GetCooldown(1) : 8.1f;
        float bmCd4 = bm != null ? bm.GetCooldown(4) : 5.4f;
        int bmCount1 = bm != null ? bm.GetCount(1) : 1;
        int bmCount4 = bm != null ? bm.GetCount(4) : 2;
        float bmRad1 = bm != null ? bm.GetHitRadius(1) : 0.55f;
        float bmRad4 = bm != null ? bm.GetHitRadius(4) : 0.95f;
        float bmSpd = bm != null ? bm.FlySpeed : 12.5f;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_boomerang",
            Category = EntryCategory.Skills,
            TitleEn = "Celestial Boomerang",
            TitleVi = "Phi Tiêu Hồi Toàn",
            SubtitleEn = "Piercing Aero-Blade & Catch Synergy",
            SubtitleVi = "Lưỡi Dao Xuyên Thấu & Bắt Dính",
            BadgeEn = "PIERCING • REBOUND",
            BadgeVi = "XUYÊN THẤU PHẢN HỒI",
            IconCode = "BM",
            ThemeColor = new Color(0.35f, 0.95f, 0.65f, 1f),
            DescriptionEn = "Aerodynamic cross-blade piercing all enemies in its path, then returning along the player's movement trajectory.",
            DescriptionVi = "Phi tiêu khí động học bay xuyên thấu quái vật theo đường thẳng rồi quay về đón đầu hướng di chuyển của người ném.",
            ParamLabelsEn = new string[] { "BASE DAMAGE", "COOLDOWN", "HITBOX RADIUS", "COUNT / SPEED" },
            ParamValuesEn = new string[] { $"{bmDmg1:0} - {bmDmg4:0} DMG", $"{bmCd1:0.0}s - {bmCd4:0.0}s", $"{bmRad1:0.00}m - {bmRad4:0.00}m", $"{bmCount1} - {bmCount4} ({bmSpd:0.0} m/s)" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG", "HỒI CHIÊU", "BÁN KÍNH HITBOX", "SỐ LƯỢNG / TỐC ĐỘ" },
            ParamValuesVi = new string[] { $"{bmDmg1:0} - {bmDmg4:0} DMG", $"{bmCd1:0.0}s - {bmCd4:0.0}s", $"{bmRad1:0.00}m - {bmRad4:0.00}m", $"{bmCount1} - {bmCount4} ({bmSpd:0.0} m/s)" },
            ProgressionSummaryEn = $"Lv1: {bmCount1} Blade (Catch -25% CD) • Lv2: Hitbox +55% • Lv3: 2 Blades • Lv4: Catch RESETS CD + Fires Empowered (+60% DMG)",
            ProgressionSummaryVi = $"Cấp 1: {bmCount1} Tiêu (Bắt -25% CD) • Cấp 2: Hitbox +55% • Cấp 3: 2 Tiêu • Cấp 4: Bắt dính Reset CD & Bắn Cường Hóa (+60% DMG)",
            MechanicsEn = "Does NOT auto-home. Returning vector predicts movement. Catching refunds cooldown and triggers rapid-fire bursts.",
            MechanicsVi = "Không tự động bám đuôi. Vector quay về đón đầu hướng di chuyển. Bắt dính giúp hồi chiêu và bắn liên hoàn.",
            TipsEn = "Move predictably to catch boomerangs and maintain continuous empowered uptime at Level 4.",
            TipsVi = "Di chuyển nhịp nhàng để hứng dính phi tiêu và duy trì trạng thái Cường Hóa liên tục ở Cấp 4."
        });

        var el = ElectricSkillConfigSO.Instance;
        float elDmg1 = el != null ? el.GetDamage(1) : 60f;
        float elDmg4 = el != null ? el.GetDamage(4) : 120f;
        float elCd1 = el != null ? el.GetCooldown(1) : 5.4f;
        float elCd4 = el != null ? el.GetCooldown(4) : 4.37f;
        int elChain1 = el != null ? el.GetChainCount(1) : 1;
        int elChain4 = el != null ? el.GetChainCount(4) : 4;
        int elPri1 = el != null ? el.GetPrimaryCount(1) : 1;
        int elPri4 = el != null ? el.GetPrimaryCount(4) : 2;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_electric",
            Category = EntryCategory.Skills,
            TitleEn = "Chain Lightning",
            TitleVi = "Lôi Điện Liên Hoàn",
            SubtitleEn = "High-Voltage Arc & Stun Chain",
            SubtitleVi = "Hồ Quang Cao Thế & Choáng Dòng Điện",
            BadgeEn = "CHAIN • STUN CONTROL",
            BadgeVi = "LAN TRUYỀN GÂY CHOÁNG",
            IconCode = "EL",
            ThemeColor = new Color(0.4f, 0.75f, 1f, 1f),
            DescriptionEn = "Summons devastating lightning arcs striking the nearest enemy and violently chaining to adjacent foes.",
            DescriptionVi = "Triệu hồi sấm sét giáng xuống quái vật gần nhất rồi phóng luồng hồ quang chuyền sang các mục tiêu lân cận.",
            ParamLabelsEn = new string[] { "PRIMARY DAMAGE", "COOLDOWN", "CHAIN JUMPS", "PRIMARY BOLTS" },
            ParamValuesEn = new string[] { $"{elDmg1:0} - {elDmg4:0} DMG", $"{elCd1:0.0}s - {elCd4:0.0}s", $"{elChain1} - {elChain4} Jumps (30% DMG)", $"{elPri1} - {elPri4} Bolts" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG GỐC", "HỒI CHIÊU", "SỐ LẦN LAN", "SỐ TIA SÉT CHA" },
            ParamValuesVi = new string[] { $"{elDmg1:0} - {elDmg4:0} DMG", $"{elCd1:0.0}s - {elCd4:0.0}s", $"{elChain1} - {elChain4} Lần (30% DMG)", $"{elPri1} - {elPri4} Tia độc lập" },
            ProgressionSummaryEn = $"Lv1: 1 Chain Jump • Lv2: 2 Jumps (-10% CD) • Lv3: Dual Thunderbolts (2 Primary) • Lv4: 4 Jumps + Stun (0.5s) + Bounces Back",
            ProgressionSummaryVi = $"Cấp 1: 1 Lần lan • Cấp 2: 2 Lần (-10% CD) • Cấp 3: Song Lôi (2 Tia cha) • Cấp 4: 4 Lần lan + Gây Choáng (0.5s) + Nảy ngược",
            MechanicsEn = "Fractal branching arcs. Level 4 allows lightning to bounce back to previous enemies, decimating small elite packs.",
            MechanicsVi = "Tia sét fractal phân nhánh. Cấp 4 cho phép tia sét nảy ngược lại quái cũ, dồn sát thương khổng lồ vào quái tinh anh.",
            TipsEn = "At Level 4, grouping 2 elites or a boss causes lightning to bounce back and forth, melting them in seconds.",
            TipsVi = "Ở Cấp 4, lùa 2 quái tinh anh hoặc quái cùng Boss lại gần nhau sẽ khiến sét nảy liên hoàn nghiền nát chúng."
        });

        var pb = PetalBurstSkillConfigSO.Instance;
        float pbDmg1 = pb != null ? pb.GetDamage(1) : 22f;
        float pbDmg4 = pb != null ? pb.GetDamage(4) : 70f;
        float pbCd1 = pb != null ? pb.GetCooldown(1) : 3.5f;
        float pbCd4 = pb != null ? pb.GetCooldown(4) : 2.2f;
        int pbCount1 = pb != null ? pb.GetCount(1) : 6;
        int pbCount4 = pb != null ? pb.GetCount(4) : 18;
        float pbExpRad1 = pb != null ? pb.GetExplosionRadius(1) : 0.85f;
        float pbExpRad4 = pb != null ? pb.GetExplosionRadius(4) : 1.35f;

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_petalburst",
            Category = EntryCategory.Skills,
            TitleEn = "Fairy Petal Burst",
            TitleVi = "Tiên Nữ Tán Hoa",
            SubtitleEn = "360° Petal Scatter & Vacuum Implosion",
            SubtitleVi = "Bung Tỏa 360° & Hoa Trận Hút Chân Không",
            BadgeEn = "BURST • VACUUM AOE",
            BadgeVi = "BUNG TỎA • HÚT QUÁI AOE",
            IconCode = "FB",
            ThemeColor = new Color(1.0f, 0.42f, 0.72f, 1f),
            DescriptionEn = "Scatters celestial fairy petals in a 360° ring around the hero. Petals rapidly decelerate and detonate upon enemy contact or reaching maximum flight duration.",
            DescriptionVi = "Bung tỏa các đóa tiên hoa xoay tròn 360 độ quanh nhân vật. Hoa bay chậm dần do lực cản và phát nổ khi chạm quái hoặc hết thời gian bay.",
            ParamLabelsEn = new string[] { "EXPLOSION DMG", "COOLDOWN", "PETAL COUNT", "BLAST RADIUS" },
            ParamValuesEn = new string[] { $"{pbDmg1:0} - {pbDmg4:0} DMG", $"{pbCd1:0.0}s - {pbCd4:0.0}s", $"{pbCount1} - {pbCount4} Petals", $"{pbExpRad1:0.0}m - {pbExpRad4:0.0}m" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG NỔ", "HỒI CHIÊU", "SỐ LƯỢNG HOA", "BÁN KÍNH NỔ" },
            ParamValuesVi = new string[] { $"{pbDmg1:0} - {pbDmg4:0} DMG", $"{pbCd1:0.0}s - {pbCd4:0.0}s", $"{pbCount1} - {pbCount4} Hoa", $"{pbExpRad1:0.0}m - {pbExpRad4:0.0}m" },
            ProgressionSummaryEn = $"Lv1: {pbCount1} Petals • Lv2: 9 Petals (+45% DMG) • Lv3: 13 Petals (-15% CD) • Lv4: 18 Petals + MINI VACUUM VORTEX (Pulls all nearby enemies into blast center)",
            ProgressionSummaryVi = $"Cấp 1: {pbCount1} Hoa • Cấp 2: 9 Hoa (+45% DMG) • Cấp 3: 13 Hoa (-15% CD) • Cấp 4: 18 Hoa + LỰC HÚT CHÂN KHÔNG (Gom toàn bộ quái vật vào tâm nổ)",
            MechanicsEn = "Petals launch with high initial velocity then decelerate rapidly, creating a tight protective defensive perimeter around the player.",
            MechanicsVi = "Hoa phóng ra với vận tốc ban đầu rất cao rồi giảm tốc nhanh chóng, tạo thành vành đai bảo hộ phòng thủ hoàn hảo quanh nhân vật.",
            TipsEn = "At Level 4, dive slightly towards dense swarms to vacuum-cluster enemies for massive collateral damage.",
            TipsVi = "Ở Cấp 4, hãy áp sát đám đông quái vật để lực hút chân không gom chúng lại một điểm dồn sát thương cực đại."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_giant_fusion",
            Category = EntryCategory.Skills,
            TitleEn = "Celestial Giant Sword",
            TitleVi = "Cự Kiếm Hoàng Kim Hợp Thể",
            SubtitleEn = "Ultimate Fusion Skill (Flying + Slash)",
            SubtitleVi = "Kỹ Năng Hợp Thể (Phi Kiếm + Trảm Kiếm)",
            BadgeEn = "FUSION • SCREEN WIPE",
            BadgeVi = "HỢP THỂ TỐI THƯỢNG",
            IconCode = "GF",
            ThemeColor = new Color(1f, 0.85f, 0.2f, 1f),
            DescriptionEn = "A colossal celestial golden blade dropping from the heavens, crushing all below with devastating shockwaves.",
            DescriptionVi = "Thanh kiếm khổng lồ màu vàng kim từ trời cao cắm thẳng xuống đất, chấn động trời đất quét sạch chiến trường.",
            ParamLabelsEn = new string[] { "IMPACT DAMAGE", "PERIODIC CD", "SHOCKWAVE RADIUS", "RADIANT WAVES" },
            ParamValuesEn = new string[] { "220 AOE DMG", "4.95s Drop CD", "4.5m Radius", "5 Waves (75 DMG / 72°)" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG CẮM", "CHU KỲ RƠI", "BÁN KÍNH SÓNG", "KIẾM KHÍ TỎA" },
            ParamValuesVi = new string[] { "220 AOE DMG", "Mỗi 4.95 giây", "Bán kính 4.5m", "5 Luồng (75 DMG / 72°)" },
            ProgressionSummaryEn = "FUSION RECIPE: Flying Sword Lv4 + Slash Sword Lv4 (Acquired via Special Perk Card)",
            ProgressionSummaryVi = "CÔNG THỨC HỢP THỂ: Flying Sword Cấp 4 + Slash Sword Cấp 4 (Nhận qua Thẻ Bài Đặc Biệt)",
            MechanicsEn = "Targets the densest horde cluster. Impact shockwave eradicates bullets and fires 5 piercing waves outward.",
            MechanicsVi = "Nhắm vào cụm quái đông đảo nhất. Sóng chấn động xóa sạch đạn địch và bắn tỏa 5 luồng kiếm khí xuyên thấu.",
            TipsEn = "Select the Fusion Perk card as soon as both swords reach Level 4 to unlock god-tier board control.",
            TipsVi = "Hãy chọn thẻ bài Hợp Thể ngay khi cả hai kỹ năng kiếm đạt Cấp 4 để kiểm soát toàn bộ màn hình."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "skill_thunder_boomerang",
            Category = EntryCategory.Skills,
            TitleEn = "Thunder Boomerang",
            TitleVi = "Lôi Tiêu Thiên Kiếp",
            SubtitleEn = "Ultimate Fusion Skill (Boomerang + Electric)",
            SubtitleVi = "Kỹ Năng Hợp Thể (Phi Tiêu + Lôi Điện)",
            BadgeEn = "FUSION • STORM CONDUIT",
            BadgeVi = "HỢP THỂ LÔI BÃO",
            IconCode = "TB",
            ThemeColor = new Color(0.3f, 0.95f, 1f, 1f),
            DescriptionEn = "Infuses aerodynamic boomerangs with celestial lightning, transforming each into a mobile lightning storm conduit.",
            DescriptionVi = "Ngưng tụ sấm sét cửu thiên vào phi tiêu hồi toàn, biến mỗi chiếc phi tiêu thành một trạm phóng lôi điện di động quét sạch chiến trường.",
            ParamLabelsEn = new string[] { "BOLT DAMAGE", "DISCHARGE RATE", "CHAIN JUMPS", "STUN EFFECT" },
            ParamValuesEn = new string[] { "90 DMG (+30% Chain)", "2 Waves (0.5s & 1.5s)", "4 Jumps per Bolt", "0.5s Stun on All Foes" },
            ParamLabelsVi = new string[] { "SÁT THƯƠNG SÉT", "TẦN SUẤT PHÓNG", "SỐ LẦN LAN", "HIỆU ỨNG CHOÁNG" },
            ParamValuesVi = new string[] { "90 DMG (+30% Lan)", "2 Đợt (0.5s & 1.5s)", "4 Lần mỗi tia", "Choáng 0.5s toàn quái" },
            ProgressionSummaryEn = "FUSION RECIPE: Boomerang Lv4 + Electric Lv4 (Acquired via Special Perk Card)",
            ProgressionSummaryVi = "CÔNG THỨC HỢP THỂ: Boomerang Cấp 4 + Electric Cấp 4 (Nhận qua Thẻ Bài Đặc Biệt)",
            MechanicsEn = "Each boomerang discharges 2 chain lightning bolts at the apex and return journey, stunning everything hit.",
            MechanicsVi = "Mỗi phi tiêu phóng 2 tia sét tại điểm xa nhất và trên đường bay về, làm choáng toàn bộ quái vật trúng đòn.",
            TipsEn = "Catch returning boomerangs to rapidly cycle lightning storms across the map.",
            TipsVi = "Tích cực bắt dính phi tiêu để liên tục triệu hồi các cơn bão sấm sét bao phủ khắp bản đồ."
        });

        // =========================================================================
        // 3. CỔ VẬT BỊ ĐỘNG (RELICS)
        // =========================================================================
        _allEntries.Add(new CodexEntry
        {
            Id = "relic_mirror",
            Category = EntryCategory.Relics,
            TitleEn = "Mirror of Illusion",
            TitleVi = "Kính Huyễn Ảo (Gương Vô Cực)",
            SubtitleEn = "Legendary Relic: Astral Clones",
            SubtitleVi = "Cổ Vật Huyền Thoại: Phân Thân Ảo Ảnh",
            BadgeEn = "TIER: LEGENDARY",
            BadgeVi = "CỔ VẬT HUYỀN THOẠI",
            IconCode = "MR",
            ThemeColor = new Color(0.4f, 0.85f, 1f, 1f),
            DescriptionEn = "An ancient artifact radiating astral energy. Summons two ethereal mirror clones flanking the player that mimic all movements and cast all weapons simultaneously.",
            DescriptionVi = "Bảo vật thượng cổ tỏa hào quang hư ảo. Lập tức triệu hồi 2 Phân Thân Ảo Ảnh đứng 2 bên sườn, sao chép mọi chuyển động và cùng thi triển toàn bộ kỹ năng của người chơi.",
            ParamLabelsEn = new string[] { "CLONES SUMMONED", "CLONE DAMAGE", "CLONE COOLDOWN", "FORMATION SPACING" },
            ParamValuesEn = new string[] { "2 Astral Clones", "25% of Player DMG", "150% (1.5x CD)", "+-5.2m Flanks" },
            ParamLabelsVi = new string[] { "SỐ PHÂN THÂN", "SÁT THƯƠNG CLONE", "HỒI CHIÊU CLONE", "KHOẢNG CÁCH SƯỜN" },
            ParamValuesVi = new string[] { "2 Ảo Ảnh Song Hành", "25% Sát thương gốc", "150% (1.5x CD)", "+-5.2m Hai bên sườn" },
            ProgressionSummaryEn = "LEGENDARY TIER: Extremely rare drop from Ancient Chests only • Clones inherit all evolutions and shields",
            ProgressionSummaryVi = "BẬC HUYỀN THOẠI: Cực hiếm chỉ rơi từ Rương Cổ Vật • Phân thân thừa hưởng toàn bộ kỹ năng và khiên",
            MechanicsEn = "Clones automatically inherit ALL player weapons and evolutions (Zero configuration). Flanks at +-5.2m to ensure independent sword orbits and projectile paths.",
            MechanicsVi = "Tự động đồng bộ 100% kỹ năng và Hợp thể tối thượng. Đứng cách sườn +-5.2m để kiếm bay và phi tiêu vận hành hoàn toàn độc lập không bị dính chùm.",
            TipsEn = "Extremely rare drop from Ancient Chests only. Increases total damage coverage by +50% and blankets the battlefield in projectiles.",
            TipsVi = "Vật phẩm cực hiếm chỉ có thể nhận từ Rương Cổ Vật. Gia tăng 50% tổng hỏa lực và phủ kín chiến trường bằng biển đạn đạo."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "relic_boots",
            Category = EntryCategory.Relics,
            TitleEn = "Windrunner Boots",
            TitleVi = "Thần Hành Hài (Giày Gió)",
            SubtitleEn = "Passive Relic: Mobility",
            SubtitleVi = "Cổ Vật: Tốc Độ Di Chuyển",
            BadgeEn = "PASSIVE • MOBILITY",
            BadgeVi = "CỔ VẬT TỐC CHẠY",
            IconCode = "WB",
            ThemeColor = new Color(0.45f, 0.85f, 1f, 1f),
            DescriptionEn = "Boots woven from ethereal clouds, lightening the user's steps.",
            DescriptionVi = "Đôi giày bện từ mây ngũ sắc, gia tăng tốc độ lướt đi của nhân vật.",
            ParamLabelsEn = new string[] { "SPEED PER LEVEL", "MAX LEVEL", "MAX BONUS", "SYNERGY" },
            ParamValuesEn = new string[] { "+15% Move Speed", "Level 5", "+75% Total Speed", "Slash Sword Cleave" },
            ParamLabelsVi = new string[] { "TĂNG MỖI CẤP", "CẤP TỐI ĐA", "TỔNG CHỈ SỐ", "TƯƠNG THÍCH" },
            ParamValuesVi = new string[] { "+15% Tốc chạy", "Cấp 5", "+75% Tốc độ chạy", "Trảm Kiếm Bán Nguyệt" },
            ProgressionSummaryEn = "Lv1: +15% Speed • Lv2: +30% • Lv3: +45% • Lv4: +60% • Lv5: +75% Total Movement Speed",
            ProgressionSummaryVi = "Cấp 1: +15% Tốc • Cấp 2: +30% • Cấp 3: +45% • Cấp 4: +60% • Cấp 5: +75% Tốc độ di chuyển",
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
            BadgeEn = "PASSIVE • COLLECTION",
            BadgeVi = "CỔ VẬT THU THẬP",
            IconCode = "SM",
            ThemeColor = new Color(0.4f, 0.95f, 0.7f, 1f),
            DescriptionEn = "A jade orb that resonates with celestial crystals and draws them from afar.",
            DescriptionVi = "Viên ngọc bích phát ra từ trường hút mọi ngọc kinh nghiệm và vật phẩm rơi trên đất.",
            ParamLabelsEn = new string[] { "RANGE PER LEVEL", "MAX LEVEL", "MAX BONUS", "SYNERGY" },
            ParamValuesEn = new string[] { "+50% Magnet Range", "Level 5", "+250% Total Range", "Fast Leveling" },
            ParamLabelsVi = new string[] { "TĂNG MỖI CẤP", "CẤP TỐI ĐA", "TỔNG CHỈ SỐ", "TƯƠNG THÍCH" },
            ParamValuesVi = new string[] { "+50% Tầm hút", "Cấp 5", "+250% Tầm hút", "Lên cấp thần tốc" },
            ProgressionSummaryEn = "Lv1: +50% Range • Lv2: +100% • Lv3: +150% • Lv4: +200% • Lv5: +250% Total Magnet Radius",
            ProgressionSummaryVi = "Cấp 1: +50% Tầm • Cấp 2: +100% • Cấp 3: +150% • Cấp 4: +200% • Cấp 5: +250% Bán kính hút ngọc",
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
            BadgeEn = "PASSIVE • VITALITY",
            BadgeVi = "CỔ VẬT SINH MỆNH",
            IconCode = "CH",
            ThemeColor = new Color(1f, 0.3f, 0.45f, 1f),
            DescriptionEn = "A pulsing crimson crystal granting boundless vigor and instant vitality.",
            DescriptionVi = "Khối pha lê đỏ rực đập theo nhịp tim, ban phát sinh lực dồi dào và hồi phục tức thời.",
            ParamLabelsEn = new string[] { "HP PER LEVEL", "INSTANT HEAL", "MAX LEVEL", "MAX BONUS" },
            ParamValuesEn = new string[] { "+30 Max HP", "100% Full Heal", "Level 5", "+150 Max HP" },
            ParamLabelsVi = new string[] { "MÁU MỖI CẤP", "HỒI MÁU TỨC THÌ", "CẤP TỐI ĐA", "TỔNG CHỈ SỐ" },
            ParamValuesVi = new string[] { "+30 Máu tối đa", "Hồi đầy 100% HP", "Cấp 5", "+150 Máu tối đa" },
            ProgressionSummaryEn = "Lv1: +30 HP (Full Heal) • Lv2: +60 HP • Lv3: +90 HP • Lv4: +120 HP • Lv5: +150 Max HP",
            ProgressionSummaryVi = "Cấp 1: +30 HP (Hồi đầy) • Cấp 2: +60 HP • Cấp 3: +90 HP • Cấp 4: +120 HP • Cấp 5: +150 Max HP",
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
            BadgeEn = "PASSIVE • DAMAGE MULT",
            BadgeVi = "CỔ VẬT UY LỰC",
            IconCode = "AT",
            ThemeColor = new Color(1f, 0.75f, 0.2f, 1f),
            DescriptionEn = "An inscribed talisman boosting the sharp edge of all physical and spiritual blades.",
            DescriptionVi = "Lá bùa cổ khắc ấn chú kiếm đạo, gia tăng sức công phá cho toàn bộ đòn đánh.",
            ParamLabelsEn = new string[] { "DMG PER LEVEL", "MAX LEVEL", "MAX BONUS", "AFFECTS" },
            ParamValuesEn = new string[] { "+15% Global DMG", "Level 5", "+75% Total DMG", "All Weapons & Fusions" },
            ParamLabelsVi = new string[] { "TĂNG MỖI CẤP", "CẤP TỐI ĐA", "TỔNG CHỈ SỐ", "PHẠM VI TÁC DỤNG" },
            ParamValuesVi = new string[] { "+15% Toàn bộ DMG", "Cấp 5", "+75% Tổng sát thương", "Tất cả vũ khí & Hợp thể" },
            ProgressionSummaryEn = "Lv1: +15% DMG • Lv2: +30% • Lv3: +45% • Lv4: +60% • Lv5: +75% Global Damage Multiplier",
            ProgressionSummaryVi = "Cấp 1: +15% DMG • Cấp 2: +30% • Cấp 3: +45% • Cấp 4: +60% • Cấp 5: +75% Hệ số sát thương",
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
            BadgeEn = "PASSIVE • FORTUNE",
            BadgeVi = "CỔ VẬT VẬN MỆNH",
            IconCode = "FD",
            ThemeColor = new Color(1f, 0.85f, 0.4f, 1f),
            DescriptionEn = "A die carved from meteor stone that bends probability in the wielder's favor.",
            DescriptionVi = "Viên xúc xắc tạc từ thiên thạch, xoay chuyển vận may ban tặng thêm lượt chọn lại bài.",
            ParamLabelsEn = new string[] { "REROLLS / LEVEL", "LUCK BONUS", "MAX LEVEL", "MAX REROLLS" },
            ParamValuesEn = new string[] { "+2 Free Rerolls", "+10% High-grade drop", "Level 5", "+10 Extra Rerolls" },
            ParamLabelsVi = new string[] { "ĐỔI BÀI / CẤP", "TĂNG VẬN MAY", "CẤP TỐI ĐA", "TỔNG SỐ LẦN ĐỔI" },
            ParamValuesVi = new string[] { "+2 Lượt Reroll", "+10% Rớt đồ quý", "Cấp 5", "+10 Lượt đổi bài" },
            ProgressionSummaryEn = "Lv1: +2 Rerolls • Lv2: +4 • Lv3: +6 • Lv4: +8 • Lv5: +10 Free Rerolls + 50% Rare Drop Luck",
            ProgressionSummaryVi = "Cấp 1: +2 Lượt • Cấp 2: +4 • Cấp 3: +6 • Cấp 4: +8 • Cấp 5: +10 Lượt Reroll + 50% May mắn",
            MechanicsEn = "Allows rerolling bad level-up options to sculpt the exact build you desire.",
            MechanicsVi = "Cho phép đổi những lựa chọn kỹ năng không mong muốn để hoàn thiện build chuẩn xác.",
            TipsEn = "Take at least 1 level early to guarantee finding your core weapon combinations.",
            TipsVi = "Nên lấy ít nhất 1 cấp ở đầu trận để dễ dàng tìm kiếm những mảnh ghép kỹ năng cần thiết."
        });

        _allEntries.Add(new CodexEntry
        {
            Id = "relic_tome",
            Category = EntryCategory.Relics,
            TitleEn = "Tome of Wisdom",
            TitleVi = "Thiên Thư Bí Pháp",
            SubtitleEn = "Passive Relic: Cultivation & EXP",
            SubtitleVi = "Cổ Vật: Tăng Tốc Tu Vi",
            BadgeEn = "PASSIVE • WISDOM",
            BadgeVi = "CỔ VẬT TRI THỨC",
            IconCode = "TM",
            ThemeColor = new Color(0.35f, 0.85f, 1f, 1f),
            DescriptionEn = "An ancient scripture recording the essence of heavenly cultivation, allowing the reader to absorb spiritual energy faster.",
            DescriptionVi = "Quyển cổ thư ghi chép chân kinh tu tiên, giúp nhân vật ngộ đạo và hấp thu linh khí từ ngọc kinh nghiệm nhanh chóng hơn.",
            ParamLabelsEn = new string[] { "EXP / LEVEL", "MAX LEVEL", "MAX BONUS", "AFFECTS" },
            ParamValuesEn = new string[] { "+15% EXP Gain", "Level 5", "+75% Total EXP", "All Experience Orbs" },
            ParamLabelsVi = new string[] { "EXP / CẤP", "CẤP TỐI ĐA", "TỔNG CHỈ SỐ", "PHẠM VI TÁC DỤNG" },
            ParamValuesVi = new string[] { "+15% EXP nhận", "Cấp 5", "+75% Tổng EXP", "Tất cả ngọc kinh nghiệm" },
            ProgressionSummaryEn = "Lv1: +15% EXP • Lv2: +30% • Lv3: +45% • Lv4: +60% • Lv5: +75% Experience Value",
            ProgressionSummaryVi = "Cấp 1: +15% EXP • Cấp 2: +30% • Cấp 3: +45% • Cấp 4: +60% • Cấp 5: +75% Giá trị ngọc",
            MechanicsEn = "Directly multiplies the amount of EXP absorbed from every fallen enemy orb.",
            MechanicsVi = "Nhân trực tiếp giá trị EXP nhận được khi hấp thu từng viên ngọc rơi từ quái vật.",
            TipsEn = "Equip early to snowball your character level and rapidly unlock weapon evolutions.",
            TipsVi = "Thu thập sớm giúp tạo hiệu ứng quả cầu tuyết về cấp độ và nhanh chóng mở khóa hợp thể vũ khí."
        });
    }
}
