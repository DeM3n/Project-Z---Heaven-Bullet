using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện Popup Menu Tạm dừng trận đấu (Pause Menu):
/// Gồm 5 nút: Tiếp tục (Resume), Chơi lại (Restart), Đổi ngôn ngữ, Chế độ Dev, và Về trang chủ.
/// Hoàn toàn độc lập với ECS.
/// </summary>
public class PauseMenuView
{
    private readonly GameObject _panelObj;
    private readonly Text _titleText;
    private readonly Text _resumeBtnText;
    private readonly Text _restartBtnText;
    private readonly Text _devModeBtnText;

    public event Action OnResumeClicked;
    public event Action OnRestartClicked;
    public event Action OnLanguageClicked;
    public event Action OnDevModeClicked;
    public event Action OnMainMenuClicked;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    public PauseMenuView(Transform canvasParent)
    {
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "Pause_Panel",
            new Color(0.02f, 0.03f, 0.06f, 0.88f)
        );

        // Khung Panel trung tâm
        var boxObj = UIFactory.CreatePanel(
            _panelObj.transform,
            "CenterBox",
            new Vector2(380f, 440f),
            new Color(0.08f, 0.11f, 0.16f, 0.98f),
            new Color(0.35f, 0.55f, 0.8f, 0.95f),
            new Vector2(3f, -3f),
            true
        );
        var bRect = boxObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.pivot = new Vector2(0.5f, 0.5f);

        // Tiêu đề: ✦ GAME PAUSED ✦
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(boxObj.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0.88f);
        tRect.anchorMax = new Vector2(0.5f, 0.88f);
        tRect.pivot = new Vector2(0.5f, 0.5f);
        tRect.sizeDelta = new Vector2(340f, 40f);

        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = HUDTheme.PixelFont;
        _titleText.fontSize = 24;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = HUDTheme.GoldTitle;
        _titleText.text = GameLocalization.PauseHeader;

        // Container các nút
        float startY = 110f;
        float spacingY = 52f;
        Vector2 btnSize = new Vector2(280f, 44f);

        // 1. Resume Button
        CreateMenuButton(boxObj.transform, new Vector2(0f, startY), btnSize,
            new Color(0.12f, 0.35f, 0.55f, 1f), new Color(0.3f, 0.75f, 1f),
            GameLocalization.ResumeButton, () => OnResumeClicked?.Invoke(), out _resumeBtnText);

        // 2. Restart Button
        CreateMenuButton(boxObj.transform, new Vector2(0f, startY - spacingY), btnSize,
            new Color(0.45f, 0.18f, 0.18f, 1f), new Color(1f, 0.45f, 0.45f),
            GameLocalization.RestartButton, () => OnRestartClicked?.Invoke(), out _restartBtnText);

        // 3. Language Toggle Button
        CreateMenuButton(boxObj.transform, new Vector2(0f, startY - spacingY * 2), btnSize,
            new Color(0.18f, 0.22f, 0.3f, 1f), new Color(0.55f, 0.75f, 1f),
            "🌐 LANGUAGE / NGÔN NGỮ", () => OnLanguageClicked?.Invoke(), out _);

        // 4. Developer Mode Button
        CreateMenuButton(boxObj.transform, new Vector2(0f, startY - spacingY * 3), btnSize,
            new Color(0.28f, 0.14f, 0.42f, 1f), new Color(0.85f, 0.45f, 1f),
            GameLocalization.DevModeButton, () => OnDevModeClicked?.Invoke(), out _devModeBtnText);

        // 5. Return to Main Menu Button
        CreateMenuButton(boxObj.transform, new Vector2(0f, startY - spacingY * 4), btnSize,
            new Color(0.25f, 0.2f, 0.15f, 1f), new Color(1f, 0.8f, 0.4f),
            "🚪 MAIN MENU / VỀ TRANG CHỦ", () => OnMainMenuClicked?.Invoke(), out _);

        _panelObj.SetActive(false);
    }

    public void Show()
    {
        if (_panelObj != null) _panelObj.SetActive(true);
    }

    public void Hide()
    {
        if (_panelObj != null) _panelObj.SetActive(false);
    }

    public void UpdateLanguage()
    {
        if (_titleText != null) _titleText.text = GameLocalization.PauseHeader;
        if (_resumeBtnText != null) _resumeBtnText.text = GameLocalization.ResumeButton;
        if (_restartBtnText != null) _restartBtnText.text = GameLocalization.RestartButton;
        if (_devModeBtnText != null) _devModeBtnText.text = GameLocalization.DevModeButton;
    }

    private void CreateMenuButton(Transform parent, Vector2 anchoredPos, Vector2 size, Color bgColor, Color outlineColor,
        string label, Action onClick, out Text textComponent)
    {
        var btn = UIFactory.CreateButton(parent, $"Btn_{label}", size, bgColor, outlineColor, () => onClick?.Invoke());
        var rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;

        textComponent = UIFactory.CreateText(
            btn.transform,
            "Text",
            label,
            15,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
    }
}
