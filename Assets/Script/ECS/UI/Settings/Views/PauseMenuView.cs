using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Các tab hiển thị bên trong Pause Menu:
/// - Attributes: Bảng chỉ số người chơi 2 cột (chỉ hiển thị những chỉ số thực tế trong game).
/// - DamageStats: Bảng tổng quan kỹ năng (Magics / Skills Overview).
/// - Artifacts: Bảng danh sách Cổ Vật Bị Động đã nhặt được.
/// - Abandon: Bảng xác nhận bỏ cuộc / chơi lại / về trang chủ.
/// </summary>
public enum PauseMenuTab
{
    Attributes,
    DamageStats,
    Artifacts,
    Abandon
}

/// <summary>
/// Quản lý giao diện Popup Menu Tạm dừng trận đấu (Pause Menu):
/// Tái hiện chuẩn mỹ thuật từ ảnh reference, nhưng chỉ hiển thị các chỉ số THỰC TẾ đang có trong game:
/// - Tiêu đề Pause, Level, Thời gian sinh tồn, Số quái đã diệt.
/// - 2 cột chỉ số thực tế: Máu, Khiên, Tự hồi máu, Giảm sát thương, Tốc độ chạy, Hồi sinh,
///   Sát thương tổng, Bán kính hút ngọc, Máu hồi từ tim, Lượt đổi thẻ, Vận khí rơi đồ, Hệ số Linh thạch.
/// - Nút Tiếp tục (Resume Game) kích hoạt đếm ngược 3 giây.
/// - Các nút điều hướng: Magics, Artifacts, Abandon.
/// </summary>
public class PauseMenuView
{
    private readonly GameObject _panelObj;
    private readonly RectTransform _boxRect;

    // Header Components
    private readonly Text _titleText;
    private readonly Text _levelText;
    private readonly Text _survivalTimeText;
    private readonly Text _enemiesSlayedText;

    // Containers for Tabs
    private readonly GameObject _attributesContainer;
    private readonly GameObject _damageStatsContainer;
    private readonly GameObject _artifactsContainer;
    private readonly GameObject _abandonContainer;

    // Cột 1 (Left Column) - 6 chỉ số thực tế
    private readonly Text _hpValueText;
    private readonly Text _shieldValueText;
    private readonly Text _hpRegenValueText;
    private readonly Text _damageTakenValueText;
    private readonly Text _moveSpeedValueText;
    private readonly Text _revivalValueText;

    // Cột 2 (Right Column) - 6 chỉ số thực tế
    private readonly Text _attackValueText;
    private readonly Text _pickupRangeValueText;
    private readonly Text _lifeOrbRecoveryValueText;
    private readonly Text _rerollsValueText;
    private readonly Text _dropLuckValueText;
    private readonly Text _stoneMultiValueText;

    // Damage / Skill Stats UI Components
    private readonly Text _totalDamageSummaryText;
    private readonly Text _emptySkillsText;
    private readonly List<SkillRowUI> _skillRows = new List<SkillRowUI>();

    // Artifacts UI Components
    private readonly Text _emptyArtifactsText;
    private readonly List<ArtifactRowUI> _artifactRows = new List<ArtifactRowUI>();

    // Bottom Navigation Buttons
    private readonly Text _resumeBtnText;
    private readonly Text _magicsBtnText;
    private readonly Text _artifactsBtnText;
    private readonly Text _abandonBtnText;

    // Gear dropdown
    private readonly GameObject _gearMenuObj;

    // Events
    public event Action OnResumeClicked;
    public event Action OnRestartClicked;
    public event Action OnLanguageClicked;
    public event Action OnDevModeClicked;
    public event Action OnMainMenuClicked;

    private PauseMenuTab _currentTab = PauseMenuTab.Attributes;
    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    private class SkillRowUI
    {
        public GameObject Root;
        public RectTransform Rect;
        public Text TitleText;
        public Text StatusText;
        public RectTransform FillBar;
        public Image FillImage;
    }

    private class ArtifactRowUI
    {
        public GameObject Root;
        public RectTransform Rect;
        public Text NameText;
        public Text DescText;
    }

    public PauseMenuView(Transform canvasParent)
    {
        // 1. Fullscreen Dim Background
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "Pause_Panel",
            new Color(0.03f, 0.04f, 0.06f, 0.94f)
        );

        // 2. Khung Box trung tâm (kích thước chuẩn 490 x 630)
        var boxObj = UIFactory.CreatePanel(
            _panelObj.transform,
            "CenterBox",
            new Vector2(490f, 630f),
            new Color(0.05f, 0.06f, 0.09f, 0.98f),
            new Color(0.22f, 0.28f, 0.40f, 0.85f),
            new Vector2(2f, -2f),
            true
        );
        _boxRect = boxObj.GetComponent<RectTransform>();
        _boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        _boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        _boxRect.pivot = new Vector2(0.5f, 0.5f);

        // 3. Nút Bánh răng Cài đặt / Dev (Top-Right Gear Button ⚙)
        CreateGearButton(boxObj.transform);

        // 4. Tiêu đề chính: "Pause"
        var titleObj = new GameObject("PauseTitle");
        titleObj.transform.SetParent(boxObj.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0.93f);
        tRect.anchorMax = new Vector2(0.5f, 0.93f);
        tRect.pivot = new Vector2(0.5f, 0.5f);
        tRect.sizeDelta = new Vector2(300f, 44f);

        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = HUDTheme.PixelFont;
        _titleText.fontSize = 34;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = Color.white;
        _titleText.text = "Pause";

        // 5. Header Sub-info: Level, Survived Time, Enemies Slayed
        _levelText = CreateSubInfoText(boxObj.transform, new Vector2(0f, 230f), new Color(1f, 0.84f, 0.25f), 16);
        _survivalTimeText = CreateSubInfoText(boxObj.transform, new Vector2(0f, 206f), new Color(1f, 0.58f, 0.16f), 15);
        _enemiesSlayedText = CreateSubInfoText(boxObj.transform, new Vector2(0f, 183f), new Color(1f, 0.58f, 0.16f), 15);

        // 6. Khu vực nội dung các Tab (Middle Content Area: Y từ +160f đến -135f, cao 295f)
        var contentObj = new GameObject("MiddleContentArea");
        contentObj.transform.SetParent(boxObj.transform, false);
        var cRect = contentObj.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.sizeDelta = new Vector2(460f, 290f);
        cRect.anchoredPosition = new Vector2(0f, 15f);

        // --- TAB 1: ATTRIBUTES (CHỈ HIỂN THỊ CÁC CHỈ SỐ THỰC TẾ TRONG GAME) ---
        _attributesContainer = new GameObject("AttributesTab");
        _attributesContainer.transform.SetParent(contentObj.transform, false);
        var aRect = _attributesContainer.AddComponent<RectTransform>();
        aRect.anchorMin = Vector2.zero;
        aRect.anchorMax = Vector2.one;
        aRect.sizeDelta = Vector2.zero;

        BuildAttributesTab(
            _attributesContainer.transform,
            out _hpValueText,
            out _shieldValueText,
            out _hpRegenValueText,
            out _damageTakenValueText,
            out _moveSpeedValueText,
            out _revivalValueText,
            out _attackValueText,
            out _pickupRangeValueText,
            out _lifeOrbRecoveryValueText,
            out _rerollsValueText,
            out _dropLuckValueText,
            out _stoneMultiValueText
        );

        // --- TAB 2: MAGICS / SKILLS OVERVIEW ---
        _damageStatsContainer = new GameObject("DamageStatsTab");
        _damageStatsContainer.transform.SetParent(contentObj.transform, false);
        var dRect = _damageStatsContainer.AddComponent<RectTransform>();
        dRect.anchorMin = Vector2.zero;
        dRect.anchorMax = Vector2.one;
        dRect.sizeDelta = Vector2.zero;

        BuildDamageStatsTab(_damageStatsContainer.transform, out _totalDamageSummaryText, out _emptySkillsText);
        _damageStatsContainer.SetActive(false);

        // --- TAB 3: ARTIFACTS (CỔ VẬT BỊ ĐỘNG) ---
        _artifactsContainer = new GameObject("ArtifactsTab");
        _artifactsContainer.transform.SetParent(contentObj.transform, false);
        var artRect = _artifactsContainer.AddComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.sizeDelta = Vector2.zero;

        BuildArtifactsTab(_artifactsContainer.transform, out _emptyArtifactsText);
        _artifactsContainer.SetActive(false);

        // --- TAB 4: ABANDON CONFIRMATION ---
        _abandonContainer = new GameObject("AbandonTab");
        _abandonContainer.transform.SetParent(contentObj.transform, false);
        var abRect = _abandonContainer.AddComponent<RectTransform>();
        abRect.anchorMin = Vector2.zero;
        abRect.anchorMax = Vector2.one;
        abRect.sizeDelta = Vector2.zero;

        BuildAbandonTab(_abandonContainer.transform);
        _abandonContainer.SetActive(false);

        // 7. Nút Tiếp tục chính: "Resume Game" (Kích hoạt đếm ngược 3 giây)
        CreateResumeButton(boxObj.transform, new Vector2(0f, -190f), new Vector2(280f, 44f), out _resumeBtnText);

        // 8. Hàng 3 nút điều hướng dưới cùng: Magics, Artifacts, Abandon
        BuildBottomNavBar(boxObj.transform, new Vector2(0f, -255f), out _magicsBtnText, out _artifactsBtnText, out _abandonBtnText);

        // 9. Menu tùy chọn khi bấm Bánh răng (Gear Dropdown Menu)
        _gearMenuObj = BuildGearDropdown(boxObj.transform);

        _panelObj.SetActive(false);
    }

    public void Show()
    {
        if (_panelObj != null)
        {
            _panelObj.SetActive(true);
            _gearMenuObj?.SetActive(false);
            SetTab(PauseMenuTab.Attributes);
            RefreshData();
        }
    }

    public void Hide()
    {
        if (_panelObj != null)
        {
            _gearMenuObj?.SetActive(false);
            _panelObj.SetActive(false);
        }
    }

    public void RefreshData()
    {
        var snapshot = PauseMenuDataProvider.Capture();

        // 1. Cập nhật thông tin Header
        if (_levelText != null) _levelText.text = $"Current Level : {snapshot.Level}";
        if (_survivalTimeText != null) _survivalTimeText.text = $"Survived Time {snapshot.FormattedSurvivalTime}";
        if (_enemiesSlayedText != null) _enemiesSlayedText.text = $"Enemies Slayed {snapshot.EnemiesKilled}";

        // 2. Cập nhật Tab Attributes (Chỉ các chỉ số thực tế trong game)
        // Cột 1 (Trái): Sinh tồn & Phòng thủ
        if (_hpValueText != null) _hpValueText.text = $"{(int)snapshot.CurrentHP} / {(int)snapshot.MaxHP}";

        if (_shieldValueText != null)
        {
            if (snapshot.HasShield)
            {
                if (snapshot.ShieldCharges > 0)
                    _shieldValueText.text = $"x{snapshot.ShieldCharges}";
                else
                    _shieldValueText.text = $"{snapshot.ShieldRechargeSec:F0}s";
            }
            else
            {
                _shieldValueText.text = "None";
            }
        }

        if (_hpRegenValueText != null) _hpRegenValueText.text = (snapshot.HpRegenPerSec > 0f) ? $"+{snapshot.HpRegenPerSec:F1}/s" : "0/s";
        if (_damageTakenValueText != null) _damageTakenValueText.text = (snapshot.DamageReductionPercent > 0f) ? $"-{snapshot.DamageReductionPercent:F0}%" : "+0%";
        if (_moveSpeedValueText != null) _moveSpeedValueText.text = $"{snapshot.MoveSpeedPercent:F0}% ({snapshot.MoveSpeed:F1}m/s)";
        if (_revivalValueText != null) _revivalValueText.text = (snapshot.HasRevival) ? (snapshot.CanRevive ? "1 Ready" : "Used") : "None";

        // Cột 2 (Phải): Tấn công & Tiện ích
        if (_attackValueText != null) _attackValueText.text = $"x{snapshot.GlobalDamageMultiplier:F2} ({snapshot.GlobalDamageMultiplier * 100:F0}%)";
        if (_pickupRangeValueText != null) _pickupRangeValueText.text = $"{snapshot.PickupRange:F1}m (+{snapshot.PickupBonusPercent:F0}%)";
        if (_lifeOrbRecoveryValueText != null) _lifeOrbRecoveryValueText.text = "+25 HP";
        if (_rerollsValueText != null) _rerollsValueText.text = $"+{snapshot.BonusRerolls}";
        if (_dropLuckValueText != null) _dropLuckValueText.text = $"+{snapshot.DropRateBonusPercent:F0}%";
        if (_stoneMultiValueText != null) _stoneMultiValueText.text = $"x{snapshot.GoldMultiplier:F2}";

        // 3. Cập nhật Tab Magics (Thống kê Sát thương & DPS Kỹ năng)
        if (_totalDamageSummaryText != null)
        {
            _totalDamageSummaryText.text = $"TOTAL DMG: <color=#ffd54f>{snapshot.TotalDamage:N0}</color>   |   DPS: <color=#00e5ff>{snapshot.TotalDps:F1}</color>   |   TIME: <color=#ffffff>{snapshot.FormattedSurvivalTime}</color>";
        }

        float startY = 100f;
        float spacingY = 38f;
        int rowIdx = 0;

        for (int i = 0; i < snapshot.SkillStats.Count; i++)
        {
            var stat = snapshot.SkillStats[i];
            // Chỉ hiển thị các kỹ năng đã học (Level > 0), không hiển thị kỹ năng chưa học
            if (stat.Level <= 0) continue;

            if (rowIdx < _skillRows.Count)
            {
                var row = _skillRows[rowIdx];
                row.Root.SetActive(true);
                row.Rect.anchoredPosition = new Vector2(0f, startY - rowIdx * spacingY);

                string lvlBadge = (stat.Level >= stat.MaxLevel) ? "<color=#ffd54f>[MAX]</color>" : $"<color=#00e5ff>[Lv.{stat.Level}]</color>";
                row.TitleText.text = $"{stat.Title} {lvlBadge}";
                row.TitleText.color = stat.ThemeColor;
                if (row.FillImage != null) row.FillImage.color = stat.ThemeColor;

                if (stat.Damage > 0f)
                {
                    row.StatusText.text = $"<color=#ffffff>{stat.Damage:N0}</color> <color=#ffd54f>({stat.Percentage:F1}%)</color>";
                    float ratio = Mathf.Clamp01(stat.Percentage / 100f);
                    row.FillBar.sizeDelta = new Vector2(Mathf.Max(6f, 430f * ratio), 6f);
                }
                else
                {
                    row.StatusText.text = "<color=#aaaaaa>0 DMG (Active)</color>";
                    row.FillBar.sizeDelta = new Vector2(4f, 6f);
                }

                rowIdx++;
            }
        }

        if (_emptySkillsText != null) _emptySkillsText.gameObject.SetActive(rowIdx == 0);

        // Tắt toàn bộ các hàng kỹ năng chưa dùng
        for (int i = rowIdx; i < _skillRows.Count; i++)
        {
            _skillRows[i].Root.SetActive(false);
        }

        // 4. Cập nhật Tab Artifacts (Cổ Vật) - Chỉ hiển thị Cổ vật đã sở hữu
        float aStartY = 100f;
        float aSpacingY = 52f;
        int aRowIdx = 0;

        for (int i = 0; i < snapshot.Relics.Count; i++)
        {
            var relic = snapshot.Relics[i];
            if (relic.Level <= 0) continue;

            if (aRowIdx < _artifactRows.Count)
            {
                var row = _artifactRows[aRowIdx];
                row.Root.SetActive(true);
                row.Rect.anchoredPosition = new Vector2(0f, aStartY - aRowIdx * aSpacingY);
                row.NameText.text = $"{relic.Name}  <color=#ffd54f>Lv.{relic.Level}</color>";
                row.DescText.text = relic.Description;
                aRowIdx++;
            }
        }

        if (_emptyArtifactsText != null) _emptyArtifactsText.gameObject.SetActive(aRowIdx == 0);

        for (int i = aRowIdx; i < _artifactRows.Count; i++)
        {
            _artifactRows[i].Root.SetActive(false);
        }
    }

    public void SetTab(PauseMenuTab tab)
    {
        _currentTab = tab;
        _attributesContainer.SetActive(tab == PauseMenuTab.Attributes);
        _damageStatsContainer.SetActive(tab == PauseMenuTab.DamageStats);
        _artifactsContainer.SetActive(tab == PauseMenuTab.Artifacts);
        _abandonContainer.SetActive(tab == PauseMenuTab.Abandon);

        // Highlight màu các nút thanh điều hướng dưới
        if (_magicsBtnText != null)
        {
            _magicsBtnText.color = (tab == PauseMenuTab.DamageStats) ? new Color(1f, 0.85f, 0.25f) : Color.white;
            _magicsBtnText.text = (tab == PauseMenuTab.DamageStats) ? "✦ Stats" : "Magics";
        }
        if (_artifactsBtnText != null)
        {
            _artifactsBtnText.color = (tab == PauseMenuTab.Artifacts) ? new Color(1f, 0.85f, 0.25f) : Color.white;
            _artifactsBtnText.text = (tab == PauseMenuTab.Artifacts) ? "✦ Stats" : "Artifacts";
        }
        if (_abandonBtnText != null)
        {
            _abandonBtnText.color = (tab == PauseMenuTab.Abandon) ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.45f, 0.45f);
            _abandonBtnText.text = (tab == PauseMenuTab.Abandon) ? "✦ Stats" : "Abandon";
        }
    }

    public void UpdateLanguage()
    {
        RefreshData();
    }

    #region --- Xây dựng giao diện các Tab ---

    private void BuildAttributesTab(
        Transform parent,
        out Text hpVal, out Text shieldVal, out Text hpRegenVal, out Text dmgTakenVal, out Text moveSpeedVal, out Text revivalVal,
        out Text atkVal, out Text pickupVal, out Text lifeOrbVal, out Text rerollsVal, out Text dropLuckVal, out Text stoneMultiVal)
    {
        float leftX = -118f;
        float rightX = 118f;
        float startY = 120f;
        float rowSpacing = 38f;
        Vector2 rowSize = new Vector2(215f, 30f);

        // CỘT 1 (TRÁI): SINH TỒN & PHÒNG THỦ (6 chỉ số thực tế trong game)
        CreateStatRow(parent, new Vector2(leftX, startY), rowSize, "<color=#ff4d4d>❤</color> HP", out hpVal);
        CreateStatRow(parent, new Vector2(leftX, startY - rowSpacing), rowSize, "<color=#00e5ff>🛡</color> Shield", out shieldVal);
        CreateStatRow(parent, new Vector2(leftX, startY - rowSpacing * 2), rowSize, "<color=#4caf50>✚</color> HP Regen", out hpRegenVal);
        CreateStatRow(parent, new Vector2(leftX, startY - rowSpacing * 3), rowSize, "<color=#64b5f6>🛡</color> Damage Taken", out dmgTakenVal);
        CreateStatRow(parent, new Vector2(leftX, startY - rowSpacing * 4), rowSize, "<color=#e0e0e0>🌀</color> Move Speed", out moveSpeedVal);
        CreateStatRow(parent, new Vector2(leftX, startY - rowSpacing * 5), rowSize, "<color=#b388ff>🕊</color> Phoenix Rebirth", out revivalVal);

        // CỘT 2 (PHẢI): SỨC MẠNH & TIỆN ÍCH (6 chỉ số thực tế trong game)
        CreateStatRow(parent, new Vector2(rightX, startY), rowSize, "<color=#ffd54f>⚔</color> Global ATK", out atkVal);
        CreateStatRow(parent, new Vector2(rightX, startY - rowSpacing), rowSize, "<color=#ffca28>🔍</color> Pickup Range", out pickupVal);
        CreateStatRow(parent, new Vector2(rightX, startY - rowSpacing * 2), rowSize, "<color=#ff4081>💖</color> Heart Recovery", out lifeOrbVal);
        lifeOrbVal.color = new Color(0.4f, 1f, 0.4f); // Số +25 HP màu xanh lá
        CreateStatRow(parent, new Vector2(rightX, startY - rowSpacing * 3), rowSize, "<color=#ba68c8>🎲</color> Bonus Rerolls", out rerollsVal);
        CreateStatRow(parent, new Vector2(rightX, startY - rowSpacing * 4), rowSize, "<color=#76ff03>🍀</color> Loot Drop Luck", out dropLuckVal);
        CreateStatRow(parent, new Vector2(rightX, startY - rowSpacing * 5), rowSize, "<color=#ffd54f>💎</color> Spirit Multiplier", out stoneMultiVal);
    }

    private void CreateStatRow(Transform parent, Vector2 pos, Vector2 size, string label, out Text valueText)
    {
        var rowObj = new GameObject($"StatRow_{label}");
        rowObj.transform.SetParent(parent, false);
        var rRect = rowObj.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.pivot = new Vector2(0.5f, 0.5f);
        rRect.anchoredPosition = pos;
        rRect.sizeDelta = size;

        // Label phía bên trái
        var lObj = new GameObject("Label");
        lObj.transform.SetParent(rowObj.transform, false);
        var lRect = lObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0.65f, 0.5f);
        lRect.pivot = new Vector2(0f, 0.5f);
        lRect.anchoredPosition = Vector2.zero;
        lRect.sizeDelta = new Vector2(0f, size.y);

        var lbl = lObj.AddComponent<Text>();
        lbl.font = HUDTheme.PixelFont;
        lbl.fontSize = 13;
        lbl.fontStyle = FontStyle.Normal;
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.color = new Color(0.92f, 0.92f, 0.95f, 1f);
        lbl.text = label;

        // Value phía bên phải
        var vObj = new GameObject("Value");
        vObj.transform.SetParent(rowObj.transform, false);
        var vRect = vObj.AddComponent<RectTransform>();
        vRect.anchorMin = new Vector2(0.65f, 0.5f);
        vRect.anchorMax = new Vector2(1f, 0.5f);
        vRect.pivot = new Vector2(1f, 0.5f);
        vRect.anchoredPosition = Vector2.zero;
        vRect.sizeDelta = new Vector2(0f, size.y);

        valueText = vObj.AddComponent<Text>();
        valueText.font = HUDTheme.PixelFont;
        valueText.fontSize = 13;
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.color = Color.white;
        valueText.text = "-";
    }

    private void BuildDamageStatsTab(Transform parent, out Text summaryText, out Text emptyText)
    {
        // 1. Header tóm tắt kỹ năng
        var sumObj = new GameObject("TotalDmgHeader");
        sumObj.transform.SetParent(parent, false);
        var sRect = sumObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0.95f);
        sRect.anchorMax = new Vector2(0.5f, 0.95f);
        sRect.pivot = new Vector2(0.5f, 0.5f);
        sRect.sizeDelta = new Vector2(430f, 26f);

        summaryText = sumObj.AddComponent<Text>();
        summaryText.font = HUDTheme.PixelFont;
        summaryText.fontSize = 14;
        summaryText.fontStyle = FontStyle.Bold;
        summaryText.alignment = TextAnchor.MiddleCenter;
        summaryText.color = Color.white;

        // Dòng thông báo khi chưa học kỹ năng nào
        var emptyObj = new GameObject("EmptySkillsNotice");
        emptyObj.transform.SetParent(parent, false);
        var eRect = emptyObj.AddComponent<RectTransform>();
        eRect.anchorMin = new Vector2(0.5f, 0.5f);
        eRect.anchorMax = new Vector2(0.5f, 0.5f);
        eRect.pivot = new Vector2(0.5f, 0.5f);
        eRect.sizeDelta = new Vector2(400f, 40f);
        eRect.anchoredPosition = new Vector2(0f, 20f);

        emptyText = emptyObj.AddComponent<Text>();
        emptyText.font = HUDTheme.PixelFont;
        emptyText.fontSize = 14;
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.color = new Color(0.6f, 0.65f, 0.75f);
        emptyText.text = "No active skills learned yet.";
        emptyObj.SetActive(false);

        // 2. Danh sách 7 hàng kỹ năng
        float startY = 100f;
        float spacingY = 38f;

        for (int i = 0; i < SkillDefinitionRegistry.AllSkills.Count; i++)
        {
            var def = SkillDefinitionRegistry.AllSkills[i];
            var rowObj = new GameObject($"SkillRow_{def.SkillId}");
            rowObj.transform.SetParent(parent, false);
            var rRect = rowObj.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.5f, 0.5f);
            rRect.anchorMax = new Vector2(0.5f, 0.5f);
            rRect.pivot = new Vector2(0.5f, 0.5f);
            rRect.anchoredPosition = new Vector2(0f, startY - i * spacingY);
            rRect.sizeDelta = new Vector2(430f, 34f);

            // Tên Kỹ Năng (Trái)
            var titleObj = new GameObject("SkillTitle");
            titleObj.transform.SetParent(rowObj.transform, false);
            var tRect = titleObj.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0.65f);
            tRect.anchorMax = new Vector2(0.54f, 0.65f);
            tRect.pivot = new Vector2(0f, 0.5f);
            tRect.sizeDelta = new Vector2(0f, 22f);

            var tText = titleObj.AddComponent<Text>();
            tText.font = HUDTheme.PixelFont;
            tText.fontSize = 13;
            tText.fontStyle = FontStyle.Bold;
            tText.alignment = TextAnchor.MiddleLeft;
            tText.color = def.ThemeColor;

            // Trạng thái (Phải)
            var statObj = new GameObject("StatusText");
            statObj.transform.SetParent(rowObj.transform, false);
            var dRect = statObj.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.54f, 0.65f);
            dRect.anchorMax = new Vector2(1f, 0.65f);
            dRect.pivot = new Vector2(1f, 0.5f);
            dRect.sizeDelta = new Vector2(0f, 22f);

            var dText = statObj.AddComponent<Text>();
            dText.font = HUDTheme.PixelFont;
            dText.fontSize = 13;
            dText.fontStyle = FontStyle.Normal;
            dText.alignment = TextAnchor.MiddleRight;
            dText.color = new Color(0.85f, 0.9f, 0.95f);

            // Thanh Progress Bar Nền
            var barBgObj = new GameObject("BarBg");
            barBgObj.transform.SetParent(rowObj.transform, false);
            var bgRect = barBgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.15f);
            bgRect.anchorMax = new Vector2(1f, 0.15f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(0f, 0f);
            bgRect.sizeDelta = new Vector2(0f, 6f);
            var bgImg = barBgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.15f, 0.22f, 0.9f);

            // Thanh Progress Bar Fill (Màu theo theme kỹ năng)
            var barFillObj = new GameObject("BarFill");
            barFillObj.transform.SetParent(barBgObj.transform, false);
            var fillRect = barFillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 6f);
            var fillImg = barFillObj.AddComponent<Image>();
            fillImg.color = def.ThemeColor;

            _skillRows.Add(new SkillRowUI
            {
                Root = rowObj,
                Rect = rRect,
                TitleText = tText,
                StatusText = dText,
                FillBar = fillRect,
                FillImage = fillImg
            });
        }
    }

    private void BuildArtifactsTab(Transform parent, out Text emptyText)
    {
        var emptyObj = new GameObject("EmptyArtifactsNotice");
        emptyObj.transform.SetParent(parent, false);
        var eRect = emptyObj.AddComponent<RectTransform>();
        eRect.anchorMin = new Vector2(0.5f, 0.5f);
        eRect.anchorMax = new Vector2(0.5f, 0.5f);
        eRect.pivot = new Vector2(0.5f, 0.5f);
        eRect.sizeDelta = new Vector2(400f, 40f);
        eRect.anchoredPosition = new Vector2(0f, 20f);

        emptyText = emptyObj.AddComponent<Text>();
        emptyText.font = HUDTheme.PixelFont;
        emptyText.fontSize = 14;
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.color = new Color(0.6f, 0.65f, 0.75f);
        emptyText.text = "No artifacts collected yet.";
        emptyObj.SetActive(false);

        float startY = 100f;
        float spacingY = 52f;

        for (int i = 0; i < 5; i++)
        {
            var rowObj = new GameObject($"ArtifactRow_{i}");
            rowObj.transform.SetParent(parent, false);
            var rRect = rowObj.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.5f, 0.5f);
            rRect.anchorMax = new Vector2(0.5f, 0.5f);
            rRect.pivot = new Vector2(0.5f, 0.5f);
            rRect.anchoredPosition = new Vector2(0f, startY - i * spacingY);
            rRect.sizeDelta = new Vector2(420f, 46f);

            // Background nhỏ cho từng Cổ vật
            var bg = rowObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.13f, 0.18f, 0.85f);

            // Tên Cổ Vật
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(rowObj.transform, false);
            var nRect = nameObj.AddComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.04f, 0.68f);
            nRect.anchorMax = new Vector2(0.96f, 0.68f);
            nRect.pivot = new Vector2(0f, 0.5f);
            nRect.sizeDelta = new Vector2(0f, 20f);

            var nText = nameObj.AddComponent<Text>();
            nText.font = HUDTheme.PixelFont;
            nText.fontSize = 13;
            nText.fontStyle = FontStyle.Bold;
            nText.alignment = TextAnchor.MiddleLeft;
            nText.color = new Color(1f, 0.85f, 0.3f);

            // Mô tả hiệu ứng
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(rowObj.transform, false);
            var dRect = descObj.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.04f, 0.28f);
            dRect.anchorMax = new Vector2(0.96f, 0.28f);
            dRect.pivot = new Vector2(0f, 0.5f);
            dRect.sizeDelta = new Vector2(0f, 18f);

            var dText = descObj.AddComponent<Text>();
            dText.font = HUDTheme.PixelFont;
            dText.fontSize = 11;
            dText.fontStyle = FontStyle.Normal;
            dText.alignment = TextAnchor.MiddleLeft;
            dText.color = new Color(0.85f, 0.9f, 0.95f);

            _artifactRows.Add(new ArtifactRowUI
            {
                Root = rowObj,
                Rect = rRect,
                NameText = nText,
                DescText = dText
            });
        }
    }

    private void BuildAbandonTab(Transform parent)
    {
        // 1. Dòng thông báo cảnh báo
        var promptObj = new GameObject("AbandonPrompt");
        promptObj.transform.SetParent(parent, false);
        var pRect = promptObj.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0.5f, 0.75f);
        pRect.anchorMax = new Vector2(0.5f, 0.75f);
        pRect.pivot = new Vector2(0.5f, 0.5f);
        pRect.sizeDelta = new Vector2(400f, 60f);

        var pText = promptObj.AddComponent<Text>();
        pText.font = HUDTheme.PixelFont;
        pText.fontSize = 15;
        pText.fontStyle = FontStyle.Bold;
        pText.alignment = TextAnchor.MiddleCenter;
        pText.color = new Color(1f, 0.6f, 0.6f);
        pText.text = "Abandon current run?\nBạn có chắc muốn bỏ cuộc trận đấu này?";

        // 2. Nút Chơi Lại (Restart)
        var restartBtn = UIFactory.CreateButton(
            parent,
            "Btn_Restart",
            new Vector2(280f, 44f),
            new Color(0.45f, 0.15f, 0.15f, 1f),
            new Color(1f, 0.45f, 0.45f),
            () => OnRestartClicked?.Invoke()
        );
        var rRect = restartBtn.GetComponent<RectTransform>();
        rRect.anchoredPosition = new Vector2(0f, 35f);
        UIFactory.CreateText(restartBtn.transform, "T", "🔄 RESTART / CHƠI LẠI", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);

        // 3. Nút Về Trang Chủ (Main Menu)
        var menuBtn = UIFactory.CreateButton(
            parent,
            "Btn_MainMenu",
            new Vector2(280f, 44f),
            new Color(0.22f, 0.25f, 0.35f, 1f),
            new Color(0.6f, 0.75f, 1f),
            () => OnMainMenuClicked?.Invoke()
        );
        var mRect = menuBtn.GetComponent<RectTransform>();
        mRect.anchoredPosition = new Vector2(0f, -20f);
        UIFactory.CreateText(menuBtn.transform, "T", "🚪 MAIN MENU / VỀ TRANG CHỦ", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);

        // 4. Nút Hủy / Quay lại (Back)
        var backBtn = UIFactory.CreateButton(
            parent,
            "Btn_Back",
            new Vector2(280f, 40f),
            new Color(0.12f, 0.15f, 0.2f, 1f),
            new Color(0.4f, 0.5f, 0.65f),
            () => SetTab(PauseMenuTab.Attributes)
        );
        var bRect = backBtn.GetComponent<RectTransform>();
        bRect.anchoredPosition = new Vector2(0f, -75f);
        UIFactory.CreateText(backBtn.transform, "T", "↩ BACK / QUAY LẠI", 13, new Color(0.8f, 0.85f, 0.9f), FontStyle.Bold, TextAnchor.MiddleCenter);
    }

    #endregion

    #region --- Nút Bấm & Điều Hướng ---

    private void CreateResumeButton(Transform parent, Vector2 pos, Vector2 size, out Text textComp)
    {
        var btn = UIFactory.CreateButton(
            parent,
            "Btn_ResumeGame",
            size,
            new Color(0.15f, 0.20f, 0.28f, 0.95f),
            new Color(0.7f, 0.8f, 0.95f, 0.85f),
            () => OnResumeClicked?.Invoke()
        );
        var rect = btn.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;

        textComp = UIFactory.CreateText(
            btn.transform,
            "Text",
            "Resume Game",
            21,
            Color.white,
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );
    }

    private void BuildBottomNavBar(Transform parent, Vector2 pos, out Text magicsText, out Text artifactsText, out Text abandonText)
    {
        var navObj = new GameObject("BottomNavBar");
        navObj.transform.SetParent(parent, false);
        var nRect = navObj.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0.5f, 0.5f);
        nRect.anchorMax = new Vector2(0.5f, 0.5f);
        nRect.pivot = new Vector2(0.5f, 0.5f);
        nRect.anchoredPosition = pos;
        nRect.sizeDelta = new Vector2(430f, 40f);

        float btnWidth = 130f;
        float btnHeight = 36f;

        // 1. Nút Magics (Kỹ năng)
        var magicsBtn = UIFactory.CreateButton(
            navObj.transform,
            "Btn_Magics",
            new Vector2(btnWidth, btnHeight),
            new Color(0.08f, 0.10f, 0.15f, 0.8f),
            new Color(0.3f, 0.4f, 0.55f, 0.6f),
            () => SetTab(_currentTab == PauseMenuTab.DamageStats ? PauseMenuTab.Attributes : PauseMenuTab.DamageStats)
        );
        var mRect = magicsBtn.GetComponent<RectTransform>();
        mRect.anchoredPosition = new Vector2(-140f, 0f);
        magicsText = UIFactory.CreateText(magicsBtn.transform, "T", "Magics", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);

        // 2. Nút Artifacts
        var artBtn = UIFactory.CreateButton(
            navObj.transform,
            "Btn_Artifacts",
            new Vector2(btnWidth, btnHeight),
            new Color(0.08f, 0.10f, 0.15f, 0.8f),
            new Color(0.3f, 0.4f, 0.55f, 0.6f),
            () => SetTab(_currentTab == PauseMenuTab.Artifacts ? PauseMenuTab.Attributes : PauseMenuTab.Artifacts)
        );
        var aRect = artBtn.GetComponent<RectTransform>();
        aRect.anchoredPosition = new Vector2(0f, 0f);
        artifactsText = UIFactory.CreateText(artBtn.transform, "T", "Artifacts", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);

        // 3. Nút Abandon
        var abBtn = UIFactory.CreateButton(
            navObj.transform,
            "Btn_Abandon",
            new Vector2(btnWidth, btnHeight),
            new Color(0.08f, 0.10f, 0.15f, 0.8f),
            new Color(0.45f, 0.25f, 0.25f, 0.6f),
            () => SetTab(_currentTab == PauseMenuTab.Abandon ? PauseMenuTab.Attributes : PauseMenuTab.Abandon)
        );
        var abRect = abBtn.GetComponent<RectTransform>();
        abRect.anchoredPosition = new Vector2(140f, 0f);
        abandonText = UIFactory.CreateText(abBtn.transform, "T", "Abandon", 16, new Color(1f, 0.45f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter);
    }

    private void CreateGearButton(Transform parent)
    {
        var gearBtn = UIFactory.CreateButton(
            parent,
            "Btn_Gear",
            new Vector2(36f, 36f),
            new Color(0.12f, 0.15f, 0.22f, 0.9f),
            new Color(0.5f, 0.65f, 0.85f, 0.8f),
            () =>
            {
                if (_gearMenuObj != null)
                {
                    _gearMenuObj.SetActive(!_gearMenuObj.activeSelf);
                }
            }
        );
        var rect = gearBtn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-15f, -15f);

        UIFactory.CreateText(gearBtn.transform, "T", "⚙", 20, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
    }

    private GameObject BuildGearDropdown(Transform parent)
    {
        var menuObj = new GameObject("GearDropdownMenu");
        menuObj.transform.SetParent(parent, false);
        var mRect = menuObj.AddComponent<RectTransform>();
        mRect.anchorMin = new Vector2(1f, 1f);
        mRect.anchorMax = new Vector2(1f, 1f);
        mRect.pivot = new Vector2(1f, 1f);
        mRect.anchoredPosition = new Vector2(-15f, -56f);
        mRect.sizeDelta = new Vector2(180f, 96f);

        var bg = menuObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);
        var outline = menuObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.45f, 0.65f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 1. Language Toggle
        var langBtn = UIFactory.CreateButton(
            menuObj.transform,
            "Btn_Lang",
            new Vector2(165f, 38f),
            new Color(0.12f, 0.16f, 0.25f, 1f),
            new Color(0.3f, 0.5f, 0.8f),
            () =>
            {
                OnLanguageClicked?.Invoke();
                RefreshData();
            }
        );
        var lRect = langBtn.GetComponent<RectTransform>();
        lRect.anchoredPosition = new Vector2(0f, 22f);
        UIFactory.CreateText(langBtn.transform, "T", "🌐 LANGUAGE / NGÔN NGỮ", 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);

        // 2. Developer Mode
        var devBtn = UIFactory.CreateButton(
            menuObj.transform,
            "Btn_Dev",
            new Vector2(165f, 38f),
            new Color(0.24f, 0.12f, 0.35f, 1f),
            new Color(0.7f, 0.35f, 0.9f),
            () =>
            {
                menuObj.SetActive(false);
                OnDevModeClicked?.Invoke();
            }
        );
        var dRect = devBtn.GetComponent<RectTransform>();
        dRect.anchoredPosition = new Vector2(0f, -22f);
        UIFactory.CreateText(devBtn.transform, "T", "🛠️ DEV PANEL [F1]", 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);

        menuObj.SetActive(false);
        return menuObj;
    }

    private Text CreateSubInfoText(Transform parent, Vector2 pos, Color color, int fontSize)
    {
        var textObj = new GameObject("SubInfo");
        textObj.transform.SetParent(parent, false);
        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(400f, 24f);

        var txt = textObj.AddComponent<Text>();
        txt.font = HUDTheme.PixelFont;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        return txt;
    }

    #endregion
}
