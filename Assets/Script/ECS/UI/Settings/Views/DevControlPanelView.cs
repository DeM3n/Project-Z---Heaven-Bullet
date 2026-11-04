using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR || DEBUG

/// <summary>
/// Quản lý giao diện Bảng điều khiển Lập trình viên (Developer Control Panel):
/// - Sinh động các hàng kỹ năng từ danh sách DevSkillInfo (Data-driven).
/// - Sinh động các nút Cheat từ danh sách DevAction.
/// - Tuyệt đối không chứa bất kỳ logic hoặc tham chiếu nào đến ECS (0 ECS leakage).
/// </summary>
public class DevControlPanelView
{
    private readonly GameObject _panelObj;
    private readonly Text _headerText;
    private readonly Dictionary<string, Text> _skillLevelTexts = new Dictionary<string, Text>();
    private readonly Dictionary<string, Text> _actionStateTexts = new Dictionary<string, Text>();

    public event Action<string, int> OnSkillLevelDelta;
    public event Action<string, int> OnSkillDirectLevel;
    public event Action<string> OnToggleFusion;
    public event Action OnMaxAllSkills;
    public event Action OnCloseClicked;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    public DevControlPanelView(Transform canvasParent, List<DevSkillInfo> skillInfos, List<DevAction> devActions)
    {
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "Dev_Panel",
            new Color(0.01f, 0.02f, 0.04f, 0.92f)
        );

        // Khung trung tâm
        var boxObj = UIFactory.CreatePanel(
            _panelObj.transform,
            "DevBox",
            new Vector2(760f, 710f),
            new Color(0.06f, 0.08f, 0.12f, 0.98f),
            HUDTheme.PurpleBorder,
            new Vector2(3f, -3f),
            true
        );
        var bRect = boxObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.pivot = new Vector2(0.5f, 0.5f);

        // Header Title
        var headerObj = new GameObject("Header");
        headerObj.transform.SetParent(boxObj.transform, false);
        var hRect = headerObj.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0.5f, 0.94f);
        hRect.anchorMax = new Vector2(0.5f, 0.94f);
        hRect.sizeDelta = new Vector2(700f, 36f);

        _headerText = headerObj.AddComponent<Text>();
        _headerText.font = HUDTheme.PixelFont;
        _headerText.fontSize = 22;
        _headerText.fontStyle = FontStyle.Bold;
        _headerText.alignment = TextAnchor.MiddleCenter;
        _headerText.color = HUDTheme.PurpleAccent;
        _headerText.text = GameLocalization.DevPanelHeader;

        // =====================================================================
        // SECTION 1: CÁC HÀNG KỸ NĂNG (SINH ĐỘNG TỪ DATA)
        // =====================================================================
        float curY = 220f;
        foreach (var info in skillInfos)
        {
            if (info.IsFusion)
            {
                BuildFusionRow(boxObj.transform, info, curY);
            }
            else
            {
                BuildSkillRow(boxObj.transform, info, curY);
            }
            curY -= 51f;
        }

        // Nút MAX ALL SKILLS
        var maxAllBtn = UIFactory.CreateButton(
            boxObj.transform,
            "Btn_MaxAll",
            new Vector2(500f, 40f),
            new Color(0.4f, 0.3f, 0.05f, 1f),
            HUDTheme.GoldBorder,
            () => OnMaxAllSkills?.Invoke()
        );
        var mRect = maxAllBtn.GetComponent<RectTransform>();
        mRect.anchorMin = new Vector2(0.5f, 0.5f);
        mRect.anchorMax = new Vector2(0.5f, 0.5f);
        mRect.anchoredPosition = new Vector2(0f, curY);

        UIFactory.CreateText(
            maxAllBtn.transform,
            "Text",
            GameLocalization.DevMaxAllSkills,
            15,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        // =====================================================================
        // SECTION 2: CÁC NÚT CHEATS TIỆN ÍCH (SINH ĐỘNG TỪ DEVACTION)
        // =====================================================================
        curY -= 54f;
        float cheatBtnWidth = 220f;
        float cheatBtnHeight = 38f;
        Vector2 cheatSize = new Vector2(cheatBtnWidth, cheatBtnHeight);

        // Hàng 1 (3 nút đầu tiên)
        float[] row1X = { -240f, 0f, 240f };
        for (int i = 0; i < Mathf.Min(3, devActions.Count); i++)
        {
            var action = devActions[i];
            BuildCheatButton(boxObj.transform, action, new Vector2(row1X[i], curY), cheatSize);
        }

        // Hàng 2 (các nút tiếp theo + Nút Đóng)
        curY -= 46f;
        for (int i = 3; i < Mathf.Min(5, devActions.Count); i++)
        {
            var action = devActions[i];
            BuildCheatButton(boxObj.transform, action, new Vector2(row1X[i - 3], curY), cheatSize);
        }

        // Nút Đóng (Close) ở góc phải hàng 2
        var closeBtn = UIFactory.CreateButton(
            boxObj.transform,
            "Btn_Close",
            cheatSize,
            new Color(0.25f, 0.25f, 0.28f, 1f),
            new Color(0.85f, 0.85f, 0.95f),
            () => OnCloseClicked?.Invoke()
        );
        var cRect = closeBtn.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = new Vector2(240f, curY);

        UIFactory.CreateText(
            closeBtn.transform,
            "Text",
            GameLocalization.CloseButton,
            15,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

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

    /// <summary>
    /// Làm mới toàn bộ text hiển thị cấp độ kỹ năng và trạng thái cheat (Data-driven).
    /// </summary>
    public void Refresh(List<DevSkillState> states, List<DevAction> actions)
    {
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

        foreach (var action in actions)
        {
            if (_actionStateTexts.TryGetValue(action.ActionId, out var txt))
            {
                txt.text = action.LabelGetter();
            }
        }
    }

    public void UpdateLanguage()
    {
        if (_headerText != null) _headerText.text = GameLocalization.DevPanelHeader;
    }

    private void BuildSkillRow(Transform parent, DevSkillInfo info, float yPos)
    {
        var rowObj = UIFactory.CreatePanel(
            parent,
            $"Row_{info.SkillId}",
            new Vector2(700f, 44f),
            new Color(0.1f, 0.13f, 0.18f, 0.9f),
            new Color(info.ThemeColor.r * 0.5f, info.ThemeColor.g * 0.5f, info.ThemeColor.b * 0.5f, 0.8f),
            new Vector2(1.5f, -1.5f)
        );
        var rRect = rowObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.anchoredPosition = new Vector2(0f, yPos);

        // Tên kỹ năng
        var nameTxt = UIFactory.CreateText(
            rowObj.transform,
            "Name",
            info.DisplayName,
            16,
            info.ThemeColor,
            FontStyle.Bold,
            TextAnchor.MiddleLeft
        );
        var nRect = nameTxt.GetComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0f, 0.5f);
        nRect.pivot = new Vector2(0f, 0.5f);
        nRect.anchoredPosition = new Vector2(16f, 0f);
        nRect.sizeDelta = new Vector2(190f, 32f);

        // Level Display Text
        var lvlTxt = UIFactory.CreateText(
            rowObj.transform,
            "LevelText",
            "[ LV.0 ]",
            16,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
        var lRect = lvlTxt.GetComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0.5f, 0.5f);
        lRect.anchoredPosition = new Vector2(250f, 0f);
        lRect.sizeDelta = new Vector2(80f, 32f);

        _skillLevelTexts[info.SkillId] = lvlTxt;

        // Nút [-]
        CreateSmallButton(rowObj.transform, new Vector2(310f, 0f), "—", () => OnSkillLevelDelta?.Invoke(info.SkillId, -1));

        // Nút [+]
        CreateSmallButton(rowObj.transform, new Vector2(352f, 0f), "+", () => OnSkillLevelDelta?.Invoke(info.SkillId, 1));

        // Các nút bấm trực tiếp [0..MaxLevel] (Sinh động từ MaxLevel)
        float btnStartX = 415f;
        for (int i = 0; i <= info.MaxLevel; i++)
        {
            int lvl = i;
            CreateDirectLevelButton(rowObj.transform, new Vector2(btnStartX + i * 44f, 0f), lvl, () => OnSkillDirectLevel?.Invoke(info.SkillId, lvl));
        }
    }

    private void BuildFusionRow(Transform parent, DevSkillInfo info, float yPos)
    {
        var rowObj = UIFactory.CreatePanel(
            parent,
            $"Row_{info.SkillId}",
            new Vector2(700f, 44f),
            new Color(0.14f, 0.12f, 0.05f, 0.95f),
            new Color(1f, 0.85f, 0.15f, 0.8f),
            new Vector2(1.5f, -1.5f)
        );
        var rRect = rowObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.anchoredPosition = new Vector2(0f, yPos);

        // Tên Hợp Thể
        var nameTxt = UIFactory.CreateText(
            rowObj.transform,
            "Name",
            info.DisplayName,
            16,
            new Color(1f, 0.85f, 0.2f),
            FontStyle.Bold,
            TextAnchor.MiddleLeft
        );
        var nRect = nameTxt.GetComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0f, 0.5f);
        nRect.pivot = new Vector2(0f, 0.5f);
        nRect.anchoredPosition = new Vector2(16f, 0f);
        nRect.sizeDelta = new Vector2(210f, 32f);

        // State Text
        var stTxt = UIFactory.CreateText(
            rowObj.transform,
            "StateText",
            "[ DISABLED ]",
            15,
            new Color(1f, 0.85f, 0.15f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
        var sRect = stTxt.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 0.5f);
        sRect.anchorMax = new Vector2(0f, 0.5f);
        sRect.pivot = new Vector2(0.5f, 0.5f);
        sRect.anchoredPosition = new Vector2(300f, 0f);
        sRect.sizeDelta = new Vector2(130f, 32f);

        _skillLevelTexts[info.SkillId] = stTxt;

        // Toggle Button
        var toggleBtn = UIFactory.CreateButton(
            rowObj.transform,
            "Btn_ToggleFusion",
            new Vector2(230f, 34f),
            new Color(0.4f, 0.3f, 0.05f, 1f),
            new Color(1f, 0.9f, 0.35f),
            () => OnToggleFusion?.Invoke(info.SkillId)
        );
        var tRect = toggleBtn.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0.5f);
        tRect.anchorMax = new Vector2(0.5f, 0.5f);
        tRect.anchoredPosition = new Vector2(180f, 0f);

        UIFactory.CreateText(
            toggleBtn.transform,
            "Text",
            "⚡ TOGGLE FUSION",
            14,
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
            () => action.ExecuteAction?.Invoke()
        );
        var rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;

        var txt = UIFactory.CreateText(
            btn.transform,
            "Text",
            action.LabelGetter(),
            15,
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
        rect.pivot = new Vector2(0.5f, 0.5f);
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
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;

        UIFactory.CreateText(
            btn.transform,
            "Text",
            level == 0 ? "OFF" : $"L{level}",
            14,
            level == 0 ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.85f, 0.95f, 1f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
    }
}

#endif
