using UnityEngine;

/// <summary>
/// Bảng màu sắc, font chữ và quy chuẩn mỹ thuật Hi-Bit Pixel cho toàn bộ hệ thống UI HUD và Popup.
/// </summary>
public static class HUDTheme
{
    // --- Font Chuẩn ---
    private static Font _cachedPixelFont;
    public static Font PixelFont
    {
        get
        {
            if (_cachedPixelFont == null)
            {
                _cachedPixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _cachedPixelFont;
        }
    }

    // --- Màu Nền & Khung Viền ---
    public static readonly Color PanelBgDark = new Color(0.06f, 0.08f, 0.12f, 0.92f);
    public static readonly Color ModalBoxBgDark = new Color(0.06f, 0.08f, 0.14f, 0.98f);
    public static readonly Color FullScreenDim = new Color(0.03f, 0.05f, 0.08f, 0.85f);
    public static readonly Color MetalBorder = new Color(0.35f, 0.45f, 0.6f, 0.95f);
    public static readonly Color GoldBorder = new Color(1f, 0.82f, 0.2f, 0.95f);
    public static readonly Color RedBorder = new Color(0.85f, 0.25f, 0.25f, 0.95f);

    // --- Màu Điểm Nhấn (Accents) ---
    public static readonly Color GoldAccent = new Color(1f, 0.85f, 0.25f, 1f);
    public static readonly Color GoldTitle = new Color(1f, 0.86f, 0.3f, 1f);
    public static readonly Color CyanAccent = new Color(0.08f, 0.92f, 0.98f, 1f);
    public static readonly Color DiamondCyan = new Color(0f, 0.9f, 1f, 1f);
    public static readonly Color RubyAccent = new Color(0.95f, 0.22f, 0.28f, 1f);
    public static readonly Color SubtitleText = new Color(0.75f, 0.82f, 0.9f, 1f);

    // --- Màu Thanh Chỉ Số (Bars) ---
    public static readonly Color HpBarBg = new Color(0.18f, 0.05f, 0.08f, 0.95f);
    public static readonly Color HpBarOutline = new Color(0.4f, 0.15f, 0.18f, 1f);
    public static readonly Color ExpBarBg = new Color(0.04f, 0.12f, 0.2f, 0.95f);
    public static readonly Color ExpBarOutline = new Color(0.12f, 0.25f, 0.38f, 1f);

    // --- Màu Huy Hiệu & Thẻ Bài ---
    public static readonly Color LevelBadgeBg = new Color(0.15f, 0.2f, 0.3f, 1f);
    public static readonly Color CardBgDark = new Color(0.1f, 0.13f, 0.18f, 0.98f);
    public static readonly Color CardFusionBg = new Color(0.18f, 0.13f, 0.04f, 0.98f);

    // --- Màu Nút Bấm ---
    public static readonly Color RerollBtnBg = new Color(0.12f, 0.1f, 0.2f, 0.96f);
    public static readonly Color RerollBtnBorder = new Color(0.65f, 0.45f, 1f, 0.95f);
    public static readonly Color CollectBtnBg = new Color(0.95f, 0.78f, 0.15f, 1f);
    public static readonly Color RestartBtnBg = new Color(0.18f, 0.42f, 0.28f, 1f);
    public static readonly Color MainMenuBtnBg = new Color(0.22f, 0.26f, 0.38f, 1f);
}

