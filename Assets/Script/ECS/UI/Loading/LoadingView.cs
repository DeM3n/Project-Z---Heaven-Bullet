using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện Màn hình Tải cảnh (Procedural Loading Screen View).
/// Tự động thích ứng tỉ lệ 16:9, phong cách Hi-Bit Cổ Phong Tiên Hiệp & Bullet Heaven.
/// </summary>
public class LoadingView : MonoBehaviour
{
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _spinnerRect;
    private Text _statusText;
    private Text _percentText;
    private Text _tipText;
    private RectTransform _fillBarRect;

    private float _targetProgress;
    private float _currentProgress;
    private const float BarWidth = 630f;
    private const float BarHeight = 22f;

    public void Init()
    {
        gameObject.name = "[LoadingScreenCanvas]";
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000; // Đặt sorting order cao nhất tuyệt đối để che phủ hoàn toàn mọi HUD, UI và Settings

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        BuildUI();
        UpdateBarDisplay();
    }

    private void BuildUI()
    {
        // 1. Phông nền tối sâu thẳm (Deep Celestial Void)
        var bgObj = UIFactory.CreateFullScreenDim(transform, "Background", new Color(0.035f, 0.045f, 0.075f, 1f));

        // 2. Tiêu đề Game Header
        var titleObj = new GameObject("HeaderContainer");
        titleObj.transform.SetParent(bgObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.78f);
        titleRect.anchorMax = new Vector2(0.5f, 0.78f);
        titleRect.sizeDelta = new Vector2(800, 100);

        var titleText = UIFactory.CreateText(
            titleRect,
            "GameTitle",
            "PROJECT Z : HEAVEN BULLET",
            34,
            HUDTheme.GoldTitle,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0f, 0f, 0f, 0.95f),
            new Vector2(3f, -3f));

        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;
        var subTextObj = UIFactory.CreateText(
            titleRect,
            "SubTitle",
            isVi ? "✦ BẤT HỦ THIÊN KIẾM ✦" : "✦ IMMORTAL CELESTIAL BLADE ✦",
            16,
            HUDTheme.CyanAccent,
            FontStyle.Normal,
            TextAnchor.LowerCenter,
            new Color(0f, 0f, 0f, 0.8f),
            new Vector2(1.5f, -1.5f));
        var subRect = subTextObj.GetComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0, -32f);

        // 3. Biểu tượng xoay trung tâm (Rotating Cultivator Emblem)
        var spinnerObj = new GameObject("SpinnerEmblem");
        spinnerObj.transform.SetParent(bgObj.transform, false);
        _spinnerRect = spinnerObj.AddComponent<RectTransform>();
        _spinnerRect.anchorMin = new Vector2(0.5f, 0.52f);
        _spinnerRect.anchorMax = new Vector2(0.5f, 0.52f);
        _spinnerRect.sizeDelta = new Vector2(90, 90);

        // Vòng phát sáng ngoại vi
        var outerRing = UIFactory.CreatePanel(
            _spinnerRect,
            "OuterRing",
            new Vector2(86, 86),
            new Color(0.08f, 0.92f, 0.98f, 0.15f),
            HUDTheme.CyanAccent,
            new Vector2(2f, -2f));

        // Pháp bảo trung tâm xoay tròn
        var iconText = UIFactory.CreateText(
            _spinnerRect,
            "SpinnerIcon",
            "⚔",
            44,
            HUDTheme.GoldAccent,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0f, 0f, 0f, 0.9f),
            new Vector2(2f, -2f));

        // 4. Trạng thái tải cảnh (Status Text)
        var statusObj = new GameObject("StatusContainer");
        statusObj.transform.SetParent(bgObj.transform, false);
        var statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.38f);
        statusRect.anchorMax = new Vector2(0.5f, 0.38f);
        statusRect.sizeDelta = new Vector2(650, 40);

        _statusText = UIFactory.CreateText(
            statusRect,
            "StatusText",
            isVi ? "Đang triệu hồi thế giới..." : "Summoning realm...",
            18,
            HUDTheme.SubtitleText,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Color(0f, 0f, 0f, 0.8f),
            new Vector2(1.5f, -1.5f));

        // 5. Thanh tiến trình (Progress Bar)
        var barContainer = UIFactory.CreatePanel(
            bgObj.transform,
            "ProgressBarContainer",
            new Vector2(BarWidth + 10f, BarHeight + 8f),
            HUDTheme.ExpBarBg,
            new Color(0.08f, 0.92f, 0.98f, 0.85f),
            new Vector2(2f, -2f));
        var barContainerRect = barContainer.GetComponent<RectTransform>();
        barContainerRect.anchorMin = new Vector2(0.5f, 0.32f);
        barContainerRect.anchorMax = new Vector2(0.5f, 0.32f);

        // Vùng thanh lấp đầy (Fill Bar)
        var fillObj = new GameObject("FillBar");
        fillObj.transform.SetParent(barContainer.transform, false);
        _fillBarRect = fillObj.AddComponent<RectTransform>();
        _fillBarRect.anchorMin = new Vector2(0f, 0.5f);
        _fillBarRect.anchorMax = new Vector2(0f, 0.5f);
        _fillBarRect.pivot = new Vector2(0f, 0.5f);
        _fillBarRect.anchoredPosition = new Vector2(5f, 0f);
        _fillBarRect.sizeDelta = new Vector2(0f, BarHeight);

        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = HUDTheme.CyanAccent;

        // Chỉ số phần trăm nằm chính giữa thanh tiến trình
        _percentText = UIFactory.CreateText(
            barContainer.transform,
            "PercentText",
            "0%",
            16,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0f, 0f, 0f, 0.95f),
            new Vector2(1.5f, -1.5f));

        // 6. Hộp Bí Kíp & Mẹo Sinh Tồn (Bottom Tip Panel)
        var tipPanel = UIFactory.CreatePanel(
            bgObj.transform,
            "TipBoxPanel",
            new Vector2(920, 80),
            HUDTheme.ModalBoxBgDark,
            HUDTheme.MetalBorder,
            new Vector2(2f, -2f));
        var tipPanelRect = tipPanel.GetComponent<RectTransform>();
        tipPanelRect.anchorMin = new Vector2(0.5f, 0.16f);
        tipPanelRect.anchorMax = new Vector2(0.5f, 0.16f);

        _tipText = UIFactory.CreateText(
            tipPanel.transform,
            "TipContentText",
            LoadingTips.GetRandomTip(),
            16,
            HUDTheme.GoldAccent,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Color(0f, 0f, 0f, 0.85f),
            new Vector2(1.5f, -1.5f));
        _tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tipText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void Update()
    {
        // 1. Hiệu ứng xoay pháp bảo trung tâm
        if (_spinnerRect != null)
        {
            _spinnerRect.Rotate(0f, 0f, -140f * Time.unscaledDeltaTime);
            float pulse = 1f + Mathf.PingPong(Time.unscaledTime * 0.7f, 0.08f);
            _spinnerRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        // 2. Nội suy tiến độ tải mượt mà
        if (_currentProgress < _targetProgress)
        {
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.unscaledDeltaTime * 2.2f);
            UpdateBarDisplay();
        }
    }

    private void UpdateBarDisplay()
    {
        if (_fillBarRect != null)
        {
            _fillBarRect.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(_currentProgress), BarHeight);
        }

        if (_percentText != null)
        {
            _percentText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(_currentProgress) * 100f)}%";
        }
    }

    public void SetProgress(float target, string status)
    {
        _targetProgress = Mathf.Clamp01(target);
        if (_statusText != null && !string.IsNullOrEmpty(status))
        {
            _statusText.text = status;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    public void RefreshTip()
    {
        if (_tipText != null)
        {
            _tipText.text = LoadingTips.GetRandomTip();
        }
    }
}
