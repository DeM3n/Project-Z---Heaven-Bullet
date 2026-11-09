using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR || DEBUG

/// <summary>
/// Quản lý giao diện Bảng điều khiển Lập trình viên (Developer Control Panel):
/// - Phân chia thành 3 Tab chuyên biệt:
///   + Tab 0: ⚔️ SKILLS & FUSION (Scrollable view, Max All, Level stepper, Direct level buttons, Fusion toggles).
///   + Tab 1: 🌊 WAVES & MEGA WAVE (Live Wave monitor, Instant Mega Wave 3/6/9, End Mega Wave, Timeline fast-forward & controls).
///   + Tab 2: 🛠️ CHEATS & UTILITIES (Full HP, God Mode, Level Up, Kill All, Speed Boost, Regen, Camera Zoom, Spirit Stones, Chest, Magnet).
/// - Tuyệt đối không chứa bất kỳ logic hoặc tham chiếu nào đến ECS (0 ECS leakage).
/// </summary>
public class DevControlPanelView
{
    private readonly GameObject _panelObj;
    private readonly Text _headerText;
    private readonly Text _closeBtnText;

    // Tabs management
    private int _currentTabIndex = 0;
    private readonly Button[] _tabButtons = new Button[3];
    private readonly Image[] _tabBackgrounds = new Image[3];
    private readonly Text[] _tabTexts = new Text[3];
    private readonly GameObject[] _tabContainers = new GameObject[3];

    // Tab 0: Skills UI elements
    private readonly Dictionary<string, Text> _skillLevelTexts = new Dictionary<string, Text>();
    private readonly Text _maxAllBtnText;

    // Tab 1: Wave Dashboard texts
    private readonly Text _waveTimeText;
    private readonly Text _waveStateText;
    private readonly Text _megaWaveTimerText;
    private readonly Text _megaWaveSurgeText;
    private readonly Text _waveKillsText;
    private readonly Text _wavePauseStatusText;

    // Action state texts (for dynamic button labels)
    private readonly Dictionary<string, Text> _actionStateTexts = new Dictionary<string, Text>();

    // Events forwarded to Bridge
    public event Action<string, int> OnSkillLevelDelta;
    public event Action<string, int> OnSkillDirectLevel;
    public event Action<string> OnToggleFusion;
    public event Action OnMaxAllSkills;
    public event Action OnCloseClicked;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    public DevControlPanelView(
        Transform canvasParent,
        List<DevSkillInfo> skillInfos,
        List<DevAction> devActions,
        List<DevAction> waveActions)
    {
        // 1. Full Screen Overlay Dim
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "Dev_Panel",
            new Color(0.01f, 0.02f, 0.04f, 0.93f)
        );

        // 2. Central Window Box (900 x 600 - rộng rãi, cân đối trên màn hình 1280x720)
        var boxObj = UIFactory.CreatePanel(
            _panelObj.transform,
            "DevBox",
            new Vector2(900f, 600f),
            new Color(0.06f, 0.08f, 0.12f, 0.98f),
            HUDTheme.PurpleBorder,
            new Vector2(3f, -3f),
            true
        );
        var bRect = boxObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.pivot = new Vector2(0.5f, 0.5f);

        // 3. Top Header Title
        var headerObj = new GameObject("Header");
        headerObj.transform.SetParent(boxObj.transform, false);
        var hRect = headerObj.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0.5f, 0.5f);
        hRect.anchorMax = new Vector2(0.5f, 0.5f);
        hRect.anchoredPosition = new Vector2(0f, 266f);
        hRect.sizeDelta = new Vector2(740f, 34f);

        _headerText = headerObj.AddComponent<Text>();
        _headerText.font = HUDTheme.PixelFont;
        _headerText.fontSize = 20;
        _headerText.fontStyle = FontStyle.Bold;
        _headerText.alignment = TextAnchor.MiddleCenter;
        _headerText.color = HUDTheme.PurpleAccent;
        _headerText.text = GameLocalization.DevPanelHeader;

        // 4. Nút Đóng [✕] góc trên bên phải
        var closeBtn = UIFactory.CreateButton(
            boxObj.transform,
            "Btn_CloseTop",
            new Vector2(40f, 32f),
            new Color(0.35f, 0.12f, 0.15f, 1f),
            new Color(1f, 0.4f, 0.5f),
            () => OnCloseClicked?.Invoke()
        );
        var cRect = closeBtn.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = new Vector2(415f, 266f);

        _closeBtnText = UIFactory.CreateText(
            closeBtn.transform,
            "Text",
            "✕",
            16,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        // 5. Thanh Điều Hướng Tabs (Top Tab Bar at Y = 218f)
        float tabWidth = 275f;
        float tabHeight = 36f;
        float[] tabX = { -285f, 0f, 285f };
        string[] tabLabels = { GameLocalization.DevTabSkills, GameLocalization.DevTabWaves, GameLocalization.DevTabCheats };

        for (int i = 0; i < 3; i++)
        {
            int tabIdx = i;
            var tabBtn = UIFactory.CreateButton(
                boxObj.transform,
                $"Tab_{i}",
                new Vector2(tabWidth, tabHeight),
                new Color(0.1f, 0.13f, 0.18f, 1f),
                new Color(0.3f, 0.45f, 0.65f),
                () => SelectTab(tabIdx),
                new Vector2(1f, -1f)
            );
            var tRect = tabBtn.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 0.5f);
            tRect.anchorMax = new Vector2(0.5f, 0.5f);
            tRect.anchoredPosition = new Vector2(tabX[i], 218f);

            var tTxt = UIFactory.CreateText(
                tabBtn.transform,
                "Text",
                tabLabels[i],
                14,
                Color.white,
                FontStyle.Bold,
                TextAnchor.MiddleCenter
            );

            _tabButtons[i] = tabBtn.GetComponent<Button>();
            _tabBackgrounds[i] = tabBtn.GetComponent<Image>();
            _tabTexts[i] = tTxt;
        }

        // =====================================================================
        // TAB 0: ⚔️ SKILLS & FUSION (Scrollable view + Max All)
        // =====================================================================
        _tabContainers[0] = new GameObject("TabContainer_Skills");
        _tabContainers[0].transform.SetParent(boxObj.transform, false);
        var t0Rect = _tabContainers[0].AddComponent<RectTransform>();
        t0Rect.anchorMin = Vector2.zero;
        t0Rect.anchorMax = Vector2.one;
        t0Rect.sizeDelta = Vector2.zero;

        // Nút MAX ALL SKILLS
        var maxAllBtn = UIFactory.CreateButton(
            _tabContainers[0].transform,
            "Btn_MaxAll",
            new Vector2(560f, 36f),
            new Color(0.38f, 0.28f, 0.05f, 1f),
            HUDTheme.GoldBorder,
            () => OnMaxAllSkills?.Invoke()
        );
        var mRect = maxAllBtn.GetComponent<RectTransform>();
        mRect.anchorMin = new Vector2(0.5f, 0.5f);
        mRect.anchorMax = new Vector2(0.5f, 0.5f);
        mRect.anchoredPosition = new Vector2(0f, 172f);

        _maxAllBtnText = UIFactory.CreateText(
            maxAllBtn.transform,
            "Text",
            GameLocalization.DevMaxAllSkills,
            14,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        // ScrollRect Viewport cho danh sách kỹ năng
        BuildSkillsScrollView(_tabContainers[0].transform, skillInfos);

        // =====================================================================
        // TAB 1: 🌊 WAVES & MEGA WAVE
        // =====================================================================
        _tabContainers[1] = new GameObject("TabContainer_Waves");
        _tabContainers[1].transform.SetParent(boxObj.transform, false);
        var t1Rect = _tabContainers[1].AddComponent<RectTransform>();
        t1Rect.anchorMin = Vector2.zero;
        t1Rect.anchorMax = Vector2.one;
        t1Rect.sizeDelta = Vector2.zero;

        // Wave Monitor Dashboard Box (width 840, height 86, Y = 125f)
        var dashObj = UIFactory.CreatePanel(
            _tabContainers[1].transform,
            "WaveDashboard",
            new Vector2(840f, 86f),
            new Color(0.08f, 0.11f, 0.16f, 0.95f),
            new Color(0.2f, 0.75f, 0.95f, 0.85f),
            new Vector2(2f, -2f)
        );
        var dRect = dashObj.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 0.5f);
        dRect.anchorMax = new Vector2(0.5f, 0.5f);
        dRect.anchoredPosition = new Vector2(0f, 125f);

        // Col 1: Timeline Time & State
        CreatePositionedText(dashObj.transform, "Col1_Header", "⏱️ TIMELINE SURVIVAL", 12, new Color(0.6f, 0.75f, 0.9f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(-280f, 22f), new Vector2(240f, 20f));
        _waveTimeText = CreatePositionedText(dashObj.transform, "Col1_Time", "00:00", 22, new Color(1f, 0.85f, 0.25f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(-280f, -4f), new Vector2(240f, 28f));
        _waveStateText = CreatePositionedText(dashObj.transform, "Col1_State", "[ NORMAL TIMELINE ]", 11, new Color(0.35f, 0.85f, 1f), FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(-280f, -24f), new Vector2(240f, 18f));

        // Col 2: Mega Wave Tracker
        CreatePositionedText(dashObj.transform, "Col2_Header", "⚡ MEGA WAVE TRACKER", 12, new Color(0.6f, 0.75f, 0.9f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 22f), new Vector2(240f, 20f));
        _megaWaveTimerText = CreatePositionedText(dashObj.transform, "Col2_Timer", "READY AT 03:00", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, -4f), new Vector2(240f, 28f));
        _megaWaveSurgeText = CreatePositionedText(dashObj.transform, "Col2_Surge", "Surge Stage: 0/2", 11, new Color(1f, 0.75f, 0.35f), FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(240f, 18f));

        // Col 3: Kills & Clock Status
        CreatePositionedText(dashObj.transform, "Col3_Header", "⚔️ COMBAT & CLOCK", 12, new Color(0.6f, 0.75f, 0.9f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(280f, 22f), new Vector2(240f, 20f));
        _waveKillsText = CreatePositionedText(dashObj.transform, "Col3_Kills", "0 KILLS IN WAVE", 18, new Color(0.4f, 1f, 0.55f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(280f, -4f), new Vector2(240f, 28f));
        _wavePauseStatusText = CreatePositionedText(dashObj.transform, "Col3_Clock", "Clock: RUNNING", 11, new Color(0.7f, 0.85f, 0.95f), FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(280f, -24f), new Vector2(240f, 18f));

        // Section A: Instant Mega Wave Launchers (Y = 32f)
        CreatePositionedText(_tabContainers[1].transform, "SecA_Title", "— ⚡ INSTANT MEGA WAVE SIEGE (60s CHALLENGE) —", 13, new Color(1f, 0.75f, 0.25f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 58f), new Vector2(800f, 20f));
        float[] secAx = { -305f, -102f, 102f, 305f };
        string[] secAIds = { "TriggerMega3", "TriggerMega6", "TriggerMega9", "EndMegaWave" };
        for (int i = 0; i < 4; i++)
        {
            var act = waveActions.Find(a => a.ActionId == secAIds[i]);
            if (act != null)
            {
                BuildCheatButton(_tabContainers[1].transform, act, new Vector2(secAx[i], 24f), new Vector2(195f, 40f));
            }
        }

        // Section B: Timeline Fast-Forward (Y = -62f)
        CreatePositionedText(_tabContainers[1].transform, "SecB_Title", "— ⏩ TIMELINE FAST-FORWARD (JUMP TO -3s BEFORE MEGA WAVE) —", 13, new Color(0.35f, 0.85f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, -34f), new Vector2(800f, 20f));
        float[] secBx = { -270f, 0f, 270f };
        string[] secBIds = { "JumpWave3", "JumpWave6", "JumpWave9" };
        for (int i = 0; i < 3; i++)
        {
            var act = waveActions.Find(a => a.ActionId == secBIds[i]);
            if (act != null)
            {
                BuildCheatButton(_tabContainers[1].transform, act, new Vector2(secBx[i], -68f), new Vector2(255f, 38f));
            }
        }

        // Section C: Timeline & Clock Adjustments (Y = -152f)
        CreatePositionedText(_tabContainers[1].transform, "SecC_Title", "— ⏱️ TIMELINE & CLOCK CONTROLS —", 13, new Color(0.7f, 0.8f, 0.9f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, -122f), new Vector2(800f, 20f));
        float[] secCx = { -305f, -102f, 102f, 305f };
        string[] secCIds = { "AddMinute", "SubMinute", "ResetTimeline", "TogglePauseTimer" };
        for (int i = 0; i < 4; i++)
        {
            var act = waveActions.Find(a => a.ActionId == secCIds[i]);
            if (act != null)
            {
                BuildCheatButton(_tabContainers[1].transform, act, new Vector2(secCx[i], -156f), new Vector2(195f, 38f));
            }
        }

        // Tip text at bottom of Tab 1
        CreatePositionedText(
            _tabContainers[1].transform,
            "WaveTip",
            "💡 Mega Wave spawns intense continuous enemy hordes for 60 seconds with 2 ambush surges at 40s and 20s remaining.\nSurviving grants an Ancient Chest & EXP Magnet!",
            12,
            new Color(0.5f, 0.7f, 0.85f, 0.9f),
            FontStyle.Italic,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -225f),
            new Vector2(820f, 38f)
        );

        // =====================================================================
        // TAB 2: 🛠️ CHEATS & UTILITIES
        // =====================================================================
        _tabContainers[2] = new GameObject("TabContainer_Cheats");
        _tabContainers[2].transform.SetParent(boxObj.transform, false);
        var t2Rect = _tabContainers[2].AddComponent<RectTransform>();
        t2Rect.anchorMin = Vector2.zero;
        t2Rect.anchorMax = Vector2.one;
        t2Rect.sizeDelta = Vector2.zero;

        CreatePositionedText(_tabContainers[2].transform, "CheatsTitle", "— 🛠️ PLAYER STATS & UTILITY CHEATS —", 14, new Color(1f, 0.85f, 0.25f), FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 170f), new Vector2(800f, 20f));

        // Grid 3 columns x 4 rows
        float[] cheatColX = { -275f, 0f, 275f };
        float[] cheatRowY = { 115f, 52f, -11f, -74f };
        Vector2 cheatBtnSize = new Vector2(260f, 44f);

        for (int i = 0; i < devActions.Count; i++)
        {
            int col = i % 3;
            int row = i / 3;
            if (row >= cheatRowY.Length) break;

            var action = devActions[i];
            BuildCheatButton(_tabContainers[2].transform, action, new Vector2(cheatColX[col], cheatRowY[row]), cheatBtnSize);
        }

        // Bottom warning note
        CreatePositionedText(
            _tabContainers[2].transform,
            "CheatWarning",
            "⚠️ Note: Activating cheats flags this run as a debug session and disables persistent Spirit Stone gains for the run.",
            12,
            new Color(0.9f, 0.45f, 0.45f, 0.85f),
            FontStyle.Italic,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -155f),
            new Vector2(800f, 32f)
        );

        // Default to Tab 0
        SelectTab(0);
        _panelObj.SetActive(false);
    }

    public void SelectTab(int index)
    {
        _currentTabIndex = Mathf.Clamp(index, 0, 2);

        for (int i = 0; i < 3; i++)
        {
            bool isActive = (i == _currentTabIndex);
            if (_tabContainers[i] != null) _tabContainers[i].SetActive(isActive);

            if (_tabBackgrounds[i] != null)
            {
                _tabBackgrounds[i].color = isActive
                    ? new Color(0.18f, 0.26f, 0.38f, 1f)
                    : new Color(0.08f, 0.1f, 0.14f, 0.85f);
            }

            if (_tabTexts[i] != null)
            {
                _tabTexts[i].color = isActive ? new Color(0.35f, 0.9f, 1f) : new Color(0.6f, 0.65f, 0.75f);
                _tabTexts[i].fontSize = isActive ? 15 : 13;
            }
        }
    }

    public void Show()
    {
        if (_panelObj != null) _panelObj.SetActive(true);
    }

    public void Hide()
    {
        if (_panelObj != null) _panelObj.SetActive(false);
    }

    /// <summary>
    /// Làm mới toàn bộ text hiển thị cấp độ kỹ năng, trạng thái cheat và dashboard wave.
    /// </summary>
    public void Refresh(
        List<DevSkillState> states,
        List<DevAction> devActions,
        List<DevAction> waveActions,
        DevWaveStatus waveStatus)
    {
        // 1. Refresh Skills
        foreach (var state in states)
        {
            if (_skillLevelTexts.TryGetValue(state.SkillId, out var txt))
            {
                txt.text = state.IsFusion ? state.FormattedFusionText : state.FormattedLevelText;
                txt.color = state.Level switch
                {
                    0 => new Color(0.6f, 0.6f, 0.6f),
                    4 or 5 => new Color(1f, 0.85f, 0.2f),
                    _ => new Color(0.2f, 0.9f, 1f)
                };
            }
        }

        // 2. Refresh Dev Cheat Button Labels
        foreach (var action in devActions)
        {
            if (_actionStateTexts.TryGetValue(action.ActionId, out var txt))
            {
                txt.text = action.LabelGetter();
            }
        }

        // 3. Refresh Wave Action Button Labels
        foreach (var action in waveActions)
        {
            if (_actionStateTexts.TryGetValue(action.ActionId, out var txt))
            {
                txt.text = action.LabelGetter();
            }
        }

        // 4. Refresh Wave Dashboard Display
        if (_waveTimeText != null)
        {
            int totalSec = Mathf.FloorToInt(waveStatus.ElapsedTime);
            int m = totalSec / 60;
            int s = totalSec % 60;
            _waveTimeText.text = $"{m:00}:{s:00}";
        }

        if (_waveStateText != null)
        {
            _waveStateText.text = waveStatus.State switch
            {
                WaveState.MegaWaveActive => "🔥 MEGA WAVE ACTIVE!",
                WaveState.MegaWaveCleared => "✨ MEGA WAVE CLEARED!",
                _ => "[ NORMAL TIMELINE ]"
            };
            _waveStateText.color = (waveStatus.State == WaveState.MegaWaveActive)
                ? new Color(1f, 0.4f, 0.25f)
                : new Color(0.35f, 0.85f, 1f);
        }

        if (_megaWaveTimerText != null)
        {
            if (waveStatus.State == WaveState.MegaWaveActive)
            {
                _megaWaveTimerText.text = $"🔥 {waveStatus.MegaWaveTimer:F1}s LEFT";
                _megaWaveTimerText.color = new Color(1f, 0.35f, 0.2f);
            }
            else
            {
                int nextMin = (waveStatus.ElapsedTime < 180f) ? 3 : ((waveStatus.ElapsedTime < 360f) ? 6 : 9);
                float secLeft = Mathf.Max(0f, (nextMin * 60f) - waveStatus.ElapsedTime);
                int leftM = Mathf.FloorToInt(secLeft) / 60;
                int leftS = Mathf.FloorToInt(secLeft) % 60;
                _megaWaveTimerText.text = $"NEXT: {nextMin}M ({leftM:00}:{leftS:00})";
                _megaWaveTimerText.color = Color.white;
            }
        }

        if (_megaWaveSurgeText != null)
        {
            if (waveStatus.State == WaveState.MegaWaveActive)
            {
                _megaWaveSurgeText.text = $"Surge Stage: {waveStatus.SurgeStage}/2 (Minute {waveStatus.ActiveMegaWaveMinute})";
            }
            else
            {
                _megaWaveSurgeText.text = "Normal Spawn Pace";
            }
        }

        if (_waveKillsText != null)
        {
            _waveKillsText.text = $"⚔️ {waveStatus.EnemiesKilled} KILLS";
        }

        if (_wavePauseStatusText != null)
        {
            _wavePauseStatusText.text = waveStatus.IsTimerPaused ? "Clock: ⏸️ PAUSED" : "Clock: ▶️ RUNNING";
            _wavePauseStatusText.color = waveStatus.IsTimerPaused ? new Color(1f, 0.6f, 0.2f) : new Color(0.4f, 1f, 0.6f);
        }
    }

    public void UpdateLanguage()
    {
        if (_headerText != null) _headerText.text = GameLocalization.DevPanelHeader;
        if (_maxAllBtnText != null) _maxAllBtnText.text = GameLocalization.DevMaxAllSkills;

        if (_tabTexts != null && _tabTexts.Length >= 3)
        {
            if (_tabTexts[0] != null) _tabTexts[0].text = GameLocalization.DevTabSkills;
            if (_tabTexts[1] != null) _tabTexts[1].text = GameLocalization.DevTabWaves;
            if (_tabTexts[2] != null) _tabTexts[2].text = GameLocalization.DevTabCheats;
        }
    }

    #region --- Sub-Builders ---

    private void BuildSkillsScrollView(Transform parent, List<DevSkillInfo> skillInfos)
    {
        var scrollObj = new GameObject("SkillsScrollView");
        scrollObj.transform.SetParent(parent, false);
        var sRect = scrollObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0.5f);
        sRect.anchorMax = new Vector2(0.5f, 0.5f);
        sRect.anchoredPosition = new Vector2(0f, -40f);
        sRect.sizeDelta = new Vector2(850f, 380f);

        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 28f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var vRect = viewportObj.AddComponent<RectTransform>();
        vRect.anchorMin = Vector2.zero;
        vRect.anchorMax = Vector2.one;
        vRect.sizeDelta = Vector2.zero;

        var vImg = viewportObj.AddComponent<Image>();
        vImg.color = new Color(0f, 0f, 0f, 0.01f);
        var mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        scrollRect.viewport = vRect;

        // Content
        float rowHeight = 44f;
        float spacing = 6f;
        float totalHeight = (skillInfos.Count * (rowHeight + spacing)) + 20f;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var cRect = contentObj.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 1f);
        cRect.anchorMax = new Vector2(0.5f, 1f);
        cRect.pivot = new Vector2(0.5f, 1f);
        cRect.anchoredPosition = Vector2.zero;
        cRect.sizeDelta = new Vector2(830f, totalHeight);

        scrollRect.content = cRect;

        // Sinh các hàng kỹ năng
        float curY = -10f;
        foreach (var info in skillInfos)
        {
            if (info.IsFusion)
            {
                BuildFusionRow(contentObj.transform, info, curY);
            }
            else
            {
                BuildSkillRow(contentObj.transform, info, curY);
            }
            curY -= (rowHeight + spacing);
        }
    }

    private void BuildSkillRow(Transform parent, DevSkillInfo info, float yPos)
    {
        var rowObj = UIFactory.CreatePanel(
            parent,
            $"Row_{info.SkillId}",
            new Vector2(810f, 44f),
            new Color(0.1f, 0.13f, 0.18f, 0.9f),
            new Color(info.ThemeColor.r * 0.5f, info.ThemeColor.g * 0.5f, info.ThemeColor.b * 0.5f, 0.8f),
            new Vector2(1.5f, -1.5f)
        );
        var rRect = rowObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 1f);
        rRect.anchorMax = new Vector2(0.5f, 1f);
        rRect.pivot = new Vector2(0.5f, 1f);
        rRect.anchoredPosition = new Vector2(0f, yPos);

        // Tên kỹ năng
        var nameTxt = UIFactory.CreateText(
            rowObj.transform,
            "Name",
            info.DisplayName,
            15,
            info.ThemeColor,
            FontStyle.Bold,
            TextAnchor.MiddleLeft
        );
        var nRect = nameTxt.GetComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0f, 0.5f);
        nRect.pivot = new Vector2(0f, 0.5f);
        nRect.anchoredPosition = new Vector2(16f, 0f);
        nRect.sizeDelta = new Vector2(230f, 32f);

        // Level Display Text
        var lvlTxt = UIFactory.CreateText(
            rowObj.transform,
            "LevelText",
            "[ LV.0 ]",
            15,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
        var lRect = lvlTxt.GetComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0f, 0.5f);
        lRect.anchoredPosition = new Vector2(260f, 0f);
        lRect.sizeDelta = new Vector2(85f, 32f);

        _skillLevelTexts[info.SkillId] = lvlTxt;

        // Nút [-]
        CreateSmallButton(rowObj.transform, new Vector2(355f, 0f), "—", () => OnSkillLevelDelta?.Invoke(info.SkillId, -1));

        // Nút [+]
        CreateSmallButton(rowObj.transform, new Vector2(398f, 0f), "+", () => OnSkillLevelDelta?.Invoke(info.SkillId, 1));

        // Các nút bấm trực tiếp [0..MaxLevel]
        float btnStartX = 465f;
        for (int i = 0; i <= info.MaxLevel; i++)
        {
            int lvl = i;
            CreateDirectLevelButton(rowObj.transform, new Vector2(btnStartX + i * 46f, 0f), lvl, () => OnSkillDirectLevel?.Invoke(info.SkillId, lvl));
        }
    }

    private void BuildFusionRow(Transform parent, DevSkillInfo info, float yPos)
    {
        var rowObj = UIFactory.CreatePanel(
            parent,
            $"Row_{info.SkillId}",
            new Vector2(810f, 44f),
            new Color(0.14f, 0.12f, 0.05f, 0.95f),
            new Color(1f, 0.85f, 0.15f, 0.8f),
            new Vector2(1.5f, -1.5f)
        );
        var rRect = rowObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 1f);
        rRect.anchorMax = new Vector2(0.5f, 1f);
        rRect.pivot = new Vector2(0.5f, 1f);
        rRect.anchoredPosition = new Vector2(0f, yPos);

        // Tên Hợp Thể
        var nameTxt = UIFactory.CreateText(
            rowObj.transform,
            "Name",
            info.DisplayName,
            15,
            new Color(1f, 0.85f, 0.2f),
            FontStyle.Bold,
            TextAnchor.MiddleLeft
        );
        var nRect = nameTxt.GetComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0f, 0.5f);
        nRect.pivot = new Vector2(0f, 0.5f);
        nRect.anchoredPosition = new Vector2(16f, 0f);
        nRect.sizeDelta = new Vector2(260f, 32f);

        // State Text
        var stTxt = UIFactory.CreateText(
            rowObj.transform,
            "StateText",
            "[ DISABLED ]",
            14,
            new Color(1f, 0.85f, 0.15f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
        var sRect = stTxt.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 0.5f);
        sRect.anchorMax = new Vector2(0f, 0.5f);
        sRect.pivot = new Vector2(0f, 0.5f);
        sRect.anchoredPosition = new Vector2(340f, 0f);
        sRect.sizeDelta = new Vector2(140f, 32f);

        _skillLevelTexts[info.SkillId] = stTxt;

        // Toggle Button
        var toggleBtn = UIFactory.CreateButton(
            rowObj.transform,
            "Btn_ToggleFusion",
            new Vector2(250f, 32f),
            new Color(0.4f, 0.3f, 0.05f, 1f),
            new Color(1f, 0.9f, 0.35f),
            () => OnToggleFusion?.Invoke(info.SkillId)
        );
        var tRect = toggleBtn.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0.5f);
        tRect.anchorMax = new Vector2(0.5f, 0.5f);
        tRect.anchoredPosition = new Vector2(240f, 0f);

        UIFactory.CreateText(
            toggleBtn.transform,
            "Text",
            "⚡ TOGGLE FUSION",
            13,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
    }

    private void BuildCheatButton(Transform parent, DevAction action, Vector2 pos, Vector2 size)
    {
        var btn = UIFactory.CreateButton(
            parent,
            $"Btn_{action.ActionId}",
            size,
            action.NormalColor,
            action.BorderColor,
            () => action.ExecuteAction?.Invoke(),
            new Vector2(1.5f, -1.5f)
        );
        var rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;

        var txt = UIFactory.CreateText(
            btn.transform,
            "Text",
            action.LabelGetter(),
            13,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
        _actionStateTexts[action.ActionId] = txt;
    }

    private void CreateSmallButton(Transform parent, Vector2 pos, string label, Action onClick)
    {
        var btn = UIFactory.CreateButton(
            parent,
            $"Btn_{label}",
            new Vector2(34f, 32f),
            new Color(0.18f, 0.22f, 0.3f, 1f),
            new Color(0.45f, 0.65f, 0.85f),
            () => onClick?.Invoke(),
            new Vector2(1f, -1f)
        );
        var rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = pos;

        UIFactory.CreateText(
            btn.transform,
            "Text",
            label,
            16,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
    }

    private void CreateDirectLevelButton(Transform parent, Vector2 pos, int level, Action onClick)
    {
        Color bgColor = level == 0 ? new Color(0.18f, 0.18f, 0.2f, 1f) : new Color(0.15f, 0.25f, 0.35f, 1f);
        Color borderColor = level == 0 ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.35f, 0.75f, 1f);

        var btn = UIFactory.CreateButton(
            parent,
            $"Btn_Lv{level}",
            new Vector2(38f, 32f),
            bgColor,
            borderColor,
            () => onClick?.Invoke(),
            new Vector2(1f, -1f)
        );
        var rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = pos;

        UIFactory.CreateText(
            btn.transform,
            "Text",
            level == 0 ? "OFF" : $"L{level}",
            13,
            level == 0 ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.85f, 0.95f, 1f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
    }

    private static Text CreatePositionedText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        Color color,
        FontStyle style,
        TextAnchor alignment,
        Vector2 pos,
        Vector2 size)
    {
        var txt = UIFactory.CreateText(parent, name, content, fontSize, color, style, alignment);
        var rect = txt.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return txt;
    }

    #endregion
}

#endif
