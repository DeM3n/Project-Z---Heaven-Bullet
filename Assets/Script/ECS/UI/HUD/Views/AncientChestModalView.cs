using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện Popup Modal mở Rương Cổ Vật (Ancient Chest),
/// bao gồm hoạt ảnh mở nắp rương pixel art 4 giai đoạn và thẻ bài Cổ Vật nhận thưởng.
/// </summary>
public class AncientChestModalView
{
    private readonly GameObject _panelObj;
    private readonly Image _chestModalImage;
    private readonly Text _titleText;
    private readonly Text _subText;
    private readonly Text _relicIconText;
    private readonly Text _relicNameText;
    private readonly Text _relicBadgeText;
    private readonly Text _relicDescText;
    private readonly Text _collectButtonText;

    private Coroutine _animCoroutine;
    private MonoBehaviour _coroutineRunner;
    private RelicRewardData _currentRelic;

    public event Action OnCollectClicked;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    public AncientChestModalView(Transform canvasParent)
    {
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "Chest_Panel",
            new Color(0.02f, 0.04f, 0.07f, 0.88f)
        );

        // Khung Modal trung tâm
        var modalObj = UIFactory.CreatePanel(
            _panelObj.transform,
            "ModalBox",
            new Vector2(580f, 490f),
            HUDTheme.ModalBoxBgDark,
            HUDTheme.GoldBorder,
            new Vector2(3f, -3f),
            true
        );
        var modalRect = modalObj.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);

        // Icon Rương động trên Header Modal
        var chestImgObj = new GameObject("ChestHeaderIcon");
        chestImgObj.transform.SetParent(modalObj.transform, false);
        var chestImgRect = chestImgObj.AddComponent<RectTransform>();
        chestImgRect.anchorMin = new Vector2(0.5f, 1f);
        chestImgRect.anchorMax = new Vector2(0.5f, 1f);
        chestImgRect.pivot = new Vector2(0.5f, 1f);
        chestImgRect.anchoredPosition = new Vector2(0f, -14f);
        chestImgRect.sizeDelta = new Vector2(64f, 58f);

        _chestModalImage = chestImgObj.AddComponent<Image>();
        _chestModalImage.preserveAspect = true;
        PickupVisualBridge.EnsureSpritesLoaded();
        if (PickupVisualBridge.ChestClosedSprite != null)
        {
            _chestModalImage.sprite = PickupVisualBridge.ChestClosedSprite;
        }

        // Header Title
        var titleObj = new GameObject("HeaderTitle");
        titleObj.transform.SetParent(modalObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -76f);
        titleRect.sizeDelta = new Vector2(540f, 44f);

        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = HUDTheme.PixelFont;
        _titleText.fontSize = 28;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = HUDTheme.GoldAccent;
        _titleText.text = GameLocalization.ChestHeader;
        var titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.4f, 0.18f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(2f, -2f);

        // Subtitle
        var subObj = new GameObject("SubTitle");
        subObj.transform.SetParent(modalObj.transform, false);
        var subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -118f);
        subRect.sizeDelta = new Vector2(500f, 26f);

        _subText = subObj.AddComponent<Text>();
        _subText.font = HUDTheme.PixelFont;
        _subText.fontSize = 14;
        _subText.alignment = TextAnchor.MiddleCenter;
        _subText.color = HUDTheme.SubtitleText;
        _subText.text = GameLocalization.ChestSubtitle;

        // Card hiển thị Cổ Vật
        var cardObj = UIFactory.CreatePanel(
            modalObj.transform,
            "RelicCard",
            new Vector2(460f, 180f),
            new Color(0.12f, 0.15f, 0.22f, 0.95f),
            new Color(0.85f, 0.7f, 0.2f, 0.8f),
            new Vector2(2f, -2f)
        );
        var cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, -5f);

        // Icon Emoji
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(cardObj.transform, false);
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(65f, 0f);
        iconRect.sizeDelta = new Vector2(90f, 90f);

        _relicIconText = iconObj.AddComponent<Text>();
        _relicIconText.font = HUDTheme.PixelFont;
        _relicIconText.fontSize = 54;
        _relicIconText.alignment = TextAnchor.MiddleCenter;
        _relicIconText.color = Color.white;
        _relicIconText.text = "💎";

        // Tên Cổ Vật
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(cardObj.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = Vector2.one;
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(130f, -22f);
        nameRect.sizeDelta = new Vector2(-150f, 34f);

        _relicNameText = nameObj.AddComponent<Text>();
        _relicNameText.font = HUDTheme.PixelFont;
        _relicNameText.fontSize = 22;
        _relicNameText.fontStyle = FontStyle.Bold;
        _relicNameText.alignment = TextAnchor.MiddleLeft;
        _relicNameText.color = new Color(1f, 0.88f, 0.35f);
        _relicNameText.text = "RELIC NAME";

        // Level Badge
        var badgeObj = new GameObject("Badge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = Vector2.one;
        badgeRect.anchorMax = Vector2.one;
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.anchoredPosition = new Vector2(-20f, -22f);
        badgeRect.sizeDelta = new Vector2(70f, 30f);

        _relicBadgeText = badgeObj.AddComponent<Text>();
        _relicBadgeText.font = HUDTheme.PixelFont;
        _relicBadgeText.fontSize = 16;
        _relicBadgeText.fontStyle = FontStyle.Bold;
        _relicBadgeText.alignment = TextAnchor.MiddleRight;
        _relicBadgeText.color = HUDTheme.DiamondCyan;
        _relicBadgeText.text = "LV.1";

        // Mô tả Cổ Vật
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(cardObj.transform, false);
        var descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.pivot = Vector2.zero;
        descRect.anchoredPosition = new Vector2(130f, 20f);
        descRect.sizeDelta = new Vector2(-150f, -80f);

        _relicDescText = descObj.AddComponent<Text>();
        _relicDescText.font = HUDTheme.PixelFont;
        _relicDescText.fontSize = 15;
        _relicDescText.alignment = TextAnchor.UpperLeft;
        _relicDescText.color = new Color(0.85f, 0.9f, 0.95f);
        _relicDescText.text = "Relic Description...";

        // Nút Thu Nhận
        var collectBtn = UIFactory.CreateButton(
            modalObj.transform,
            "Collect_Button",
            new Vector2(240f, 48f),
            HUDTheme.CollectBtnBg,
            new Color(0.35f, 0.2f, 0f, 0.95f),
            () => OnCollectClicked?.Invoke()
        );
        var btnRect = collectBtn.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 25f);

        _collectButtonText = UIFactory.CreateText(
            collectBtn.transform,
            "Text",
            GameLocalization.CollectButtonText,
            18,
            new Color(0.12f, 0.08f, 0.02f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        _panelObj.SetActive(false);
    }

    public void Show(RelicRewardData rewardData, MonoBehaviour runner)
    {
        _currentRelic = rewardData;
        _coroutineRunner = runner;

        if (_relicIconText != null) _relicIconText.text = rewardData.IconEmoji;
        if (_relicNameText != null) _relicNameText.text = rewardData.Title;
        if (_relicBadgeText != null) _relicBadgeText.text = rewardData.LevelBadge;
        if (_relicDescText != null) _relicDescText.text = rewardData.Description;

        if (_panelObj != null) _panelObj.SetActive(true);

        // Chạy animation mở nắp rương
        if (_coroutineRunner != null)
        {
            if (_animCoroutine != null) _coroutineRunner.StopCoroutine(_animCoroutine);
            _animCoroutine = _coroutineRunner.StartCoroutine(AnimateChestModalRoutine());
        }
    }

    public void Hide()
    {
        if (_animCoroutine != null && _coroutineRunner != null)
        {
            _coroutineRunner.StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }

        if (_panelObj != null) _panelObj.SetActive(false);
    }

    public void UpdateLanguage(Func<RelicType, RelicRewardData> refreshFunc = null)
    {
        if (_titleText != null) _titleText.text = GameLocalization.ChestHeader;
        if (_subText != null) _subText.text = GameLocalization.ChestSubtitle;
        if (_collectButtonText != null) _collectButtonText.text = GameLocalization.CollectButtonText;

        if (IsActive && refreshFunc != null)
        {
            _currentRelic = refreshFunc(_currentRelic.Type);
            if (_relicNameText != null) _relicNameText.text = _currentRelic.Title;
            if (_relicDescText != null) _relicDescText.text = _currentRelic.Description;
            if (_relicBadgeText != null) _relicBadgeText.text = _currentRelic.LevelBadge;
        }
    }

    private IEnumerator AnimateChestModalRoutine()
    {
        if (_chestModalImage == null) yield break;

        PickupVisualBridge.EnsureSpritesLoaded();
        var s0 = PickupVisualBridge.ChestClosedSprite;
        var s1 = PickupVisualBridge.ChestOpenFrame1;
        var s2 = PickupVisualBridge.ChestOpenFrame2;
        var s3 = PickupVisualBridge.ChestOpenFrame3;

        if (s0 != null) _chestModalImage.sprite = s0;
        _chestModalImage.transform.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(0.10f);
        if (s1 != null) _chestModalImage.sprite = s1;

        yield return new WaitForSecondsRealtime(0.10f);
        if (s2 != null) _chestModalImage.sprite = s2;

        yield return new WaitForSecondsRealtime(0.10f);
        if (s3 != null)
        {
            _chestModalImage.sprite = s3;
            // Hiệu ứng nảy scale khi nắp mở toang
            _chestModalImage.transform.localScale = new Vector3(1.22f, 1.22f, 1f);
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float sc = Mathf.Lerp(1.22f, 1.0f, elapsed / 0.2f);
                _chestModalImage.transform.localScale = new Vector3(sc, sc, 1f);
                yield return null;
            }
            _chestModalImage.transform.localScale = Vector3.one;
        }
    }
}

