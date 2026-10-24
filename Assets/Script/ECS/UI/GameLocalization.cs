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

    // --- Perk Titles ---
    public static string FlyingSwordTitle => Get("FLYING SWORD", "PHI KIẾM HỘ THỂ");
    public static string SlashSwordTitle => Get("CRESCENT BLADE", "TRẢM YÊU KIẾM");
    public static string SwordFusionTitle => Get("CELESTIAL BLADE FUSION", "THIÊN KIẾM HỢP THỂ");
    public static string ProjectileSkillTitle => Get("ARCANE VOLLEY", "ĐẠN TỎA QUẠT");
    public static string HealTitle => Get("VITALITY ELIXIR", "TIÊN DƯỢC PHỤC HỒI");
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

    // --- Perk Descriptions ---
    public static string FlyingSwordDesc(int level)
    {
        return level switch
        {
            2 => Get(
                "+1 Flying Sword. Increases flight speed by +20% and damage by +35%.",
                "+1 Thanh Phi Kiếm hộ thể, tăng tốc độ bay +20% và sát thương +35%."
            ),
            3 => Get(
                "+1 Flying Sword (3 swords total). Expands outer engagement range.",
                "+1 Thanh Phi Kiếm (tổng 3 kiếm), mở rộng tầm xuất chiến quanh Player."
            ),
            4 => Get(
                "★ ULTIMATE STRIKE ★ Periodically dives into dense enemy swarms, triggering a massive AOE blast and DESTROYING 100% of enemy bullets!",
                "★ ĐÒN ULTIMATE ★ Tự động lao vào cụm quái đông nhất, nổ AOE cực đại và TIÊU HỦY 100% đạn địch!"
            ),
            _ => Get(
                "Summons ethereal flying swords that patrol and slice through nearby foes.",
                "Triệu hồi Phi Kiếm hộ thể bay lượn liên hoàn chém quái."
            )
        };
    }

    public static string SlashSwordDesc(int level)
    {
        return level switch
        {
            2 => Get(
                "+1 Sentinel Blade (2 total). Reduces cooldown to 3.3s and increases damage to 90 DMG.",
                "+1 Linh kiếm (tổng 2 kiếm). Giảm hồi chiêu xuống 3.3s và tăng sát thương lên 90 DMG."
            ),
            3 => Get(
                "+1 Sentinel Blade (3 total). Reduces cooldown to 2.6s, increases damage to 125 DMG and expands slash arc.",
                "+1 Linh kiếm (tổng 3 kiếm). Giảm hồi chiêu xuống 2.6s, tăng sát thương lên 125 DMG và mở rộng góc chém."
            ),
            4 => Get(
                "★ SWORD AURA EVOLUTION ★ Reduces cooldown to 2.0s! Every slash unleashes a Piercing Sword Wave that cuts through all foes!",
                "★ TIẾN HÓA KIẾM KHÍ ★ Giảm hồi chiêu xuống 2.0s! Mỗi nhát chém phóng ra luồng Kiếm Khí xuyên thấu toàn bộ mục tiêu!"
            ),
            _ => Get(
                "Summons a sentinel blade that rushes in to unleash lethal melee arc slashes (4s cooldown, 65 DMG).",
                "Triệu hồi linh kiếm lao vào áp sát chém quét vòng cung cực mạnh (hồi chiêu 4s, 65 DMG)."
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

    public static string HealDesc => Get(
        "Instantly restores 40% of maximum HP to the hero.",
        "Lập tức hồi phục lại 40% lượng Máu tối đa (Max HP) cho nhân vật."
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
}
