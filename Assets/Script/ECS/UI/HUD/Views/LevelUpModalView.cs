using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện Popup Modal chọn Perk khi người chơi lên cấp,
/// hiển thị 3 thẻ bài Hi-Bit Pixel, nút Reroll và hỗ trợ chọn nhanh bằng phím tắt [1], [2], [3], [R].
/// </summary>
public class LevelUpModalView
{
    private readonly GameObject _panelObj;
    private readonly Text _titleText;
    private readonly Text _subText;
    private readonly Transform _cardsContainer;
    private readonly Button _rerollButton;
    private readonly Text _rerollButtonText;

    private readonly List<PerkOptionData> _currentOptions = new List<PerkOptionData>();
    private int _rerollsRemaining;

    public event Action<PerkOptionData> OnPerkSelected;
    public event Action OnRerollClicked;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;
    public int OptionCount => _currentOptions.Count;

    public LevelUpModalView(Transform canvasParent)
    {
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "LevelUp_Panel",
            HUDTheme.FullScreenDim
        );

        // Header Title: ✦ LEVEL UP ✦
        var titleObj = new GameObject("HeaderTitle");
        titleObj.transform.SetParent(_panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.82f);
        titleRect.anchorMax = new Vector2(0.5f, 0.82f);
        titleRect.sizeDelta = new Vector2(600f, 70f);

        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = HUDTheme.PixelFont;
        _titleText.fontSize = 42;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = HUDTheme.GoldTitle;
        _titleText.text = GameLocalization.LevelUpHeader;
        var titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.5f, 0.2f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(3f, -3f);

        // Subtitle hướng dẫn
        var subObj = new GameObject("SubTitle");
        subObj.transform.SetParent(_panelObj.transform, false);
        var subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.74f);
        subRect.anchorMax = new Vector2(0.5f, 0.74f);
        subRect.sizeDelta = new Vector2(560f, 35f);

        _subText = subObj.AddComponent<Text>();
        _subText.font = HUDTheme.PixelFont;
        _subText.fontSize = 16;
        _subText.alignment = TextAnchor.MiddleCenter;
        _subText.color = HUDTheme.SubtitleText;
        _subText.text = GameLocalization.LevelUpSubtitle;

        // Container chứa 3 thẻ Card
        var containerObj = new GameObject("CardsContainer");
        containerObj.transform.SetParent(_panelObj.transform, false);
        var containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.48f);
        containerRect.anchorMax = new Vector2(0.5f, 0.48f);
        containerRect.sizeDelta = new Vector2(850f, 390f);

        var layout = containerObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 30f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        _cardsContainer = containerObj.transform;

        // --- Nút Reroll Perk Card ---
        _rerollButton = UIFactory.CreateButton(
            _panelObj.transform,
            "Reroll_Button",
            new Vector2(260f, 44f),
            HUDTheme.RerollBtnBg,
            HUDTheme.RerollBtnBorder,
            () =>
            {
                AudioManager.Instance.PlayUI(UIType.Reroll);
                OnRerollClicked?.Invoke();
            }
        );
        var rerollRect = _rerollButton.GetComponent<RectTransform>();
        rerollRect.anchorMin = new Vector2(0.5f, 0.13f);
        rerollRect.anchorMax = new Vector2(0.5f, 0.13f);

        _rerollButtonText = UIFactory.CreateText(
            _rerollButton.transform,
            "Text",
            "",
            15,
            new Color(0.88f, 0.78f, 1f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        _panelObj.SetActive(false);
    }

    public void Show(List<PerkOptionData> options, int rerollsRemaining)
    {
        _currentOptions.Clear();
        _currentOptions.AddRange(options);
        _rerollsRemaining = rerollsRemaining;

        UpdateRerollButtonUI();
        RenderCards();

        if (_panelObj != null)
        {
            _panelObj.SetActive(true);
            AudioManager.Instance.PlayUI(UIType.LevelUp);
        }
    }

    public void Hide()
    {
        if (_panelObj != null) _panelObj.SetActive(false);
    }

    public void SelectCardByIndex(int index)
    {
        if (index >= 0 && index < _currentOptions.Count)
        {
            AudioManager.Instance.PlayUI(UIType.PerkSelect);
            OnPerkSelected?.Invoke(_currentOptions[index]);
        }
    }

    public void UpdateRerolls(int rerollsRemaining)
    {
        _rerollsRemaining = rerollsRemaining;
        UpdateRerollButtonUI();
    }

    public void UpdateLanguage(List<PerkOptionData> refreshedOptions)
    {
        if (_titleText != null) _titleText.text = GameLocalization.LevelUpHeader;
        if (_subText != null) _subText.text = GameLocalization.LevelUpSubtitle;

        UpdateRerollButtonUI();

        if (IsActive && refreshedOptions != null)
        {
            _currentOptions.Clear();
            _currentOptions.AddRange(refreshedOptions);
            RenderCards();
        }
    }

    private void UpdateRerollButtonUI()
    {
        if (_rerollButtonText != null)
        {
            _rerollButtonText.text = _rerollsRemaining > 0
                ? GameLocalization.RerollButtonText(_rerollsRemaining)
                : GameLocalization.RerollEmptyText;
        }

        if (_rerollButton != null)
        {
            _rerollButton.interactable = _rerollsRemaining > 0;
        }
    }

    private void RenderCards()
    {
        if (_cardsContainer == null) return;

        // Xóa các card cũ
        var oldCards = new List<GameObject>();
        foreach (Transform child in _cardsContainer)
        {
            oldCards.Add(child.gameObject);
        }
        foreach (var old in oldCards)
        {
            old.transform.SetParent(null);
            UnityEngine.Object.Destroy(old);
        }

        for (int i = 0; i < _currentOptions.Count; i++)
        {
            CreateCardUI(_currentOptions[i], i + 1);
        }

        if (_cardsContainer is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private void CreateCardUI(PerkOptionData perk, int cardIndex)
    {
        var cardObj = new GameObject($"Card_{perk.Title}");
        cardObj.transform.SetParent(_cardsContainer, false);

        var rect = cardObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250f, 370f);

        bool isFusion = perk.IsFusion;

        var cardImg = cardObj.AddComponent<Image>();
        cardImg.color = isFusion ? HUDTheme.CardFusionBg : HUDTheme.CardBgDark;
        cardImg.raycastTarget = true;

        var outline = cardObj.AddComponent<Outline>();
        outline.effectColor = perk.HighlightColor;
        outline.effectDistance = isFusion ? new Vector2(4f, -4f) : new Vector2(3f, -3f);

        var btn = cardObj.AddComponent<Button>();
        btn.targetGraphic = cardImg;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => OnPerkSelected?.Invoke(perk));

        // Badge Cấp độ
        var badgeObj = new GameObject("Badge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 0.88f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.88f);
        badgeRect.sizeDelta = new Vector2(220f, 30f);

        var badgeText = badgeObj.AddComponent<Text>();
        badgeText.font = HUDTheme.PixelFont;
        badgeText.fontSize = 13;
        badgeText.fontStyle = FontStyle.Bold;
        badgeText.alignment = TextAnchor.MiddleCenter;
        badgeText.color = perk.HighlightColor;
        badgeText.text = $"[ {perk.LevelBadge} ]";
        badgeText.raycastTarget = false;

        // Tiêu đề Kỹ năng
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.74f);
        titleRect.anchorMax = new Vector2(0.5f, 0.74f);
        titleRect.sizeDelta = new Vector2(230f, 40f);

        var titleText = titleObj.AddComponent<Text>();
        titleText.font = HUDTheme.PixelFont;
        titleText.fontSize = 17;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = perk.Title;
        titleText.raycastTarget = false;

        // Vạch kẻ ngăn cách
        var lineObj = new GameObject("Divider");
        lineObj.transform.SetParent(cardObj.transform, false);
        var lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.65f);
        lineRect.anchorMax = new Vector2(0.5f, 0.65f);
        lineRect.sizeDelta = new Vector2(180f, 2f);
        var lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(perk.HighlightColor.r, perk.HighlightColor.g, perk.HighlightColor.b, 0.5f);
        lineImg.raycastTarget = false;

        // Mô tả chi tiết
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(cardObj.transform, false);
        var descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.5f, 0.38f);
        descRect.anchorMax = new Vector2(0.5f, 0.38f);
        descRect.sizeDelta = new Vector2(215f, 140f);

        var descText = descObj.AddComponent<Text>();
        descText.font = HUDTheme.PixelFont;
        descText.fontSize = 13;
        descText.alignment = TextAnchor.UpperCenter;
        descText.color = new Color(0.85f, 0.9f, 0.95f);
        descText.text = perk.Description;
        descText.raycastTarget = false;

        // Nút Chọn ở đáy thẻ
        var selectBtnObj = new GameObject("SelectButton");
        selectBtnObj.transform.SetParent(cardObj.transform, false);
        var selectBtnRect = selectBtnObj.AddComponent<RectTransform>();
        selectBtnRect.anchorMin = new Vector2(0.5f, 0.1f);
        selectBtnRect.anchorMax = new Vector2(0.5f, 0.1f);
        selectBtnRect.sizeDelta = new Vector2(190f, 38f);

        var selectBtnImg = selectBtnObj.AddComponent<Image>();
        selectBtnImg.color = new Color(perk.HighlightColor.r * 0.35f, perk.HighlightColor.g * 0.35f, perk.HighlightColor.b * 0.35f, 1f);
        selectBtnImg.raycastTarget = true;

        var sOutline = selectBtnObj.AddComponent<Outline>();
        sOutline.effectColor = perk.HighlightColor;
        sOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var sBtn = selectBtnObj.AddComponent<Button>();
        sBtn.targetGraphic = selectBtnImg;
        var sColors = sBtn.colors;
        sColors.normalColor = Color.white;
        sColors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        sColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        sBtn.colors = sColors;
        sBtn.onClick.AddListener(() => OnPerkSelected?.Invoke(perk));

        var btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(selectBtnObj.transform, false);
        var btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.sizeDelta = new Vector2(190f, 38f);

        var btnText = btnTextObj.AddComponent<Text>();
        btnText.font = HUDTheme.PixelFont;
        btnText.fontSize = 15;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        btnText.text = $"[{cardIndex}] {GameLocalization.SelectButtonText}";
        btnText.raycastTarget = false;
    }
}

