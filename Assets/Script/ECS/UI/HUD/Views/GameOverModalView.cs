using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện Popup Modal Game Over / Run Ended khi người chơi hy sinh,
/// hiển thị thống kê thời gian sinh tồn, cấp độ và số Linh Thạch tích lũy được trong ván đấu.
/// </summary>
public class GameOverModalView
{
    private readonly GameObject _panelObj;
    private readonly Text _titleText;
    private readonly Text _subText;
    private readonly Text _timeValueText;
    private readonly Text _levelValueText;
    private readonly Text _stonesValueText;
    private readonly Text _timeLabelText;
    private readonly Text _levelLabelText;
    private readonly Text _stonesLabelText;
    private readonly Text _restartBtnText;
    private readonly Text _mainMenuBtnText;

    private GameOverStats _lastStats;

    public event Action OnRestartClicked;
    public event Action OnMainMenuClicked;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    public GameOverModalView(Transform canvasParent)
    {
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "GameOver_Panel",
            new Color(0.04f, 0.02f, 0.05f, 0.92f)
        );

        // Khung Modal trung tâm
        var modalObj = UIFactory.CreatePanel(
            _panelObj.transform,
            "ModalBox",
            new Vector2(560f, 440f),
            new Color(0.08f, 0.09f, 0.14f, 0.98f),
            HUDTheme.RedBorder,
            new Vector2(3f, -3f),
            true
        );
        var modalRect = modalObj.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);

        // Header Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(modalObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(500f, 44f);

        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = HUDTheme.PixelFont;
        _titleText.fontSize = 28;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = new Color(1f, 0.35f, 0.35f);
        _titleText.text = GameLocalization.GameOverHeader;

        // Subtitle
        var subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(modalObj.transform, false);
        var subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -70f);
        subRect.sizeDelta = new Vector2(480f, 40f);

        _subText = subObj.AddComponent<Text>();
        _subText.font = HUDTheme.PixelFont;
        _subText.fontSize = 14;
        _subText.alignment = TextAnchor.MiddleCenter;
        _subText.color = new Color(0.7f, 0.75f, 0.85f);
        _subText.text = GameLocalization.GameOverSubtitle;

        // Stats Box
        var statsObj = UIFactory.CreatePanel(
            modalObj.transform,
            "StatsBox",
            new Vector2(460f, 160f),
            new Color(0.04f, 0.06f, 0.10f, 0.9f)
        );
        var statsRect = statsObj.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.5f, 0.5f);
        statsRect.anchorMax = new Vector2(0.5f, 0.5f);
        statsRect.pivot = new Vector2(0.5f, 0.5f);
        statsRect.anchoredPosition = new Vector2(0f, 10f);

        // Stats Rows
        (_timeLabelText, _timeValueText) = CreateStatRow(statsObj.transform, 35f, GameLocalization.GameOverTimeLabel, "00:00");
        (_levelLabelText, _levelValueText) = CreateStatRow(statsObj.transform, -5f, GameLocalization.GameOverLevelLabel, "Lv.1");
        (_stonesLabelText, _stonesValueText) = CreateStatRow(statsObj.transform, -45f, GameLocalization.GameOverStonesLabel, "+0 💎", new Color(0.3f, 0.9f, 1f));

        // Buttons Container
        var btnContainer = new GameObject("Buttons");
        btnContainer.transform.SetParent(modalObj.transform, false);
        var btnRect = btnContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 24f);
        btnRect.sizeDelta = new Vector2(480f, 50f);

        var hlg = btnContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Restart Button
        var restartBtn = UIFactory.CreateButton(
            btnContainer.transform,
            "RestartBtn",
            Vector2.zero,
            HUDTheme.RestartBtnBg,
            null,
            () => OnRestartClicked?.Invoke()
        );
        _restartBtnText = UIFactory.CreateText(
            restartBtn.transform,
            "Text",
            GameLocalization.GameOverRestartBtn,
            17,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        // Main Menu Button
        var menuBtn = UIFactory.CreateButton(
            btnContainer.transform,
            "MainMenuBtn",
            Vector2.zero,
            HUDTheme.MainMenuBtnBg,
            null,
            () => OnMainMenuClicked?.Invoke()
        );
        _mainMenuBtnText = UIFactory.CreateText(
            menuBtn.transform,
            "Text",
            GameLocalization.GameOverMainMenuBtn,
            17,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        _panelObj.SetActive(false);
    }

    public void Show(GameOverStats stats)
    {
        _lastStats = stats;
        if (_timeValueText != null) _timeValueText.text = stats.FormattedTime;
        if (_levelValueText != null) _levelValueText.text = $"Lv.{stats.FinalLevel}";
        if (_stonesValueText != null) _stonesValueText.text = $"+{stats.StonesEarned} 💎";

        if (_panelObj != null) _panelObj.SetActive(true);
    }

    public void Hide()
    {
        if (_panelObj != null) _panelObj.SetActive(false);
    }

    public void UpdateLanguage()
    {
        if (_titleText != null) _titleText.text = GameLocalization.GameOverHeader;
        if (_subText != null) _subText.text = GameLocalization.GameOverSubtitle;
        if (_timeLabelText != null) _timeLabelText.text = GameLocalization.GameOverTimeLabel;
        if (_levelLabelText != null) _levelLabelText.text = GameLocalization.GameOverLevelLabel;
        if (_stonesLabelText != null) _stonesLabelText.text = GameLocalization.GameOverStonesLabel;
        if (_restartBtnText != null) _restartBtnText.text = GameLocalization.GameOverRestartBtn;
        if (_mainMenuBtnText != null) _mainMenuBtnText.text = GameLocalization.GameOverMainMenuBtn;

        if (IsActive)
        {
            Show(_lastStats);
        }
    }

    private (Text label, Text value) CreateStatRow(Transform parent, float posY, string label, string value, Color? valueColor = null)
    {
        var rowObj = new GameObject("Row_" + label);
        rowObj.transform.SetParent(parent, false);
        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, posY);
        rowRect.sizeDelta = new Vector2(400f, 30f);

        // Label
        var lblObj = new GameObject("Label");
        lblObj.transform.SetParent(rowObj.transform, false);
        var lRect = lblObj.AddComponent<RectTransform>();
        lRect.anchorMin = Vector2.zero;
        lRect.anchorMax = new Vector2(0.6f, 1f);
        lRect.offsetMin = Vector2.zero;
        lRect.offsetMax = Vector2.zero;
        var lText = lblObj.AddComponent<Text>();
        lText.font = HUDTheme.PixelFont;
        lText.fontSize = 16;
        lText.alignment = TextAnchor.MiddleLeft;
        lText.color = new Color(0.8f, 0.85f, 0.95f);
        lText.text = label;

        // Value
        var valObj = new GameObject("Value");
        valObj.transform.SetParent(rowObj.transform, false);
        var vRect = valObj.AddComponent<RectTransform>();
        vRect.anchorMin = new Vector2(0.6f, 0f);
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = Vector2.zero;
        vRect.offsetMax = Vector2.zero;
        var vText = valObj.AddComponent<Text>();
        vText.font = HUDTheme.PixelFont;
        vText.fontSize = 17;
        vText.fontStyle = FontStyle.Bold;
        vText.alignment = TextAnchor.MiddleRight;
        vText.color = valueColor ?? Color.white;
        vText.text = value;

        return (lText, vText);
    }
}

