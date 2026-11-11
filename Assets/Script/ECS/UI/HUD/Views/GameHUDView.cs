using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện HUD in-game ở góc trên bên trái (HP, Ghost Damage Bar, EXP, Level Badge),
/// đồng hồ sinh tồn ở giữa phía trên và nút chuyển đổi ngôn ngữ ở góc trên bên phải.
/// Tích hợp hiệu ứng hoạt ảnh Fill mượt mà (Ghost Lag Bar, EXP Surge Lerp, Level-Up Wraparound).
/// Hoàn toàn độc lập với ECS, chỉ render dữ liệu thông qua PlayerHudSnapshot.
/// </summary>
public class GameHUDView
{
    private readonly GameObject _hudRoot;
    private readonly Image _hpFillImage;
    private readonly Image _hpGhostFillImage;
    private readonly Outline _hpOutline;
    private readonly Text _hpText;
    private readonly Image _expFillImage;
    private readonly RectTransform _levelBadgeRect;
    private readonly Text _levelBadgeText;
    private readonly Text _survivalTimerText;
    private readonly GameObject _siegeObjectiveObj;
    private readonly Text _siegeObjectiveText;

    private readonly Text _langButtonText;
    private readonly Text _langHintText;

    // --- Fill Animation States ---
    private float _currentHpPercent = -1f;
    private float _ghostHpPercent = -1f;
    private float _hpGhostDelayTimer = 0f;

    private float _currentExpPercent = -1f;
    private int _lastObservedLevel = 1;
    private bool _isLevelingUpWrap = false;
    private float _expFlashTimer = 0f;

    public GameHUDView(Transform canvasParent)
    {
        // 1. Đồng hồ sinh tồn (Survival Timer: mm:ss) ở giữa phía trên & Thanh mục tiêu phòng thủ Siege
        _survivalTimerText = BuildSurvivalTimer(canvasParent, out _siegeObjectiveObj, out _siegeObjectiveText);

        // 2. HUD Panel góc trên bên trái (Level Badge, HP Bar + Ghost Damage Bar, EXP Bar thanh mảnh)
        _hudRoot = BuildHUDPanel(
            canvasParent,
            out _hpFillImage,
            out _hpGhostFillImage,
            out _hpOutline,
            out _hpText,
            out _expFillImage,
            out _levelBadgeRect,
            out _levelBadgeText
        );

        // 3. Nút chuyển đổi ngôn ngữ ở góc trên bên phải
        BuildLanguageButton(canvasParent, out _langButtonText, out _langHintText);
    }

    public void SetActive(bool active)
    {
        if (_hudRoot != null) _hudRoot.SetActive(active);
    }

    /// <summary>
    /// Đặt lại trạng thái hoạt ảnh fill khi bắt đầu ván đấu mới.
    /// </summary>
    public void ResetAnimationState()
    {
        _currentHpPercent = -1f;
        _ghostHpPercent = -1f;
        _hpGhostDelayTimer = 0f;

        _currentExpPercent = -1f;
        _lastObservedLevel = 1;
        _isLevelingUpWrap = false;
        _expFlashTimer = 0f;

        if (_levelBadgeRect != null) _levelBadgeRect.localScale = Vector3.one;
    }

    /// <summary>
    /// Cập nhật hiển thị HUD từ Snapshot dữ liệu người chơi kèm hoạt ảnh fill mượt mà
    /// </summary>
    public void UpdateSnapshot(PlayerHudSnapshot snapshot)
    {
        float dt = Time.unscaledDeltaTime;

        // =========================================================================
        // 1. HOẠT ẢNH FILL THANH MÁU (HP BAR + GHOST DAMAGE LAG BAR)
        // =========================================================================
        float targetHp = Mathf.Clamp01(snapshot.HpPercent);

        if (_currentHpPercent < 0f)
        {
            _currentHpPercent = targetHp;
            _ghostHpPercent = targetHp;
        }

        if (targetHp < _currentHpPercent)
        {
            // Bị đánh mất máu: Thanh máu chính tụt ngay lập tức để người chơi cảm nhận cú đánh
            _currentHpPercent = targetHp;
            _hpGhostDelayTimer = 0.55f; // Giữ lại vệt bóng mờ trắng tinh khôi 0.55s (rất rõ ràng, không thể bỏ lỡ)
        }
        else if (targetHp > _currentHpPercent)
        {
            // Được hồi máu: Thanh bóng mờ bắt kịp ngay, thanh chính nở mượt mà lên
            _ghostHpPercent = targetHp;
            _currentHpPercent = Mathf.MoveTowards(_currentHpPercent, targetHp, dt * 1.5f);
        }

        // Thoát thời gian delay và trượt thanh bóng mờ sát thương về mức máu hiện tại
        if (_hpGhostDelayTimer > 0f)
        {
            _hpGhostDelayTimer -= dt;
        }
        else if (_ghostHpPercent > _currentHpPercent)
        {
            // Rút từ tốn và êm ái
            _ghostHpPercent = Mathf.MoveTowards(_ghostHpPercent, _currentHpPercent, dt * 0.65f);
        }
        else
        {
            _ghostHpPercent = _currentHpPercent;
        }

        if (_hpFillImage != null) _hpFillImage.fillAmount = _currentHpPercent;
        if (_hpGhostFillImage != null) _hpGhostFillImage.fillAmount = _ghostHpPercent;
        if (_hpText != null) _hpText.text = snapshot.FormattedHpText;

        // Nhấp nháy cảnh báo nguy cấp khi máu dưới 25%
        if (_hpOutline != null)
        {
            if (snapshot.HpPercent <= 0.25f && snapshot.HpPercent > 0f)
            {
                float ping = Mathf.PingPong(Time.unscaledTime * 4.5f, 1f);
                _hpOutline.effectColor = Color.Lerp(HUDTheme.HpBarOutline, new Color(1f, 0.2f, 0.25f, 1f), ping);
            }
            else
            {
                _hpOutline.effectColor = HUDTheme.HpBarOutline;
            }
        }

        // =========================================================================
        // 2. HOẠT ẢNH FILL THANH EXP (EXP BAR + SURGE LERP + LEVEL-UP WRAPAROUND)
        // =========================================================================
        float targetExp = Mathf.Clamp01(snapshot.ExpPercent);

        if (_currentExpPercent < 0f)
        {
            _currentExpPercent = targetExp;
            _lastObservedLevel = snapshot.Level;
        }

        // Kiểm tra biến cố Lên Cấp (Level Up)
        if (snapshot.Level > _lastObservedLevel)
        {
            _lastObservedLevel = snapshot.Level;
            _isLevelingUpWrap = true;

            // Hiệu ứng nảy bung huy hiệu cấp độ (Badge Pop Bounce)
            if (_levelBadgeRect != null)
            {
                _levelBadgeRect.localScale = Vector3.one * 1.35f;
            }
        }

        if (_isLevelingUpWrap)
        {
            // Khi lên cấp: Thanh EXP trượt đầy đến 100% trước khi cuộn về 0
            _currentExpPercent = Mathf.MoveTowards(_currentExpPercent, 1.0f, dt * 2.8f);
            if (_currentExpPercent >= 1.0f)
            {
                _currentExpPercent = 0f;
                _isLevelingUpWrap = false;
            }
        }
        else
        {
            // Nhặt ngọc EXP: Trượt êm ái dạng dòng chảy linh khí (tốc độ nhìn rõ mắt thường)
            if (targetExp > _currentExpPercent)
            {
                float diff = targetExp - _currentExpPercent;
                float speed = Mathf.Clamp(diff * 3.5f, 0.45f, 1.8f);
                _currentExpPercent = Mathf.MoveTowards(_currentExpPercent, targetExp, dt * speed);
                _expFlashTimer = 0.3f; // Giữ vệt sáng neon trong 0.3s
            }
            else
            {
                _currentExpPercent = Mathf.MoveTowards(_currentExpPercent, targetExp, dt * 1.5f);
            }
        }

        if (_expFillImage != null)
        {
            _expFillImage.fillAmount = _currentExpPercent;
            _expFillImage.color = HUDTheme.CyanAccent;
        }

        // Phục hồi kích thước huy hiệu Level Badge
        if (_levelBadgeRect != null && _levelBadgeRect.localScale.x > 1f)
        {
            _levelBadgeRect.localScale = Vector3.MoveTowards(_levelBadgeRect.localScale, Vector3.one, dt * 2.5f);
        }

        if (_levelBadgeText != null) _levelBadgeText.text = snapshot.FormattedLevelBadge;

        // =========================================================================
        // 3. ĐỒNG HỒ SINH TỒN & MỤC TIÊU MEGA WAVE
        // =========================================================================
        if (_survivalTimerText != null)
        {
            if (snapshot.IsMegaWaveActive)
            {
                _survivalTimerText.text = $"⚠️ {snapshot.FormattedSurvivalTimer}";
                _survivalTimerText.color = (Time.unscaledTime % 0.8f < 0.4f) ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.9f, 0.4f);
            }
            else
            {
                _survivalTimerText.text = snapshot.FormattedSurvivalTimer;
                _survivalTimerText.color = new Color(1f, 0.9f, 0.4f);
            }
        }

        if (_siegeObjectiveObj != null && _siegeObjectiveText != null)
        {
            if (snapshot.IsMegaWaveActive)
            {
                _siegeObjectiveObj.SetActive(true);
                _siegeObjectiveText.text = GameLocalization.SiegeSurvivalText(snapshot.MegaWaveTimeRemainingSec, snapshot.MegaWaveEnemiesKilled);
            }
            else
            {
                _siegeObjectiveObj.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Đồng bộ văn bản nút ngôn ngữ
    /// </summary>
    public void UpdateLanguage()
    {
        if (_langButtonText != null) _langButtonText.text = GameLocalization.LangButtonText;
        if (_langHintText != null) _langHintText.text = GameLocalization.LangHintText;
    }

    private Text BuildSurvivalTimer(Transform parent, out GameObject siegeObj, out Text siegeTxt)
    {
        var timerObj = UIFactory.CreatePanel(
            parent,
            "Survival_Timer",
            new Vector2(140f, 38f),
            HUDTheme.PanelBgDark,
            HUDTheme.MetalBorder,
            new Vector2(2f, -2f)
        );

        var rect = timerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);

        var timerText = UIFactory.CreateText(
            timerObj.transform,
            "Text",
            "00:00",
            20,
            new Color(1f, 0.9f, 0.4f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.black
        );

        // Thanh mục tiêu phòng thủ Siege (chỉ hiển thị khi đang diễn ra Mega Wave)
        siegeObj = UIFactory.CreatePanel(
            parent,
            "Siege_Objective_Panel",
            new Vector2(430f, 32f),
            new Color(0.2f, 0.04f, 0.04f, 0.94f),
            HUDTheme.GoldBorder,
            new Vector2(2f, -2f)
        );
        var sRect = siegeObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 1f);
        sRect.anchorMax = new Vector2(0.5f, 1f);
        sRect.pivot = new Vector2(0.5f, 1f);
        sRect.anchoredPosition = new Vector2(0f, -68f);

        siegeTxt = UIFactory.CreateText(
            siegeObj.transform,
            "Siege_Text",
            "",
            14,
            new Color(1f, 0.88f, 0.35f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.black
        );

        siegeObj.SetActive(false);
        return timerText;
    }

    private GameObject BuildHUDPanel(
        Transform parent,
        out Image hpFill,
        out Image hpGhostFill,
        out Outline hpOutline,
        out Text hpTxt,
        out Image expFill,
        out RectTransform badgeRect,
        out Text levelBadgeTxt)
    {
        var hudObj = UIFactory.CreatePanel(
            parent,
            "HUD_TopLeft",
            new Vector2(350f, 74f),
            HUDTheme.PanelBgDark,
            HUDTheme.MetalBorder,
            new Vector2(2f, -2f)
        );

        var rect = hudObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);

        // --- Level Badge ---
        var badgeObj = UIFactory.CreatePanel(
            hudObj.transform,
            "LevelBadge",
            new Vector2(56f, 56f),
            HUDTheme.LevelBadgeBg,
            HUDTheme.GoldAccent,
            new Vector2(2f, -2f)
        );
        badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(38f, 0f);

        levelBadgeTxt = UIFactory.CreateText(
            badgeObj.transform,
            "Text",
            "LV.1",
            20,
            new Color(1f, 0.9f, 0.3f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.black
        );

        // --- Container thanh Máu & EXP ---
        var barsContainer = new GameObject("BarsContainer");
        barsContainer.transform.SetParent(hudObj.transform, false);
        var barsRect = barsContainer.AddComponent<RectTransform>();
        barsRect.anchorMin = Vector2.zero;
        barsRect.anchorMax = Vector2.one;
        barsRect.offsetMin = new Vector2(74f, 10f);
        barsRect.offsetMax = new Vector2(-12f, -10f);

        // HP Bar (chiếm phần lớn không gian phía trên, nổi bật, cao ~37px)
        var hpBarObj = new GameObject("HP_Bar");
        hpBarObj.transform.SetParent(barsContainer.transform, false);
        var hpRect = hpBarObj.AddComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0f, 0.28f);
        hpRect.anchorMax = new Vector2(1f, 1f);
        hpRect.offsetMin = Vector2.zero;
        hpRect.offsetMax = Vector2.zero;

        var hpBg = hpBarObj.AddComponent<Image>();
        hpBg.color = HUDTheme.HpBarBg;
        hpOutline = hpBarObj.AddComponent<Outline>();
        hpOutline.effectColor = HUDTheme.HpBarOutline;
        hpOutline.effectDistance = new Vector2(1f, -1f);

        // HP Ghost Bar (vệt bóng mờ sát thương trắng tinh khôi nằm ngay dưới thanh máu đỏ)
        var hpGhostObj = new GameObject("HP_GhostFill");
        hpGhostObj.transform.SetParent(hpBarObj.transform, false);
        var hpGhostRect = hpGhostObj.AddComponent<RectTransform>();
        hpGhostRect.anchorMin = Vector2.zero;
        hpGhostRect.anchorMax = Vector2.one;
        hpGhostRect.offsetMin = new Vector2(2f, 2f);
        hpGhostRect.offsetMax = new Vector2(-2f, -2f);

        hpGhostFill = hpGhostObj.AddComponent<Image>();
        hpGhostFill.sprite = UIFactory.WhiteSprite;
        hpGhostFill.type = Image.Type.Filled;
        hpGhostFill.fillMethod = Image.FillMethod.Horizontal;
        hpGhostFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpGhostFill.color = new Color(1f, 1f, 1f, 0.95f); // Trắng sáng tinh khôi (Stark White) tạo độ tương phản cực đại khi mất máu

        // HP Main Bar (màu đỏ ruby)
        var hpFillObj = new GameObject("HP_Fill");
        hpFillObj.transform.SetParent(hpBarObj.transform, false);
        var hpFillRect = hpFillObj.AddComponent<RectTransform>();
        hpFillRect.anchorMin = Vector2.zero;
        hpFillRect.anchorMax = Vector2.one;
        hpFillRect.offsetMin = new Vector2(2f, 2f);
        hpFillRect.offsetMax = new Vector2(-2f, -2f);

        hpFill = hpFillObj.AddComponent<Image>();
        hpFill.sprite = UIFactory.WhiteSprite;
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFill.color = HUDTheme.RubyAccent;

        hpTxt = UIFactory.CreateText(
            hpBarObj.transform,
            "HP_Text",
            "100 / 100",
            14,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.black
        );

        // EXP Bar (Thanh kinh nghiệm thanh mảnh, gọn gàng, không số)
        var expBarObj = new GameObject("EXP_Bar");
        expBarObj.transform.SetParent(barsContainer.transform, false);
        var expRect = expBarObj.AddComponent<RectTransform>();
        expRect.anchorMin = Vector2.zero;
        expRect.anchorMax = new Vector2(1f, 0.16f);
        expRect.offsetMin = Vector2.zero;
        expRect.offsetMax = Vector2.zero;

        var expBg = expBarObj.AddComponent<Image>();
        expBg.color = HUDTheme.ExpBarBg;
        var expOutline = expBarObj.AddComponent<Outline>();
        expOutline.effectColor = HUDTheme.ExpBarOutline;
        expOutline.effectDistance = new Vector2(1f, -1f);

        // EXP Main Bar (màu xanh ngọc lam Cyan sắc nét, lấp đầy từ trái sang phải, không hiệu ứng mờ)
        var expFillObj = new GameObject("EXP_Fill");
        expFillObj.transform.SetParent(expBarObj.transform, false);
        var expFillRect = expFillObj.AddComponent<RectTransform>();
        expFillRect.anchorMin = Vector2.zero;
        expFillRect.anchorMax = Vector2.one;
        expFillRect.offsetMin = new Vector2(1f, 1f);
        expFillRect.offsetMax = new Vector2(-1f, -1f);

        expFill = expFillObj.AddComponent<Image>();
        expFill.sprite = UIFactory.WhiteSprite;
        expFill.type = Image.Type.Filled;
        expFill.fillMethod = Image.FillMethod.Horizontal;
        expFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        expFill.color = HUDTheme.CyanAccent;

        return hudObj;
    }

    private void BuildLanguageButton(Transform parent, out Text labelTxt, out Text hintTxt)
    {
        var btnObj = UIFactory.CreatePanel(
            parent,
            "Language_Button",
            new Vector2(106f, 38f),
            new Color(0.08f, 0.12f, 0.18f, 0.95f),
            new Color(0.35f, 0.55f, 0.75f, 0.95f),
            new Vector2(2f, -2f),
            true
        );

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);

        var btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnObj.GetComponent<Image>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = colors;
        btn.onClick.AddListener(GameLocalization.ToggleLanguage);

        // Label EN / VI
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.3f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        labelTxt = labelObj.AddComponent<Text>();
        labelTxt.font = HUDTheme.PixelFont;
        labelTxt.fontSize = 15;
        labelTxt.fontStyle = FontStyle.Bold;
        labelTxt.alignment = TextAnchor.MiddleCenter;
        labelTxt.color = new Color(1f, 0.9f, 0.4f);
        labelTxt.text = GameLocalization.LangButtonText;

        // Subtext [L]
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(btnObj.transform, false);
        var hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = new Vector2(1f, 0.35f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;

        hintTxt = hintObj.AddComponent<Text>();
        hintTxt.font = HUDTheme.PixelFont;
        hintTxt.fontSize = 9;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color = new Color(0.65f, 0.75f, 0.85f);
        hintTxt.text = GameLocalization.LangHintText;
    }
}
