using System;
using UnityEngine;

/// <summary>
/// Các ngôn ngữ được hỗ trợ trong Project Z - Heaven Bullet.
/// Mặc định: English (Tiếng Anh). Hỗ trợ chuyển đổi song ngữ English / Vietnamese.
/// </summary>
public enum GameLanguage
{
    English = 0,
    Vietnamese = 1
}

/// <summary>
/// Hệ thống bản địa hóa (Localization) quản lý toàn bộ chuỗi văn bản trong game.
/// Cho phép đổi ngôn ngữ tức thời tại runtime và tự động lưu cấu hình vào PlayerPrefs.
/// </summary>
public static class GameLocalization
{
    private const string PREF_KEY = "ProjectZ_Language";
    private static GameLanguage _currentLanguage = GameLanguage.English;
    private static bool _initialized = false;

    public static event Action OnLanguageChanged;

    public static GameLanguage CurrentLanguage
    {
        get
        {
            if (!_initialized) Init();
            return _currentLanguage;
        }
        set
        {
            if (!_initialized) Init();
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                PlayerPrefs.SetInt(PREF_KEY, (int)_currentLanguage);
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke();
            }
        }
    }

    private static void Init()
    {
        // Mặc định là Tiếng Anh (English = 0)
        int saved = PlayerPrefs.GetInt(PREF_KEY, (int)GameLanguage.English);
        _currentLanguage = (saved == (int)GameLanguage.Vietnamese) ? GameLanguage.Vietnamese : GameLanguage.English;
        _initialized = true;
    }

    public static void ToggleLanguage()
    {
        CurrentLanguage = (CurrentLanguage == GameLanguage.English)
            ? GameLanguage.Vietnamese
            : GameLanguage.English;
    }

    public static string Get(string enText, string viText)
    {
        return CurrentLanguage == GameLanguage.Vietnamese ? viText : enText;
    }

    // --- UI Header & Labels ---
    public static string LevelUpHeader => Get("✦ LEVEL UP ✦", "✦ CẤP ĐỘ TIẾP THEO ✦");
    public static string LevelUpSubtitle => Get("Choose an upgrade to enhance your power", "Chọn một nâng cấp sức mạnh để tiếp tục chiến đấu");
    public static string SelectButtonText => Get("SELECT", "CHỌN NÂNG CẤP");
    public static string LangButtonText => Get("🌐 EN", "🌐 VI");
    public static string LangHintText => Get("[L] Language", "[L] Ngôn ngữ");
    public static string UltimateReadyText => Get("✦ ULTIMATE READY ✦", "✦ TUYỆT KỸ SẴN SÀNG ✦");
    public static string UltimateCooldownText(float timer) => Get($"⚔ ULTIMATE: {timer:0.0}s", $"⚔ TUYỆT KỸ: {timer:0.0}s");
    public static string RerollButtonText(int count) => Get($"🎲 REROLL ({count}) [R]", $"🎲 ĐỔI THẺ ({count}) [R]");
    public static string RerollEmptyText => Get("🎲 NO REROLLS", "🎲 HẾT LƯỢT ĐỔI");

    // --- Pause & Settings Menu ---
    public static string PauseHeader => Get("✦ GAME PAUSED ✦", "✦ TẠM DỪNG TRẬN ĐẤU ✦");
    public static string ResumeButton => Get("▶ RESUME [ESC]", "▶ TIẾP TỤC [ESC]");
    public static string RestartButton => Get("🔄 RESTART", "🔄 CHƠI LẠI");
    public static string DevModeButton => Get("🛠️ DEVELOPER MODE [F1]", "🛠️ CHẾ ĐỘ DEVELOPER [F1]");
    public static string CloseButton => Get("✕ CLOSE", "✕ ĐÓNG");

    // --- Developer Control Panel ---
    public static string DevPanelHeader => Get("🛠️ DEVELOPER CONTROL PANEL [F1]", "🛠️ BẢNG ĐIỀU KHIỂN DEVELOPER [F1]");
    public static string DevSkillSection => Get("— SKILL LEVEL EDITOR —", "— ĐIỀU CHỈNH CẤP KỸ NĂNG —");
    public static string DevCheatSection => Get("— UTILITY CHEATS —", "— CÔNG CỤ HỖ TRỢ TEST —");
    public static string DevMaxAllSkills => Get("⚡ MAX ALL SKILLS", "⚡ MAX CẤP TOÀN BỘ");
    public static string DevHealFull => Get("❤️ FULL HP", "❤️ HỒI ĐẦY MÁU");
    public static string DevGodMode(bool on) => Get($"🛡️ GOD MODE: {(on ? "ON" : "OFF")}", $"🛡️ BẤT TỬ: {(on ? "BẬT" : "TẮT")}");
    public static string DevLevelUp => Get("⭐ +1 LEVEL", "⭐ LÊN 1 CẤP");
    public static string DevKillAll => Get("💀 KILL ALL ENEMIES", "💀 QUÉT SẠCH QUÁI");
    public static string DevResetReroll => Get("🎲 3 REROLLS", "🎲 HỒI 3 LƯỢT ĐỔI");
    public static string DevSpeedBoost(bool on) => Get($"🏃 SPEED x2: {(on ? "ON" : "OFF")}", $"🏃 TỐC ĐỘ x2: {(on ? "BẬT" : "TẮT")}");
    public static string DevAddRegen => Get("💚 +0.5 HP/s REGEN", "💚 +0.5 HP/s HỒI MÁU");

    // --- Perk Titles ---
    public static string FlyingSwordTitle => Get("FLYING SWORD", "PHI KIẾM HỘ THỂ");
    public static string SlashSwordTitle => Get("CRESCENT BLADE", "TRẢM YÊU KIẾM");
    public static string SwordFusionTitle => Get("CELESTIAL BLADE FUSION", "THIÊN KIẾM HỢP THỂ");
    public static string ThunderBoomerangTitle => Get("THUNDER BOOMERANG", "LÔI TIÊU THIÊN KIẾP");
    public static string ProjectileSkillTitle => Get("ARCANE VOLLEY", "ĐẠN TỎA QUẠT");
    public static string FireballTitle => Get("HELLFIRE ORB", "CẦU LỬA VIÊM MA");
    public static string ShieldTitle => Get("AEGIS SHIELD", "KHIÊN HỘ THỂ");
    public static string BoomerangTitle => Get("CELESTIAL BOOMERANG", "PHI TIÊU HỒI TOÀN");
    public static string ElectricTitle => Get("CHAIN LIGHTNING", "LÔI ĐIỆN LIÊN HOÀN");
    public static string HealTitle => Get("VITALITY ELIXIR", "TIÊN DƯỢC PHỤC HỒI");
    public static string HpRegenTitle => Get("REJUVENATION", "HỒI XUÂN THUẬT");
    public static string MaxHpTitle => Get("IRON WILL", "CƯƠNG THỂ QUYẾT");
    public static string MoveSpeedTitle => Get("WIND STRIDE", "THẦN HÀNH BỘ");
    public static string PickupRangeTitle => Get("MAGNETIC AURA", "HÚT LINH THẠCH");

    // --- Perk Badges ---
    public static string LevelBadge(int level, bool isUltimate = false)
    {
        if (isUltimate)
            return Get($"LV.{level} [ULTIMATE]", $"CẤP {level} [ULTIMATE]");
        return Get($"LEVEL {level}", $"CẤP {level}");
    }

    public static string SwordFusionBadge => Get("✦ FUSION ✦", "✦ HỢP THỂ ✦");
    public static string RecoveryBadge => Get("RECOVERY", "HỒI PHỤC");
    public static string UpgradeBadge => Get("UPGRADE", "NÂNG CẤP");
    public static string UtilityBadge => Get("UTILITY", "TIỆN ÍCH");

    public static string SwordFusionDesc => Get(
        "★ CELESTIAL FUSION ★ Merges Flying Sword & Crescent Blade! Retains full powers of both, plus periodically drops a COLOSSAL CELESTIAL SWORD for 350 AOE DMG that bursts into 5 Piercing Sword Waves!",
        "★ THIÊN KIẾM HỢP THỂ ★ Kết hợp Phi Kiếm & Trảm Yêu Kiếm! Giữ trọn vẹn sức mạnh cả hai, định kỳ giáng KIẾM KHỔNG LỒ cắm đất gây 350 sát thương nổ AOE và phóng tỏa 5 luồng Kiếm Khí xuyên thấu!"
    );

    public static string ThunderBoomerangDesc => Get(
        "★ THUNDER FUSION ★ Merges Boomerang & Chain Lightning! Every Boomerang launched discharges 2 Chain Lightning bolts twice during flight (at 0.5s and 1.5s), shocking and stunning nearby foes for 0.5s!",
        "★ LÔI TIÊU HỢP THỂ ★ Kết hợp Phi Tiêu & Lôi Điện! Mỗi khi Boomerang được phóng ra, nó sẽ phóng 2 tia sét lan truyền tối đa 2 lần trong hành trình bay (sau 0.5s và 1.5s), giật điện và làm choáng quái 0.5s!"
    );

    // --- Perk Descriptions ---
    public static string FlyingSwordDesc(int level)
    {
        var cfg = FlyingSwordSkillConfigSO.Instance;
        int count = cfg != null ? cfg.GetSwordCount(level) : level;
        float aoeRad = cfg != null ? cfg.AoeRadius : 3.5f;

        return level switch
        {
            2 => Get(
                $"+1 Flying Sword ({count} total). Increases flight speed and damage.",
                $"+1 Thanh Phi Kiếm hộ thể (tổng {count} kiếm), tăng tốc độ bay và sát thương."
            ),
            3 => Get(
                $"+1 Flying Sword ({count} swords total). Expands outer engagement range.",
                $"+1 Thanh Phi Kiếm (tổng {count} kiếm), mở rộng tầm xuất chiến quanh Player."
            ),
            4 => Get(
                $"★ ULTIMATE STRIKE ★ Periodically dives into dense enemy swarms, triggering a massive {aoeRad:0.0}m AOE blast and DESTROYING 100% of enemy bullets!",
                $"★ ĐÒN ULTIMATE ★ Tự động lao vào cụm quái đông nhất, nổ AOE cực đại {aoeRad:0.0}m và TIÊU HỦY 100% đạn địch!"
            ),
            _ => Get(
                "Summons ethereal flying swords that patrol and slice through nearby foes.",
                "Triệu hồi Phi Kiếm hộ thể bay lượn liên hoàn chém quái."
            )
        };
    }

    public static string SlashSwordDesc(int level)
    {
        var cfg = SlashSwordSkillConfigSO.Instance;
        float cd = cfg != null ? cfg.GetCooldown(level) : (level switch { 2 => 3.3f, 3 => 2.6f, 4 => 2.0f, _ => 4.0f });
        float rad = cfg != null ? cfg.GetSlashRadius(level) : (level switch { 2 => 3.55f, 3 => 3.9f, 4 => 4.25f, _ => 3.2f });
        int count = cfg != null ? cfg.GetSwordCount(level) : (level + 1);

        return level switch
        {
            2 => Get(
                $"+1 Sentinel Blade ({count} total). Reduces cooldown to {cd:0.0}s and expands slash AOE to {rad:0.00}m.",
                $"+1 Linh kiếm (tổng {count} kiếm). Giảm hồi chiêu xuống {cd:0.0}s và mở rộng tầm chém AOE lên {rad:0.00}m."
            ),
            3 => Get(
                $"+1 Sentinel Blade ({count} total). Reduces cooldown to {cd:0.0}s and expands slash AOE to {rad:0.00}m.",
                $"+1 Linh kiếm (tổng {count} kiếm). Giảm hồi chiêu xuống {cd:0.0}s và mở rộng tầm chém AOE lên {rad:0.00}m."
            ),
            4 => Get(
                $"★ SWORD AURA EVOLUTION ★ +1 Sentinel Blade ({count} total)! Cooldown {cd:0.0}s. All {count} blades slash with massive {rad:0.00}m AOE and unleash Piercing Sword Waves!",
                $"★ TIẾN HÓA KIẾM KHÍ ★ +1 Linh kiếm (tổng {count} kiếm)! Hồi chiêu {cd:0.0}s. Cả {count} kiếm chém AOE cực đại {rad:0.00}m và phóng Kiếm Khí xuyên thấu mọi mục tiêu!"
            ),
            _ => Get(
                $"Summons {count} sentinel blades that rush in to unleash lethal {rad:0.0}m arc slashes ({cd:0}s cooldown).",
                $"Triệu hồi {count} linh kiếm lao vào chém quét vòng cung diện rộng {rad:0.0}m (hồi chiêu {cd:0}s)."
            )
        };
    }

    public static string ProjectileSkillDesc(int level)
    {
        return Get(
            "+1 Projectile per volley, -12% cooldown reduction, and increased bullet damage.",
            "+1 Tia đạn bắn ra mỗi đợt, giảm 12% thời gian hồi chiêu và tăng sát thương đạn."
        );
    }

    public static string FireballDesc(int level)
    {
        var cfg = FireballSkillConfigSO.Instance;
        float cd2 = cfg != null ? cfg.GetCooldown(2) : 2.53f;
        float cd3 = cfg != null ? cfg.GetCooldown(3) : 2.20f;
        float dmg1 = cfg != null ? cfg.GetDamage(1) : 45f;
        float dmg4 = cfg != null ? cfg.GetDamage(4) : 120f;
        int sub2 = cfg != null ? cfg.GetSubFireballCount(2) : 2;
        int sub3 = cfg != null ? cfg.GetSubFireballCount(3) : 3;
        int sub4 = cfg != null ? cfg.GetSubFireballCount(4) : 4;

        return level switch
        {
            2 => Get(
                $"+45% Blast DMG. Bursts into {sub2} cluster sub-fireballs (50% DMG each). Cooldown reduced to {cd2:0.0}s.",
                $"+45% Sát thương nổ. Nổ chùm văng {sub2} cầu lửa con (50% sát thương mỗi quả). Hồi chiêu giảm còn {cd2:0.0}s."
            ),
            3 => Get(
                $"★ TWIN HELLFIRE ★ Fires +1 Main Fireball (2 Main Fireballs)! +40% Blast DMG & +15% Radius. Bursts into {sub3} cluster sub-fireballs. Cooldown {cd3:0.0}s.",
                $"★ SONG HỎA LIÊN HOÀN ★ Bắn thêm +1 Cầu Lửa Mẹ (Tổng 2 Cầu Lửa Mẹ)! +40% Sát thương nổ, nổ chùm {sub3} cầu lửa con. Hồi chiêu giảm còn {cd3:0.0}s."
            ),
            4 => Get(
                $"★ INFERNO CATACLYSM ★ 2 Main Fireballs with {dmg4:0} Blast DMG! Bursts into {sub4} cluster fireballs each and ignites Scorched Fire Zones dealing continuous DOT for 3.5s!",
                $"★ ĐỊA NGỤC HỎA DIỆM ★ 2 Cầu Lửa Mẹ với {dmg4:0} Sát thương nổ! Mỗi quả nổ chùm {sub4} cầu con và thắp sáng Vùng Lửa thiêu đốt DOT trong 3.5s!"
            ),
            _ => Get(
                $"Fires an explosive hellfire orb that detonates for {dmg1:0} AOE DMG and bursts into 1 mini cluster fireball (50% DMG).",
                $"Bắn cầu lửa Viêm Ma nổ diện rộng {dmg1:0} sát thương và nổ chùm văng 1 cầu lửa con (50% sát thương)."
            )
        };
    }

    public static string ShieldDesc(int level)
    {
        return level switch
        {
            2 => Get(
                "Reduces recharge cooldown from 30s down to 18s (Recharges 40% faster!).",
                "Giảm thời gian hồi khiên từ 30s xuống 18s (Hồi nhanh hơn 40%!)."
            ),
            3 => Get(
                "Dual Aegis! Can stack up to 2 Shields simultaneously. Each shield recharges independently (16s).",
                "Song Khiên Hộ Vệ! Có thể tích lũy tối đa 2 lớp khiên. Tự động hồi tuần tự từng lớp khiên (16s mỗi lớp)."
            ),
            4 => Get(
                "★ SHIELD SHATTER NOVA ★ Each shield now blocks 2 hits (Total 4 hits)! When destroyed, triggers a massive 100 DMG Shockwave that DESTROYS all enemy bullets!",
                "★ KHIÊN PHẢN MA TRẬN ★ Mỗi lớp khiên chịu được 2 lần sát thương (Tổng 4 hits)! Khi khiên bị phá vỡ, phát nổ sóng xung kích 100 DMG và TIÊU HỦY 100% đạn địch!"
            ),
            _ => Get(
                "Deploys an energetic barrier that absorbs 1 instance of damage completely. Recharges after 30s.",
                "Kích hoạt tấm khiên năng lượng chặn hoàn toàn 1 lần sát thương vào người chơi. Hồi lại sau 30s."
            )
        };
    }

    public static string BoomerangDesc(int level)
    {
        return level switch
        {
            2 => Get(
                "Expanded Hitbox & +57% Damage! Increases flight slice radius to 0.85m dealing 55 DMG both ways.",
                "Tăng mạnh Hitbox & +57% Sát thương! Mở rộng bán kính chém lên 0.85m gây 55 DMG cả lượt đi và lượt về."
            ),
            3 => Get(
                "★ TWIN BOOMERANGS ★ Fires +1 Boomerang (2 Total) with smart multi-target priority! Cooldown reduced from 8s to 6s. Each catch refunds 25% cooldown.",
                "★ SONG TIÊU LIÊN HOÀN ★ Bắn thêm +1 Phi Tiêu (Tổng 2 Phi Tiêu) ưu tiên chia đa mục tiêu! Hồi chiêu giảm xuống 6s. Bắt dính mỗi chiếc giảm 25% hồi chiêu."
            ),
            4 => Get(
                "★ EMPOWERED REBOUND ★ +45% Base DMG (80 DMG). SPECIAL: Successfully catching a boomerang immediately RESETS cooldown and instantly launches an EMPOWERED BOOMERANG with +60% BONUS DAMAGE!",
                "★ PHẢN HỒI NGUYÊN LỰC ★ Tăng sát thương (80 DMG). ĐẶC BIỆT: Bắt dính phi tiêu lập tức RESET HỒI CHIÊU và tự động phóng ngược lại 1 Phi Tiêu Cường Hóa gây thêm +60% SÁT THƯƠNG!"
            ),
            _ => Get(
                "Hurls a piercing boomerang that slices through foes and flies back towards your moving trajectory. Catching it reduces current cooldown by 25%! (8s cooldown, 35 DMG).",
                "Ném phi tiêu bay xuyên thấu quái vật rồi quay về đón đầu hướng di chuyển. Bắt dính phi tiêu giúp giảm 25% hồi chiêu hiện tại! (Hồi chiêu 8s, 35 DMG)."
            )
        };
    }

    public static string ElectricDesc(int level)
    {
        return level switch
        {
            2 => Get(
                "+50% Primary Bolt DMG (90 DMG) and +1 Chain Jump (2 Jumps Total). Cooldown reduced by 10% (5.4s).",
                "Tăng sát thương tia sét cha lên 90 DMG (+50%) và số lần lan sang mục tiêu +1 (Tổng 2 lần lan). Giảm 10% hồi chiêu (5.4s)."
            ),
            3 => Get(
                "★ DUAL THUNDERBOLTS ★ Fires +1 Primary Lightning Bolt (2 Bolts Total) hitting distinct targets! Each bolt chains 2 times. Cooldown reduced by 10% (4.86s).",
                "★ SONG LÔI CUỒNG NỘ ★ Bắn thêm +1 tia sét cha (Tổng 2 tia) vào 2 mục tiêu khác nhau! Mỗi tia lan 2 lần. Giảm 10% hồi chiêu (4.86s)."
            ),
            4 => Get(
                "★ ARC OVERLOAD ★ +2 Chain Jumps (4 Jumps Total). SPECIAL: Lightning can now BOUNCE BACK to previous targets, and all struck enemies are STUNNED for 0.5s!",
                "★ THIÊN LÔI QUÁ TẢI ★ Thêm +2 lần lan (Tổng 4 lần lan). ĐẶC BIỆT: Sét có thể LAN NGƯỢC VỀ MỤC TIÊU CŨ, và toàn bộ quái dính sét bị CHOÁNG 0.5s!"
            ),
            _ => Get(
                "Strikes a target for 60 DMG and chains to 1 nearby enemy dealing 30% damage (18 DMG). (6s Cooldown).",
                "Bắn ra 1 tia sét giáng vào mục tiêu gây 60 DMG và lan sang 1 quái kế cận gây 30% sát thương gốc (18 DMG). (Hồi chiêu 6s)."
            )
        };
    }

    public static string HealDesc => Get(
        "Instantly restores 40% of maximum HP to the hero.",
        "Lập tức hồi phục lại 40% lượng Máu tối đa (Max HP) cho nhân vật."
    );

    public static string HpRegenDesc => Get(
        "Permanently increases HP Regeneration by +0.5 HP/s.",
        "Tăng vĩnh viễn tốc độ Tự Hồi Máu thêm +0.5 HP/s trong suốt trận đấu."
    );

    public static string MaxHpDesc => Get(
        "Permanently increases Max HP by +30 and heals 30 HP immediately.",
        "Tăng vĩnh viễn +30 Máu tối đa (Max HP) và hồi lại 30 điểm Máu ngay lập tức."
    );

    public static string MoveSpeedDesc => Get(
        "+15% Movement Speed to dodge enemy projectiles and kite swarms.",
        "Tăng +15% Tốc độ di chuyển giúp né đạn và kiting quái vật dễ dàng hơn."
    );

    public static string PickupRangeDesc => Get(
        "+1.5m EXP attraction radius to harvest dropped orbs from afar.",
        "Tăng +1.5m Bán kính nhặt ngọc kinh nghiệm rơi từ quái vật."
    );

    // --- Ancient Treasure Chest & Relics Localization ---
    public static string ChestHeader => Get("✦ ANCIENT TREASURE CHEST ✦", "✦ BẢO RƯƠNG CỔ VẬT ✦");
    public static string ChestSubtitle => Get("You uncovered an ancient artifact of immense power!", "Bạn đã khai mở một cổ vật mang sức mạnh cổ xưa!");
    public static string CollectButtonText => Get("COLLECT [SPACE]", "THU NHẬN [SPACE]");
    public static string GoblinBannerAlert => Get("⚡ A Treasure Goblin appeared! Catch it before it escapes!", "⚡ Yêu Tinh Trộm Bảo xuất hiện! Hãy tiêu diệt nó trước khi trốn thoát!");

    public static string RelicBootsTitle => Get("WINDRUNNER BOOTS", "THẦN HÀNH HÀI");
    public static string RelicBootsDesc(int lvl) => Get(
        $"+15% Movement Speed permanently (Level {lvl}).",
        $"Tăng vĩnh viễn +15% Tốc độ di chuyển cho nhân vật (Cấp {lvl})."
    );

    public static string RelicMagnetTitle => Get("SPIRIT MAGNET ORB", "TỤ LINH CHÂU");
    public static string RelicMagnetDesc(int lvl) => Get(
        $"+50% EXP Pickup Range permanently (Level {lvl}).",
        $"Tăng vĩnh viễn +50% Bán kính nhặt ngọc kinh nghiệm (Cấp {lvl})."
    );

    public static string RelicVitalityTitle => Get("HEART CRYSTAL", "TRÁI TIM PHA LÊ");
    public static string RelicVitalityDesc(int lvl) => Get(
        $"+30 Max HP permanently and fully restores 100% Health (Level {lvl})!",
        $"Tăng vĩnh viễn +30 Máu tối đa và hồi phục 100% Máu ngay lập tức (Cấp {lvl})!"
    );

    public static string RelicPowerTitle => Get("POWER CHARM", "CỔ KIẾM BÙA CHÚ");
    public static string RelicPowerDesc(int lvl) => Get(
        $"+15% Damage to all skills permanently (Level {lvl}).",
        $"Tăng vĩnh viễn +15% Sát thương cho tất cả kỹ năng (Cấp {lvl})."
    );

    public static string RelicDiceTitle => Get("FORTUNE DICE", "XÚC XẮC VẬN MỆNH");
    public static string RelicDiceDesc(int lvl) => Get(
        $"+2 Level-Up Rerolls added immediately (Level {lvl})!",
        $"Nhận thêm +2 Lượt đổi thẻ kỹ năng khi lên cấp (Cấp {lvl})!"
    );

    // --- Game Over / Run Summary ---
    public static string GameOverHeader => Get("✦ FATE'S RETREAT ✦", "✦ THIÊN MỆNH CHƯA TẬN ✦");
    public static string GameOverSubtitle => Get("Your spirit returns to gather power for the next reincarnation.", "Chân linh hồi quy, tích lũy tu vi chuẩn bị cho lần luân hồi tiếp theo.");
    public static string GameOverTimeLabel => Get("⏱️ Survival Time:", "⏱️ Thời gian sống sót:");
    public static string GameOverLevelLabel => Get("⭐ Level Reached:", "⭐ Cấp độ đạt được:");
    public static string GameOverStonesLabel => Get("💎 Spirit Stones Earned:", "💎 Linh Thạch thu hoạch:");
    public static string GameOverRestartBtn => Get("🔄 RETRY / CHƠI LẠI", "🔄 LUÂN HỒI TÁI CHIẾN");
    public static string GameOverMainMenuBtn => Get("🚪 MAIN MENU / VỀ TRANG CHỦ", "🚪 TRỞ VỀ TRANG CHỦ");
}
