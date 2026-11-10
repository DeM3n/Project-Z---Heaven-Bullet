using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Giao diện Màn hình Chính (Main Menu Controller):
/// Thiết kế theo phong cách hiện đại, trực quan, mô-đun hóa cao (Modern Pixel-Survivor Dark UI):
/// 1. Thanh tiêu đề & Ví Linh Thạch (Spirit Stones) realtime + Phím tắt đổi ngôn ngữ [L].
/// 2. Menu chính: VÀO TRẬN (Play), BÁCH KHOA TOÀN THƯ (Codex), CÂY THIÊN PHÚ (Talent Tree), CÀI ĐẶT (Settings), THOÁT.
/// 3. Bách Khoa Toàn Thư (Codex): 3 tab tra cứu chuyên sâu Quái vật, Kỹ năng/Đạn và Cổ vật bị động.
/// 4. Cây Thiên Phú (Skill Tree): Nâng cấp chỉ số chung 3 nhánh Thể Lực, Công Kích, Vận Mệnh + Tẩy điểm Respec 100%.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    private static MainMenuUI _instance;
    public static MainMenuUI Instance => _instance;

    private Canvas _canvas;
    private GraphicRaycaster _raycaster;

    // --- State Panels ---
    private enum MenuState
    {
        Home,
        Codex,
        Talents,
        Settings
    }
    private MenuState _currentState = MenuState.Home;

    private GameObject _homePanel;
    private GameObject _codexPanel;
    private GameObject _talentsPanel;
    private GameObject _settingsPanel;

    // --- Header Elements ---
    private Text _titleText;
    private Text _currencyText;
    private Button _testAddCurrencyBtn;
    private Button _langBtn;
    private Text _langBtnText;

    // --- Home Buttons ---
    private Button _playBtn;
    private Text _playBtnText;
    private Button _codexBtn;
    private Text _codexBtnText;
    private Button _talentsBtn;
    private Text _talentsBtnText;
    private Button _settingsBtn;
    private Text _settingsBtnText;
    private Text _settingsMuteBtnText;
    private Button _quitBtn;
    private Text _quitBtnText;

    // --- Codex State & UI 2.0 ---
    private CodexDatabase.EntryCategory _codexCategory = CodexDatabase.EntryCategory.Enemies;
    private string _selectedEntryId;
    private Transform _codexListContainer;
    private Text _codexEntryTitle;
    private Text _codexEntrySubtitle;
    private Text _codexEntryBadge;
    private Text _codexEntryDesc;
    private Text[] _statCardLabels = new Text[4];
    private Text[] _statCardValues = new Text[4];
    private Text _codexParamsHeader;
    private Text _codexProgressionHeader;
    private Text _codexProgressionText;
    private Text _codexEntryMechanics;
    private Text _codexEntryTips;
    private Button _tabEnemiesBtn;
    private Button _tabSkillsBtn;
    private Button _tabRelicsBtn;
    private readonly List<Image> _codexItemBgs = new List<Image>();
    private readonly List<GameObject> _codexItemAccents = new List<GameObject>();

    // --- Talent Tree UI ---
    private Transform _healthNodesContainer;
    private Transform _offenseNodesContainer;
    private Transform _fortuneNodesContainer;
    private Text _healthBranchTitle;
    private Text _healthBranchSub;
    private Text _offenseBranchTitle;
    private Text _offenseBranchSub;
    private Text _fortuneBranchTitle;
    private Text _fortuneBranchSub;
    private Text _talentSummaryText;
    private Button _respecBtn;
    private Text _respecBtnText;
    private readonly Dictionary<string, NodeUIItem> _nodeUIItems = new Dictionary<string, NodeUIItem>();

    // Mastery Summary Cards for balanced 3x5 layout
    private Text _healthMasteryTitle;
    private Text _healthMasteryLine1;
    private Text _healthMasteryLine2;
    private Text _healthMasteryBadge;
    private Text _offenseMasteryTitle;
    private Text _offenseMasteryLine1;
    private Text _offenseMasteryLine2;
    private Text _offenseMasteryBadge;

    // --- Talent Tooltip UI ---
    private GameObject _talentTooltipObj;
    private RectTransform _talentTooltipRt;
    private Outline _talentTooltipOutline;
    private Text _tooltipIconText;
    private Text _tooltipNameText;
    private Text _tooltipBranchTierText;
    private Text _tooltipDescText;
    private Text _tooltipCurrentStatText;
    private Text _tooltipNextStatText;
    private Text _tooltipMaxStatText;
    private Text _tooltipFooterText;
    private Image _tooltipDividerImg;
    private string _currentHoveredNodeId;

    private class NodeUIItem
    {
        public string NodeId;
        public Image CardBg;
        public Outline CardOutline;
        public Text IconText;
        public Text TierBadgeText;
        public Text NameText;
        public Text LevelBadgeText;
        public List<Image> PipImages = new List<Image>();
        public GameObject StatusTagObj;
        public Image StatusTagBg;
        public Outline StatusTagOutline;
        public Text StatusTagText;
        public Button UpgradeBtn;
        public Text UpgradeCostText;
        public Text UpgradeBtnText;
        public GameObject MaxedBadge;
        public Text MaxedBadgeText;
        public GameObject LockedBadge;
        public Text LockedBadgeText;
    }

    // --- Fonts & Textures ---
    private Font _defaultFont;
    private Sprite _cardBgSprite;
    private Sprite _btnBgSprite;
    private Sprite _badgeBgSprite;
    private Sprite _pipBgSprite;
    private Sprite _powerCellSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        CheckSceneAndInit();
        SceneManager.sceneLoaded += (scene, mode) => CheckSceneAndInit();
    }

    private static void CheckSceneAndInit()
    {
        if (SceneManager.GetActiveScene().name == "MainMenuScene")
        {
            // Dọn sạch các thực thể gameplay (quái, đạn, ngọc, vật phẩm) còn sót lại từ trận đấu trước nếu có
            if (Unity.Entities.World.DefaultGameObjectInjectionWorld != null &&
                Unity.Entities.World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                var em = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
                using (var query = em.CreateEntityQuery(new Unity.Entities.EntityQueryDesc
                {
                    Any = new Unity.Entities.ComponentType[]
                    {
                        Unity.Entities.ComponentType.ReadOnly<EnemyTag>(),
                        Unity.Entities.ComponentType.ReadOnly<ProjectileTag>(),
                        Unity.Entities.ComponentType.ReadOnly<ExpOrbTag>(),
                        Unity.Entities.ComponentType.ReadOnly<PickupItemTag>(),
                        Unity.Entities.ComponentType.ReadOnly<FlyingSwordTag>(),
                        Unity.Entities.ComponentType.ReadOnly<SlashSwordTag>()
                    }
                }))
                {
                    em.DestroyEntity(query);
                }
            }

            if (FindAnyObjectByType<MainMenuUI>() == null)
            {
                var go = new GameObject("[MainMenuUI]");
                go.AddComponent<MainMenuUI>();
            }
        }
    }

    private void Awake()
    {
        _instance = this;
        EnsureEventSystem();
        LoadResources();
        BuildUI();
    }

    private void Start()
    {
        MetaProgressionService.Instance.OnDataChanged += RefreshAllUI;
        GameLocalization.OnLanguageChanged += UpdateLocalization;
        RefreshAllUI();
    }

    private void OnDestroy()
    {
        if (MetaProgressionService.Instance != null)
        {
            MetaProgressionService.Instance.OnDataChanged -= RefreshAllUI;
        }
        GameLocalization.OnLanguageChanged -= UpdateLocalization;
    }

    private void Update()
    {
        // Cập nhật vị trí Tooltip theo con trỏ chuột khi đang hover vào thẻ thiên phú
        if (!string.IsNullOrEmpty(_currentHoveredNodeId) && _talentTooltipObj != null && _talentTooltipObj.activeSelf)
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            UpdateTooltipPosition(mousePos);
        }

        // Phím tắt bàn phím tiện ích
        if (Keyboard.current != null)
        {
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                GameLocalization.ToggleLanguage();
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_currentState != MenuState.Home)
                {
                    SwitchState(MenuState.Home);
        AudioManager.Instance.PlayBGM(BGMTrack.MainMenu, 1.5f);
                }
            }
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (_currentState == MenuState.Home)
                {
                    OnPlayClicked();
                }
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void LoadResources()
    {
        try
        {
            _defaultFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma" }, 36);
        }
        catch { }

        if (_defaultFont == null)
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        _cardBgSprite = CreateRoundedRectSprite(64, 64, 10, new Color(0.12f, 0.15f, 0.22f, 0.95f), new Color(0.24f, 0.32f, 0.48f, 1f));
        _btnBgSprite = CreateRoundedRectSprite(48, 48, 8, new Color(0.18f, 0.24f, 0.36f, 1f), new Color(0.35f, 0.5f, 0.75f, 1f));
        _badgeBgSprite = CreateRoundedRectSprite(32, 32, 6, new Color(0.12f, 0.16f, 0.24f, 1f), new Color(0.35f, 0.48f, 0.7f, 1f));
        _pipBgSprite = CreateRoundedRectSprite(16, 16, 2, Color.white, Color.clear);
        _powerCellSprite = CreateRoundedRectSprite(32, 16, 3, Color.white, new Color(1f, 1f, 1f, 0.35f));
    }

    // =========================================================================
    // XÂY DỰNG TOÀN BỘ GIAO DIỆN PROCEDURAL
    // =========================================================================
    private void BuildUI()
    {
        // 1. Root Canvas
        var canvasObj = new GameObject("MainMenuCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3.0f;
        scaler.referencePixelsPerUnit = 100f;

        _raycaster = canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Background Panel (Màu Slate đen vũ trụ mờ ảo)
        var bgObj = CreateUIObject("Background", canvasObj.transform);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
        SetFullStretch(bgObj.GetComponent<RectTransform>());

        // Vệt sáng Gradient phía trên
        var topGlow = CreateUIObject("TopGlow", bgObj.transform);
        var glowImg = topGlow.AddComponent<Image>();
        glowImg.color = new Color(0.12f, 0.25f, 0.45f, 0.25f);
        var glowRt = topGlow.GetComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0f, 0.7f);
        glowRt.anchorMax = new Vector2(1f, 1f);
        glowRt.offsetMin = glowRt.offsetMax = Vector2.zero;

        // 3. Header Bar
        BuildHeader(bgObj.transform);

        // 4. Các Panel Chức Năng
        BuildHomePanel(bgObj.transform);
        BuildCodexPanel(bgObj.transform);
        BuildTalentTreePanel(bgObj.transform);
        BuildSettingsPanel(bgObj.transform);

        SwitchState(MenuState.Home);
        AudioManager.Instance.PlayBGM(BGMTrack.MainMenu, 1.5f);
    }

    private void BuildHeader(Transform parent)
    {
        var headerObj = CreateUIObject("HeaderBar", parent);
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, 0f);
        headerRt.sizeDelta = new Vector2(0f, 75f);

        var headerImg = headerObj.AddComponent<Image>();
        headerImg.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        // Game Title (Fixed left anchor with proper margin, never cut off)
        var titleObj = CreateUIObject("GameTitle", headerObj.transform);
        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = _defaultFont;
        _titleText.fontSize = 24;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.color = new Color(0.95f, 0.85f, 0.45f, 1f);
        _titleText.alignment = TextAnchor.MiddleLeft;
        _titleText.text = "PROJECT Z: HEAVEN BULLET";
        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(0f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(30f, 0f);
        titleRt.sizeDelta = new Vector2(480f, 50f);

        // Tiền Tệ Linh Thạch (Spirit Stones)
        var curObj = CreateUIObject("CurrencyDisplay", headerObj.transform);
        _currencyText = curObj.AddComponent<Text>();
        _currencyText.font = _defaultFont;
        _currencyText.fontSize = 20;
        _currencyText.fontStyle = FontStyle.Bold;
        _currencyText.color = new Color(0.35f, 0.95f, 1f, 1f);
        _currencyText.alignment = TextAnchor.MiddleRight;
        _currencyText.text = "💎 500 Linh Thạch";
        var curRt = curObj.GetComponent<RectTransform>();
        curRt.anchorMin = new Vector2(1f, 0.5f);
        curRt.anchorMax = new Vector2(1f, 0.5f);
        curRt.pivot = new Vector2(1f, 0.5f);
        curRt.anchoredPosition = new Vector2(-225f, 0f);
        curRt.sizeDelta = new Vector2(280f, 44f);

        // Nút Cheat/Test Thêm Linh Thạch
        _testAddCurrencyBtn = CreateSimpleButton("TestAddBtn", headerObj.transform, "+1000 💎", 14, new Vector2(90f, 34f), () =>
        {
            MetaProgressionService.Instance.AddSpiritStones(1000);
        });
        var testRt = _testAddCurrencyBtn.GetComponent<RectTransform>();
        testRt.anchorMin = new Vector2(1f, 0.5f);
        testRt.anchorMax = new Vector2(1f, 0.5f);
        testRt.pivot = new Vector2(1f, 0.5f);
        testRt.anchoredPosition = new Vector2(-125f, 0f);

        // Nút Đổi Ngôn Ngữ
        _langBtn = CreateSimpleButton("LangBtn", headerObj.transform, "🌐 VI", 14, new Vector2(75f, 36f), () =>
        {
            GameLocalization.ToggleLanguage();
        });
        var langRt = _langBtn.GetComponent<RectTransform>();
        langRt.anchorMin = new Vector2(1f, 0.5f);
        langRt.anchorMax = new Vector2(1f, 0.5f);
        langRt.pivot = new Vector2(1f, 0.5f);
        langRt.anchoredPosition = new Vector2(-30f, 0f);
        _langBtnText = _langBtn.GetComponentInChildren<Text>();
    }

    // =========================================================================
    // HOME PANEL (CÁC NÚT ĐIỀU HƯỚNG CHÍNH)
    // =========================================================================
    private void BuildHomePanel(Transform parent)
    {
        _homePanel = CreateUIObject("HomePanel", parent);
        SetFullStretch(_homePanel.GetComponent<RectTransform>(), 0f, 90f);

        // Center Container
        var menuContainer = CreateUIObject("MenuButtons", _homePanel.transform);
        var containerRt = menuContainer.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.sizeDelta = new Vector2(460f, 520f);
        containerRt.anchoredPosition = new Vector2(0f, -20f);

        var layout = menuContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        // 1. Play Button
        _playBtn = CreateMenuButton("PlayBtn", menuContainer.transform, "⚔️  VÀO TRẬN (PLAY)", new Color(0.95f, 0.35f, 0.2f, 1f), 65f, OnPlayClicked);
        _playBtnText = _playBtn.GetComponentInChildren<Text>();
        _playBtnText.fontSize = 24;

        // 2. Codex Button
        _codexBtn = CreateMenuButton("CodexBtn", menuContainer.transform, "📖  BÁCH KHOA TOÀN THƯ (CODEX)", new Color(0.2f, 0.55f, 0.85f, 1f), 56f, () => SwitchState(MenuState.Codex));
        _codexBtnText = _codexBtn.GetComponentInChildren<Text>();

        // 3. Talent Tree Button
        _talentsBtn = CreateMenuButton("TalentsBtn", menuContainer.transform, "⚡  CÂY THIÊN PHÚ (TALENTS)", new Color(0.85f, 0.65f, 0.2f, 1f), 56f, () => SwitchState(MenuState.Talents));
        _talentsBtnText = _talentsBtn.GetComponentInChildren<Text>();

        // 4. Settings Button
        _settingsBtn = CreateMenuButton("SettingsBtn", menuContainer.transform, "⚙️  CÀI ĐẶT (SETTINGS)", new Color(0.35f, 0.45f, 0.6f, 1f), 52f, () => SwitchState(MenuState.Settings));
        _settingsBtnText = _settingsBtn.GetComponentInChildren<Text>();

        // 5. Quit Button
        _quitBtn = CreateMenuButton("QuitBtn", menuContainer.transform, "🚪  THOÁT (QUIT)", new Color(0.25f, 0.3f, 0.4f, 1f), 48f, () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
        _quitBtnText = _quitBtn.GetComponentInChildren<Text>();
    }

    // =========================================================================
    // CODEX PANEL 2.0 (MODULAR SHOWCASE & SSOT ARCHIVE)
    // =========================================================================
    private void BuildCodexPanel(Transform parent)
    {
        _codexPanel = CreateUIObject("CodexPanel", parent);
        SetFullStretch(_codexPanel.GetComponent<RectTransform>(), 0f, 75f);

        // 1. Sub-Header Bar: Nút Quay Lại + 3 Tab Danh Mục Trung Tâm (Chuẩn 16:9, không bị đè hay tràn)
        var subHeader = CreateUIObject("SubHeader", _codexPanel.transform);
        var subHeaderRt = subHeader.GetComponent<RectTransform>();
        subHeaderRt.anchorMin = new Vector2(0.04f, 1f);
        subHeaderRt.anchorMax = new Vector2(0.96f, 1f);
        subHeaderRt.pivot = new Vector2(0.5f, 1f);
        subHeaderRt.anchoredPosition = new Vector2(0f, -10f);
        subHeaderRt.sizeDelta = new Vector2(0f, 44f);

        // Nút Trở Về (Back Button) cố định góc trái của SubHeader
        var backBtn = CreateSimpleButton("BackBtn", subHeader.transform, "◀ QUAY LẠI [ESC]", 14, new Vector2(150f, 40f), () => SwitchState(MenuState.Home));
        var backRt = backBtn.GetComponent<RectTransform>();
        backRt.anchorMin = new Vector2(0f, 0.5f);
        backRt.anchorMax = new Vector2(0f, 0.5f);
        backRt.pivot = new Vector2(0f, 0.5f);
        backRt.anchoredPosition = Vector2.zero;
        backBtn.image.color = new Color(0.18f, 0.24f, 0.35f, 0.95f);

        // Top Navigation Tabs (Căn giữa tuyệt đối trong thanh SubHeader, rộng 760px)
        var topTabsObj = CreateUIObject("TopTabs", subHeader.transform);
        var tabsRt = topTabsObj.GetComponent<RectTransform>();
        tabsRt.anchorMin = new Vector2(0.5f, 0.5f);
        tabsRt.anchorMax = new Vector2(0.5f, 0.5f);
        tabsRt.pivot = new Vector2(0.5f, 0.5f);
        tabsRt.anchoredPosition = Vector2.zero;
        tabsRt.sizeDelta = new Vector2(760f, 40f);

        var tabsLayout = topTabsObj.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 14f;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;

        _tabEnemiesBtn = CreateSimpleButton("TabEnemies", topTabsObj.transform, "QUÁI VẬT (BESTIARY)", 15, Vector2.zero, () => SelectCodexCategory(CodexDatabase.EntryCategory.Enemies));
        _tabSkillsBtn = CreateSimpleButton("TabSkills", topTabsObj.transform, "VŨ KHÍ & ĐẠN (WEAPONS)", 15, Vector2.zero, () => SelectCodexCategory(CodexDatabase.EntryCategory.Skills));
        _tabRelicsBtn = CreateSimpleButton("TabRelics", topTabsObj.transform, "CỔ VẬT (RELICS)", 15, Vector2.zero, () => SelectCodexCategory(CodexDatabase.EntryCategory.Relics));

        // 2. Content Area: Split 2 Cột (Trái: Danh Sách Thẻ 28%, Phải: Hero Inspection Card 70.5%)
        var contentObj = CreateUIObject("CodexContent", _codexPanel.transform);
        var contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.04f, 0.04f);
        contentRt.anchorMax = new Vector2(0.96f, 1f);
        contentRt.offsetMin = new Vector2(0f, 14f);
        contentRt.offsetMax = new Vector2(0f, -66f); // chừa khoảng trống cho SubHeader

        // --- CỘT TRÁI (CATALOG LIST) ---
        var leftCol = CreateUIObject("LeftList", contentObj.transform);
        var leftRt = leftCol.GetComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0f, 0f);
        leftRt.anchorMax = new Vector2(0.28f, 1f);
        leftRt.offsetMin = leftRt.offsetMax = Vector2.zero;

        var leftBg = leftCol.AddComponent<Image>();
        leftBg.sprite = _cardBgSprite;
        leftBg.type = Image.Type.Sliced;
        leftBg.color = new Color(0.08f, 0.11f, 0.18f, 0.95f);

        var scroll = leftCol.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = CreateUIObject("Viewport", leftCol.transform);
        SetFullStretch(viewport.GetComponent<RectTransform>(), 4f, 4f);
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport.GetComponent<RectTransform>();

        var container = CreateUIObject("ListContainer", viewport.transform);
        var cRt = container.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.sizeDelta = new Vector2(0f, 600f);
        var vLayout = container.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 8f;
        vLayout.padding = new RectOffset(6, 6, 8, 8);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        var fitter = container.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cRt;
        _codexListContainer = container.transform;

        // --- CỘT PHẢI (HERO DETAIL INSPECTION CARD) ---
        var rightCol = CreateUIObject("RightDetail", contentObj.transform);
        var rightRt = rightCol.GetComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(0.295f, 0f);
        rightRt.anchorMax = new Vector2(1f, 1f);
        rightRt.offsetMin = rightRt.offsetMax = Vector2.zero;

        var cardImg = rightCol.AddComponent<Image>();
        cardImg.sprite = _cardBgSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.10f, 0.14f, 0.22f, 0.96f);

        // Layout bên trong Card chi tiết
        var detailPad = CreateUIObject("DetailPadding", rightCol.transform);
        SetFullStretch(detailPad.GetComponent<RectTransform>(), 20f, 20f);
        var dLayout = detailPad.AddComponent<VerticalLayoutGroup>();
        dLayout.spacing = 16f;
        dLayout.childControlWidth = true;
        dLayout.childControlHeight = false;

        // BLOCK 1: HEADER BANNER (Title, Badge, Subtitle, Lore)
        var bannerBox = CreateUIObject("HeaderBanner", detailPad.transform);
        var bImg = bannerBox.AddComponent<Image>();
        bImg.sprite = _cardBgSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.14f, 0.18f, 0.28f, 0.92f);
        var bLayout = bannerBox.AddComponent<VerticalLayoutGroup>();
        bLayout.padding = new RectOffset(22, 22, 18, 18);
        bLayout.spacing = 10f;
        bLayout.childControlWidth = true;
        bLayout.childControlHeight = true;
        bLayout.childForceExpandHeight = false;

        var bFitter = bannerBox.AddComponent<ContentSizeFitter>();
        bFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleRow = CreateUIObject("TitleRow", bannerBox.transform);
        var trLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
        trLayout.spacing = 16f;
        trLayout.childAlignment = TextAnchor.MiddleLeft;
        trLayout.childControlWidth = true;
        trLayout.childControlHeight = true;
        trLayout.childForceExpandWidth = false;
        trLayout.childForceExpandHeight = false;
        var trLe = titleRow.AddComponent<LayoutElement>();
        trLe.preferredHeight = 48f;
        trLe.minHeight = 48f;

        _codexEntryTitle = CreateLabel("EntryTitle", titleRow.transform, "Tên Mục", 34, FontStyle.Bold, new Color(1f, 0.90f, 0.45f, 1f));
        _codexEntryTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        var titleLe = _codexEntryTitle.gameObject.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;

        _codexEntryBadge = CreateLabel("EntryBadge", titleRow.transform, "[ PHÂN LOẠI ]", 16, FontStyle.Bold, new Color(0.35f, 0.85f, 1f, 1f));
        _codexEntryBadge.horizontalOverflow = HorizontalWrapMode.Overflow;
        _codexEntryBadge.alignment = TextAnchor.MiddleRight;
        var badgeLe = _codexEntryBadge.gameObject.AddComponent<LayoutElement>();
        badgeLe.preferredWidth = 320f;

        _codexEntrySubtitle = CreateLabel("EntrySubtitle", bannerBox.transform, "Phân Loại Phụ", 18, FontStyle.Italic, new Color(0.60f, 0.88f, 1f, 1f));
        _codexEntrySubtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        var subLe = _codexEntrySubtitle.gameObject.AddComponent<LayoutElement>();
        subLe.preferredHeight = 28f;
        subLe.minHeight = 28f;

        _codexEntryDesc = CreateLabel("EntryDesc", bannerBox.transform, "Mô tả truyền thuyết...", 19, FontStyle.Normal, new Color(0.92f, 0.95f, 0.98f, 1f));
        _codexEntryDesc.lineSpacing = 1.35f;
        var descLe = _codexEntryDesc.gameObject.AddComponent<LayoutElement>();
        descLe.minHeight = 52f;

        // BLOCK 2: CORE PARAMETERS MATRIX (2x2 Stat Cards + Progression Bar)
        var paramsBox = CreateUIObject("ParamsCard", detailPad.transform);
        var pImg = paramsBox.AddComponent<Image>();
        pImg.sprite = _cardBgSprite;
        pImg.type = Image.Type.Sliced;
        pImg.color = new Color(0.12f, 0.16f, 0.25f, 0.90f);
        var pLayout = paramsBox.AddComponent<VerticalLayoutGroup>();
        pLayout.padding = new RectOffset(22, 22, 18, 18);
        pLayout.spacing = 14f;
        pLayout.childControlWidth = true;
        pLayout.childControlHeight = true;
        pLayout.childForceExpandHeight = false;

        var pFitter = paramsBox.AddComponent<ContentSizeFitter>();
        pFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _codexParamsHeader = CreateLabel("ParamsHeader", paramsBox.transform, "[ BẢNG THÔNG SỐ CỐT LÕI (CORE SPECIFICATIONS) ]", 17, FontStyle.Bold, new Color(0.55f, 0.85f, 1f, 1f));
        var phLe = _codexParamsHeader.gameObject.AddComponent<LayoutElement>();
        phLe.preferredHeight = 28f;

        // 2x2 Grid cho 4 Chỉ Số Cốt Lõi
        for (int row = 0; row < 2; row++)
        {
            var rowObj = CreateUIObject($"Row_{row}", paramsBox.transform);
            var rLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rLayout.spacing = 14f;
            rLayout.childControlWidth = true;
            rLayout.childControlHeight = true;
            var rLe = rowObj.AddComponent<LayoutElement>();
            rLe.preferredHeight = 58f;
            rLe.minHeight = 58f;

            for (int col = 0; col < 2; col++)
            {
                int cardIdx = row * 2 + col;
                var cellObj = CreateUIObject($"StatBox_{cardIdx}", rowObj.transform);
                var cImg = cellObj.AddComponent<Image>();
                cImg.sprite = _cardBgSprite;
                cImg.type = Image.Type.Sliced;
                cImg.color = new Color(0.08f, 0.11f, 0.18f, 0.85f);

                var cellLayout = cellObj.AddComponent<HorizontalLayoutGroup>();
                cellLayout.padding = new RectOffset(16, 16, 6, 6);
                cellLayout.spacing = 12f;
                cellLayout.childAlignment = TextAnchor.MiddleLeft;
                cellLayout.childControlWidth = true;
                cellLayout.childControlHeight = true;
                cellLayout.childForceExpandWidth = false;

                _statCardLabels[cardIdx] = CreateLabel("Label", cellObj.transform, "LABEL", 16, FontStyle.Bold, new Color(0.72f, 0.84f, 0.96f, 1f));
                _statCardLabels[cardIdx].horizontalOverflow = HorizontalWrapMode.Overflow;
                var lblLe = _statCardLabels[cardIdx].gameObject.AddComponent<LayoutElement>();
                lblLe.preferredWidth = 240f;
                lblLe.minWidth = 200f;

                _statCardValues[cardIdx] = CreateLabel("Value", cellObj.transform, "VALUE", 22, FontStyle.Bold, new Color(0.35f, 0.92f, 1f, 1f));
                _statCardValues[cardIdx].horizontalOverflow = HorizontalWrapMode.Overflow;
                var valLe = _statCardValues[cardIdx].gameObject.AddComponent<LayoutElement>();
                valLe.flexibleWidth = 1f;
            }
        }

        // Thanh Tiến Trình / Thăng Hoa Cấp Độ
        var progObj = CreateUIObject("ProgressionSection", paramsBox.transform);
        var progLayout = progObj.AddComponent<VerticalLayoutGroup>();
        progLayout.spacing = 6f;
        progLayout.childControlWidth = true;
        progLayout.childControlHeight = true;
        progLayout.childForceExpandHeight = false;

        _codexProgressionHeader = CreateLabel("ProgHeader", progObj.transform, "[ TIẾN TRÌNH CẤP ĐỘ & THĂNG HOA (PROGRESSION) ]", 17, FontStyle.Bold, new Color(1f, 0.92f, 0.55f, 1f));
        var prghLe = _codexProgressionHeader.gameObject.AddComponent<LayoutElement>();
        prghLe.preferredHeight = 28f;

        _codexProgressionText = CreateLabel("ProgText", progObj.transform, "Lv1 -> Lv4...", 18, FontStyle.Normal, new Color(0.92f, 0.95f, 0.98f, 1f));
        _codexProgressionText.lineSpacing = 1.35f;
        var prgtLe = _codexProgressionText.gameObject.AddComponent<LayoutElement>();
        prgtLe.minHeight = 44f;

        // BLOCK 3: ADVISORY (Mechanics & Tactical Advisory)
        var advBox = CreateUIObject("AdvisoryCard", detailPad.transform);
        var aImg = advBox.AddComponent<Image>();
        aImg.sprite = _cardBgSprite;
        aImg.type = Image.Type.Sliced;
        aImg.color = new Color(0.12f, 0.16f, 0.25f, 0.90f);
        var aLayout = advBox.AddComponent<VerticalLayoutGroup>();
        aLayout.padding = new RectOffset(22, 22, 18, 18);
        aLayout.spacing = 14f;
        aLayout.childControlWidth = true;
        aLayout.childControlHeight = true;
        aLayout.childForceExpandHeight = false;

        var aFitter = advBox.AddComponent<ContentSizeFitter>();
        aFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _codexEntryMechanics = CreateLabel("EntryMechanics", advBox.transform, "Cơ Chế...", 18, FontStyle.Normal, new Color(0.92f, 0.95f, 0.98f, 1f));
        _codexEntryMechanics.lineSpacing = 1.35f;
        var mechLe = _codexEntryMechanics.gameObject.AddComponent<LayoutElement>();
        mechLe.minHeight = 48f;

        // Tactical Tip Sub-Box (Amber highlight)
        var tipBox = CreateUIObject("TipBox", advBox.transform);
        var tipImg = tipBox.AddComponent<Image>();
        tipImg.sprite = _cardBgSprite;
        tipImg.type = Image.Type.Sliced;
        tipImg.color = new Color(0.24f, 0.16f, 0.08f, 0.85f);
        var tLayout = tipBox.AddComponent<VerticalLayoutGroup>();
        tLayout.padding = new RectOffset(16, 16, 12, 12);
        tLayout.childControlWidth = true;
        tLayout.childControlHeight = true;
        tLayout.childForceExpandHeight = false;

        _codexEntryTips = CreateLabel("EntryTips", tipBox.transform, "Mẹo Chiến Thuật...", 18, FontStyle.Italic, new Color(1f, 0.85f, 0.5f, 1f));
        _codexEntryTips.lineSpacing = 1.35f;
        var tipLe = _codexEntryTips.gameObject.AddComponent<LayoutElement>();
        tipLe.minHeight = 40f;

        SelectCodexCategory(CodexDatabase.EntryCategory.Enemies);
    }

    private void SelectCodexCategory(CodexDatabase.EntryCategory category)
    {
        _codexCategory = category;

        // Cập nhật màu sắc làm nổi bật Tab đang chọn
        Color activeTabColor = new Color(0.22f, 0.50f, 0.85f, 1f);
        Color inactiveTabColor = new Color(0.12f, 0.16f, 0.25f, 0.85f);

        if (_tabEnemiesBtn != null) _tabEnemiesBtn.image.color = (_codexCategory == CodexDatabase.EntryCategory.Enemies) ? activeTabColor : inactiveTabColor;
        if (_tabSkillsBtn != null) _tabSkillsBtn.image.color = (_codexCategory == CodexDatabase.EntryCategory.Skills) ? activeTabColor : inactiveTabColor;
        if (_tabRelicsBtn != null) _tabRelicsBtn.image.color = (_codexCategory == CodexDatabase.EntryCategory.Relics) ? activeTabColor : inactiveTabColor;

        RebuildCodexList();
    }

    private void RebuildCodexList()
    {
        _codexItemBgs.Clear();
        _codexItemAccents.Clear();

        // Xóa các item cũ
        for (int i = _codexListContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(_codexListContainer.GetChild(i).gameObject);
        }

        var entries = CodexDatabase.GetEntriesByCategory(_codexCategory);
        if (entries.Count == 0) return;

        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            var itemObj = CreateUIObject($"Item_{entry.Id}", _codexListContainer);
            var itemRt = itemObj.GetComponent<RectTransform>();
            itemRt.sizeDelta = new Vector2(0f, 68f);
            var le = itemObj.AddComponent<LayoutElement>();
            le.preferredHeight = 68f;
            le.minHeight = 68f;
            le.flexibleWidth = 1f;

            var bgImg = itemObj.AddComponent<Image>();
            bgImg.sprite = _btnBgSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = (entry.Id == _selectedEntryId)
                ? new Color(0.20f, 0.36f, 0.58f, 1f)
                : new Color(0.11f, 0.15f, 0.23f, 0.85f);
            _codexItemBgs.Add(bgImg);

            var btn = itemObj.AddComponent<Button>();
            btn.targetGraphic = bgImg;

            // Thanh vạch sáng bên trái khi được chọn (Left Accent Bar)
            var accentBar = CreateUIObject("AccentBar", itemObj.transform);
            var aRt = accentBar.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0f, 0.1f);
            aRt.anchorMax = new Vector2(0f, 0.9f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.anchoredPosition = new Vector2(2f, 0f);
            aRt.sizeDelta = new Vector2(4f, 0f);
            var aImg = accentBar.AddComponent<Image>();
            aImg.color = new Color(0.35f, 0.85f, 1f, 1f);
            accentBar.SetActive(entry.Id == _selectedEntryId);
            _codexItemAccents.Add(accentBar);

            // Icon Badge hình vuông bên trái (44x44)
            var iconBox = CreateUIObject("IconBox", itemObj.transform);
            var iRt = iconBox.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0f, 0.5f);
            iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(10f, 0f);
            iRt.sizeDelta = new Vector2(44f, 44f);
            var iImg = iconBox.AddComponent<Image>();
            iImg.sprite = _cardBgSprite;
            iImg.type = Image.Type.Sliced;
            iImg.color = entry.ThemeColor * 0.28f + new Color(0.08f, 0.12f, 0.18f, 0.8f);

            var iconTxt = CreateLabel("IconCode", iconBox.transform, entry.IconCode ?? "HB", 16, FontStyle.Bold, entry.ThemeColor);
            SetFullStretch(iconTxt.GetComponent<RectTransform>());
            iconTxt.alignment = TextAnchor.MiddleCenter;

            // Container chứa 2 dòng Text bên phải (Chuẩn LayoutGroup để không bao giờ bị lệch vị trí sang item khác)
            var textContainer = CreateUIObject("TextContainer", itemObj.transform);
            var tRt = textContainer.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = new Vector2(64f, 6f);
            tRt.offsetMax = new Vector2(-10f, -6f);
            var tcLayout = textContainer.AddComponent<VerticalLayoutGroup>();
            tcLayout.spacing = 3f;
            tcLayout.childAlignment = TextAnchor.MiddleLeft;
            tcLayout.childControlWidth = true;
            tcLayout.childControlHeight = true;
            tcLayout.childForceExpandHeight = false;

            var titleLbl = CreateLabel("Title", textContainer.transform, isVi ? entry.TitleVi : entry.TitleEn, 17, FontStyle.Bold, Color.white);
            titleLbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tLe = titleLbl.gameObject.AddComponent<LayoutElement>();
            tLe.preferredHeight = 24f;
            tLe.minHeight = 24f;

            var subLbl = CreateLabel("Sub", textContainer.transform, isVi ? entry.SubtitleVi : entry.SubtitleEn, 14, FontStyle.Normal, new Color(0.60f, 0.85f, 1f, 1f));
            subLbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sLe = subLbl.gameObject.AddComponent<LayoutElement>();
            sLe.preferredHeight = 20f;
            sLe.minHeight = 20f;

            int capturedIdx = i;
            var capturedEntry = entry;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlayUI(UIType.Click);
                SelectCodexEntry(capturedIdx, capturedEntry);
            });
        }

        // Chọn item đầu tiên nếu chưa có item nào được chọn hoặc item cũ không thuộc category này
        int selectIndex = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id == _selectedEntryId)
            {
                selectIndex = i;
                break;
            }
        }
        SelectCodexEntry(selectIndex, entries[selectIndex]);
    }

    private void SelectCodexEntry(int index, CodexDatabase.CodexEntry entry)
    {
        if (entry == null) return;
        _selectedEntryId = entry.Id;

        // Cập nhật trạng thái visual cho toàn bộ list items
        for (int i = 0; i < _codexItemBgs.Count; i++)
        {
            bool isSel = (i == index);
            if (_codexItemBgs[i] != null)
            {
                _codexItemBgs[i].color = isSel
                    ? new Color(0.20f, 0.36f, 0.58f, 1f)
                    : new Color(0.11f, 0.15f, 0.23f, 0.85f);
            }
            if (i < _codexItemAccents.Count && _codexItemAccents[i] != null)
            {
                _codexItemAccents[i].SetActive(isSel);
            }
        }

        DisplayCodexDetail(entry);
    }

    private void DisplayCodexDetail(CodexDatabase.CodexEntry entry)
    {
        if (entry == null) return;
        _selectedEntryId = entry.Id;
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        if (_codexEntryTitle != null) _codexEntryTitle.text = isVi ? entry.TitleVi : entry.TitleEn;
        if (_codexEntrySubtitle != null) _codexEntrySubtitle.text = isVi ? entry.SubtitleVi : entry.SubtitleEn;
        if (_codexEntryBadge != null)
        {
            _codexEntryBadge.text = $"[ {(isVi ? entry.BadgeVi : entry.BadgeEn)} ]";
            _codexEntryBadge.color = entry.ThemeColor;
        }
        if (_codexEntryDesc != null) _codexEntryDesc.text = isVi ? entry.DescriptionVi : entry.DescriptionEn;

        // 4 Thẻ Chỉ Số Cốt Lõi
        var pLabels = isVi ? entry.ParamLabelsVi : entry.ParamLabelsEn;
        var pValues = isVi ? entry.ParamValuesVi : entry.ParamValuesEn;

        if (_codexParamsHeader != null)
        {
            _codexParamsHeader.text = isVi ? "[ BẢNG THÔNG SỐ CỐT LÕI (CORE SPECIFICATIONS) ]" : "[ CORE SPECIFICATIONS ]";
        }

        for (int i = 0; i < 4; i++)
        {
            if (_statCardLabels[i] != null && _statCardValues[i] != null && _statCardLabels[i].transform.parent != null)
            {
                if (pLabels != null && pValues != null && i < pLabels.Length && !string.IsNullOrEmpty(pLabels[i]))
                {
                    _statCardLabels[i].text = pLabels[i];
                    _statCardValues[i].text = pValues[i];
                    _statCardLabels[i].transform.parent.gameObject.SetActive(true);
                }
                else
                {
                    _statCardLabels[i].transform.parent.gameObject.SetActive(false);
                }
            }
        }

        // Tiến trình cấp độ / công thức hợp thể / đặc tính
        string prog = isVi ? entry.ProgressionSummaryVi : entry.ProgressionSummaryEn;
        if (_codexProgressionHeader != null)
        {
            if (_codexCategory == CodexDatabase.EntryCategory.Enemies)
                _codexProgressionHeader.text = isVi ? "[ ĐẶC TÍNH CHIẾN TRƯỜNG ]" : "[ BATTLEFIELD TRAITS ]";
            else if (_codexCategory == CodexDatabase.EntryCategory.Relics)
                _codexProgressionHeader.text = isVi ? "[ NÂNG CẤP & CỘNG DỒN HIỆU ỨNG ]" : "[ UPGRADE & STACKING EFFECTS ]";
            else
                _codexProgressionHeader.text = isVi ? "[ TIẾN TRÌNH CẤP ĐỘ & THĂNG HOA ]" : "[ LEVEL PROGRESSION & EVOLUTION ]";
        }
        if (_codexProgressionText != null)
        {
            _codexProgressionText.text = !string.IsNullOrEmpty(prog) ? prog : (isVi ? "Không có dữ liệu tiến trình." : "No progression data.");
        }

        // Cơ chế & Lời khuyên chiến thuật
        if (_codexEntryMechanics != null)
        {
            _codexEntryMechanics.text = isVi
                ? $"<b>[ CƠ CHẾ HOẠT ĐỘNG ]</b>  {entry.MechanicsVi}"
                : $"<b>[ CORE MECHANICS ]</b>  {entry.MechanicsEn}";
        }

        if (_codexEntryTips != null)
        {
            _codexEntryTips.text = isVi
                ? $"<b>[ LỜI KHUYÊN CHIẾN THUẬT ]</b>  {entry.TipsVi}"
                : $"<b>[ TACTICAL ADVISORY ]</b>  {entry.TipsEn}";
        }
    }

    // =========================================================================
    // TALENT TREE PANEL (CÂY THIÊN PHÚ NÂNG CẤP VĨNH VIỄN)
    // =========================================================================
    private void BuildTalentTreePanel(Transform parent)
    {
        _talentsPanel = CreateUIObject("TalentsPanel", parent);
        SetFullStretch(_talentsPanel.GetComponent<RectTransform>(), 0f, 75f);

        // Header Sub-bar: Nút Quay Lại + Tổng Linh Thạch + Nút Tẩy Điểm
        var topBarObj = CreateUIObject("TalentTopBar", _talentsPanel.transform);
        var barRt = topBarObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -10f);
        barRt.sizeDelta = new Vector2(0f, 48f);

        var backBtn = CreateSimpleButton("BackBtn", topBarObj.transform, "◀ QUAY LẠI [ESC]", 14, new Vector2(160f, 42f), () => SwitchState(MenuState.Home));
        var bRt = backBtn.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0.5f);
        bRt.anchorMax = new Vector2(0f, 0.5f);
        bRt.pivot = new Vector2(0f, 0.5f);
        bRt.anchoredPosition = new Vector2(30f, 0f);

        _talentSummaryText = CreateLabel("TalentSummary", topBarObj.transform, "ĐÃ ĐẦU TƯ: 0 LINH THẠCH", 16, FontStyle.Bold, new Color(0.4f, 0.9f, 1f, 1f));
        var sRt = _talentSummaryText.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.5f);
        sRt.anchorMax = new Vector2(0.5f, 0.5f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = new Vector2(0f, 0f);
        sRt.sizeDelta = new Vector2(450f, 40f);
        _talentSummaryText.alignment = TextAnchor.MiddleCenter;

        _respecBtn = CreateSimpleButton("RespecBtn", topBarObj.transform, "🔄 TẨY ĐIỂM (HOÀN 100%)", 14, new Vector2(230f, 42f), () =>
        {
            MetaProgressionService.Instance.ResetAllTalents();
            AudioManager.Instance.PlayUI(UIType.Reroll);
        });
        _respecBtn.image.color = new Color(0.85f, 0.28f, 0.28f, 1f);
        _respecBtnText = _respecBtn.GetComponentInChildren<Text>();
        var rRt = _respecBtn.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(1f, 0.5f);
        rRt.anchorMax = new Vector2(1f, 0.5f);
        rRt.pivot = new Vector2(1f, 0.5f);
        rRt.anchoredPosition = new Vector2(-30f, 0f);

        // 3 Cột tương ứng 3 Nhánh
        var columnsObj = CreateUIObject("ColumnsContainer", _talentsPanel.transform);
        var colsRt = columnsObj.GetComponent<RectTransform>();
        colsRt.anchorMin = new Vector2(0.02f, 0.02f);
        colsRt.anchorMax = new Vector2(0.98f, 0.89f);
        colsRt.offsetMin = colsRt.offsetMax = Vector2.zero;

        var hLayout = columnsObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 16f;
        hLayout.childAlignment = TextAnchor.UpperCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = false;

        _healthNodesContainer = CreateBranchColumn(columnsObj.transform, "🛡️ THỂ LỰC (SURVIVAL)", "Khí Huyết & Thân Thể Bất Hoại", new Color(0.2f, 0.85f, 0.45f, 1f), out _healthBranchTitle, out _healthBranchSub);
        _offenseNodesContainer = CreateBranchColumn(columnsObj.transform, "⚔️ CÔNG KÍCH (OFFENSE)", "Kiếm Ý & Sát Thương Toàn Diện", new Color(0.95f, 0.35f, 0.35f, 1f), out _offenseBranchTitle, out _offenseBranchSub);
        _fortuneNodesContainer = CreateBranchColumn(columnsObj.transform, "✨ VẬN MỆNH (FORTUNE)", "Cơ Duyên & Đạo Tài Vô Song", new Color(0.95f, 0.78f, 0.22f, 1f), out _fortuneBranchTitle, out _fortuneBranchSub);

        BuildTalentNodesUI();
        BuildTalentTooltip(_talentsPanel.transform);
    }

    private Transform CreateBranchColumn(Transform parent, string title, string subtitle, Color accentColor, out Text titleOut, out Text subOut)
    {
        var colObj = CreateUIObject($"Col_{title}", parent);
        var colImg = colObj.AddComponent<Image>();
        colImg.sprite = _cardBgSprite;
        colImg.type = Image.Type.Sliced;
        colImg.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);

        var colOutline = colObj.AddComponent<Outline>();
        colOutline.effectColor = accentColor * 0.5f;
        colOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var colLe = colObj.AddComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;

        var vLayout = colObj.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 10f;
        vLayout.padding = new RectOffset(10, 10, 12, 12);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        // Top Header (Height 60px, big 22px title and 14px subtitle with dedicated LayoutElements)
        var headerBox = CreateUIObject("HeaderBox", colObj.transform);
        var hbLe = headerBox.AddComponent<LayoutElement>();
        hbLe.preferredHeight = 60f;
        hbLe.minHeight = 60f;
        hbLe.flexibleHeight = 0f;

        var hbLayout = headerBox.AddComponent<VerticalLayoutGroup>();
        hbLayout.spacing = 3f;
        hbLayout.childControlWidth = true;
        hbLayout.childControlHeight = true;
        hbLayout.childForceExpandWidth = true;
        hbLayout.childForceExpandHeight = false;

        titleOut = CreateLabel("Title", headerBox.transform, title, 22, FontStyle.Bold, accentColor);
        titleOut.alignment = TextAnchor.MiddleCenter;
        var tLe = titleOut.gameObject.AddComponent<LayoutElement>();
        tLe.preferredHeight = 30f;
        tLe.flexibleHeight = 0f;

        subOut = CreateLabel("Subtitle", headerBox.transform, subtitle, 14, FontStyle.Normal, new Color(0.85f, 0.92f, 1f, 0.95f));
        subOut.alignment = TextAnchor.MiddleCenter;
        var sLe = subOut.gameObject.AddComponent<LayoutElement>();
        sLe.preferredHeight = 22f;
        sLe.flexibleHeight = 0f;

        CreateDivider(colObj.transform, accentColor * 0.6f);

        return colObj.transform;
    }

    private void BuildTalentNodesUI()
    {
        _nodeUIItems.Clear();

        foreach (var node in MetaProgressionService.Instance.GetAllNodes())
        {
            Transform targetParent = node.Branch switch
            {
                MetaProgressionService.TalentBranch.Health => _healthNodesContainer,
                MetaProgressionService.TalentBranch.Offense => _offenseNodesContainer,
                _ => _fortuneNodesContainer
            };

            var item = CreateNodeCard(targetParent, node);
            _nodeUIItems[node.Id] = item;
        }

        // Thêm Thẻ Tổng Quan Hiệu Quả Nhánh cho Thể Lực và Công Kích để cân bằng 5 thẻ mỗi cột
        CreateBranchMasteryCard(_healthNodesContainer, MetaProgressionService.TalentBranch.Health);
        CreateBranchMasteryCard(_offenseNodesContainer, MetaProgressionService.TalentBranch.Offense);
    }

    private string GetNodeSymbol(string nodeId)
    {
        return nodeId switch
        {
            // Survival
            "hp_max" => "HP",
            "hp_regen" => "REG",
            "hp_armor" => "DEF",
            "hp_revival" => "★",
            // Offense
            "atk_global" => "ATK",
            "atk_cdr" => "CDR",
            "atk_crit" => "CRT",
            "atk_apex" => "★",
            // Fortune
            "util_speed" => "SPD",
            "util_pickup" => "MAG",
            "util_drop" => "LUK",
            "util_gold" => "GLD",
            "util_apex" => "★",
            _ => "◆"
        };
    }

    private NodeUIItem CreateNodeCard(Transform parent, MetaProgressionService.TalentNodeInfo node)
    {
        var cardObj = CreateUIObject($"Node_{node.Id}", parent);
        var cardImg = cardObj.AddComponent<Image>();
        cardImg.sprite = _cardBgSprite;
        cardImg.type = Image.Type.Sliced;

        Color branchAccent = node.Branch switch
        {
            MetaProgressionService.TalentBranch.Health => new Color(0.2f, 0.9f, 0.5f, 1f),
            MetaProgressionService.TalentBranch.Offense => new Color(1f, 0.38f, 0.38f, 1f),
            _ => new Color(1f, 0.82f, 0.25f, 1f)
        };

        Color cardBgColor = node.Branch switch
        {
            MetaProgressionService.TalentBranch.Health => new Color(0.08f, 0.16f, 0.14f, 0.98f),
            MetaProgressionService.TalentBranch.Offense => new Color(0.17f, 0.10f, 0.12f, 0.98f),
            _ => new Color(0.16f, 0.14f, 0.08f, 0.98f)
        };
        cardImg.color = cardBgColor;

        var cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = branchAccent * 0.85f;
        cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // LayoutElement for card (Tối ưu 124px cân đối, thanh thoát, không gian thoáng đãng)
        var cardLe = cardObj.AddComponent<LayoutElement>();
        cardLe.preferredHeight = 124f;
        cardLe.minHeight = 124f;
        cardLe.flexibleHeight = 0f;
        cardLe.flexibleWidth = 1f;

        // Card Content Horizontal Layout: [IconBadge: 58px] [InfoArea: Flex] [ActionArea: 130px]
        var hLayout = cardObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 12f;
        hLayout.padding = new RectOffset(12, 12, 10, 10);
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.childAlignment = TextAnchor.MiddleCenter;

        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        // 1. LEFT: Icon & Tier Badge (Fixed Width: 58px)
        var iconBox = CreateUIObject("IconBox", cardObj.transform);
        var ibLe = iconBox.AddComponent<LayoutElement>();
        ibLe.preferredWidth = 58f;
        ibLe.minWidth = 58f;
        ibLe.flexibleWidth = 0f;

        var ibImg = iconBox.AddComponent<Image>();
        ibImg.sprite = _badgeBgSprite;
        ibImg.type = Image.Type.Sliced;
        ibImg.color = new Color(0.07f, 0.10f, 0.15f, 0.95f);
        var ibOutline = iconBox.AddComponent<Outline>();
        ibOutline.effectColor = branchAccent * 0.9f;
        ibOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var ibLayout = iconBox.AddComponent<VerticalLayoutGroup>();
        ibLayout.spacing = 2f;
        ibLayout.padding = new RectOffset(2, 2, 8, 8);
        ibLayout.childControlWidth = true;
        ibLayout.childControlHeight = true;
        ibLayout.childForceExpandWidth = true;
        ibLayout.childForceExpandHeight = true;

        string iconSymbol = GetNodeSymbol(node.Id);
        Color iconColor = node.Tier >= 3 ? new Color(1f, 0.9f, 0.35f, 1f) : branchAccent;
        var iconTxt = CreateLabel("Icon", iconBox.transform, iconSymbol, 26, FontStyle.Bold, iconColor);
        iconTxt.alignment = TextAnchor.MiddleCenter;

        string tierText = node.Tier >= 3 ? "APEX" : $"T{node.Tier}";
        Color tierColor = node.Tier >= 3 ? new Color(1f, 0.88f, 0.3f, 1f) : new Color(0.75f, 0.85f, 0.98f, 0.9f);
        var tierTxt = CreateLabel("Tier", iconBox.transform, tierText, 12, FontStyle.Bold, tierColor);
        tierTxt.alignment = TextAnchor.MiddleCenter;

        // 2. CENTER: Info & Progress Area (Flexible Width)
        var infoBox = CreateUIObject("InfoBox", cardObj.transform);
        var infoLe = infoBox.AddComponent<LayoutElement>();
        infoLe.preferredWidth = 0f;
        infoLe.flexibleWidth = 1f;

        var infoLayout = infoBox.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 5f;
        infoLayout.padding = new RectOffset(0, 0, 0, 0);
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        // Row 1: Title + Level Badge
        var titleRow = CreateUIObject("TitleRow", infoBox.transform);
        var trLe = titleRow.AddComponent<LayoutElement>();
        trLe.preferredHeight = 26f;
        trLe.minHeight = 24f;
        trLe.flexibleHeight = 0f;

        var trLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
        trLayout.spacing = 8f;
        trLayout.childControlWidth = true;
        trLayout.childControlHeight = true;
        trLayout.childForceExpandWidth = false;
        trLayout.childForceExpandHeight = true;

        Color titleColor = node.Tier >= 3 ? new Color(1f, 0.9f, 0.45f, 1f) : Color.white;
        var nameTxt = CreateLabel("Name", titleRow.transform, isVi ? node.NameVi : node.NameEn, 19, FontStyle.Bold, titleColor);
        nameTxt.alignment = TextAnchor.MiddleLeft;
        var nameLe = nameTxt.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 0f;
        nameLe.flexibleWidth = 1f;

        var lvlBadgeTxt = CreateLabel("LevelBadge", titleRow.transform, $"Lv {node.CurrentLevel}/{node.MaxLevel}", 17, FontStyle.Bold, branchAccent);
        lvlBadgeTxt.alignment = TextAnchor.MiddleRight;
        var lvlBadgeLe = lvlBadgeTxt.gameObject.AddComponent<LayoutElement>();
        lvlBadgeLe.preferredWidth = 85f;
        lvlBadgeLe.flexibleWidth = 0f;

        // Row 2: Segmented Power Cells (Thanh năng lượng phân đoạn to bản, phát sáng rực rỡ)
        var powerCellsRow = CreateUIObject("PowerCellsRow", infoBox.transform);
        var prLe = powerCellsRow.AddComponent<LayoutElement>();
        prLe.preferredHeight = 14f;
        prLe.minHeight = 14f;
        prLe.flexibleHeight = 0f;

        var prLayout = powerCellsRow.AddComponent<HorizontalLayoutGroup>();
        prLayout.spacing = 4f;
        prLayout.childControlWidth = true;
        prLayout.childControlHeight = true;
        prLayout.childForceExpandWidth = true;
        prLayout.childForceExpandHeight = true;
        prLayout.childAlignment = TextAnchor.MiddleCenter;

        var pipImages = new List<Image>();
        for (int p = 0; p < node.MaxLevel; p++)
        {
            var cellObj = CreateUIObject($"Cell_{p}", powerCellsRow.transform);
            var cellLe = cellObj.AddComponent<LayoutElement>();
            cellLe.preferredHeight = 14f;
            cellLe.minHeight = 14f;
            cellLe.flexibleWidth = 1f;

            var cellImg = cellObj.AddComponent<Image>();
            cellImg.sprite = _powerCellSprite;
            cellImg.type = Image.Type.Sliced;
            cellImg.color = (p < node.CurrentLevel) ? branchAccent : new Color(0.12f, 0.16f, 0.24f, 0.85f);
            pipImages.Add(cellImg);
        }

        // Row 3: Short Stat Tag (Capsule nhãn hiển thị chỉ số hiện tại ngắn gọn, tinh tế)
        var tagBox = CreateUIObject("StatusTag", infoBox.transform);
        var tagLe = tagBox.AddComponent<LayoutElement>();
        tagLe.preferredHeight = 24f;
        tagLe.minHeight = 24f;
        tagLe.flexibleHeight = 0f;
        tagLe.flexibleWidth = 1f;

        var tagImg = tagBox.AddComponent<Image>();
        tagImg.sprite = _badgeBgSprite;
        tagImg.type = Image.Type.Sliced;
        tagImg.color = new Color(0.06f, 0.10f, 0.16f, 0.9f);

        var tagOutline = tagBox.AddComponent<Outline>();
        tagOutline.effectColor = branchAccent * 0.45f;
        tagOutline.effectDistance = new Vector2(1f, -1f);

        var tagTxt = CreateLabel("StatusTagText", tagBox.transform, "", 12, FontStyle.Bold, Color.white);
        tagTxt.alignment = TextAnchor.MiddleCenter;
        SetFullStretch(tagTxt.GetComponent<RectTransform>(), 4f, 0f);

        // 3. RIGHT: Action Area (Fixed Width: 130px)
        var actionBox = CreateUIObject("ActionBox", cardObj.transform);
        var abLe = actionBox.AddComponent<LayoutElement>();
        abLe.preferredWidth = 130f;
        abLe.minWidth = 130f;
        abLe.flexibleWidth = 0f;

        // Center elements vertically inside Action Area
        // A. Maxed Badge
        var maxBadge = CreateUIObject("MaxBadge", actionBox.transform);
        var mbRt = maxBadge.GetComponent<RectTransform>();
        mbRt.anchorMin = new Vector2(0.5f, 0.5f);
        mbRt.anchorMax = new Vector2(0.5f, 0.5f);
        mbRt.pivot = new Vector2(0.5f, 0.5f);
        mbRt.anchoredPosition = Vector2.zero;
        mbRt.sizeDelta = new Vector2(126f, 52f);

        var mbImg = maxBadge.AddComponent<Image>();
        mbImg.sprite = _btnBgSprite;
        mbImg.type = Image.Type.Sliced;
        mbImg.color = new Color(0.08f, 0.28f, 0.16f, 0.95f);
        var mbOutline = maxBadge.AddComponent<Outline>();
        mbOutline.effectColor = new Color(0.2f, 0.9f, 0.45f, 0.9f);
        mbOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var maxTxt = CreateLabel("MaxTxt", maxBadge.transform, "✔ TỐI ĐA", 16, FontStyle.Bold, new Color(0.35f, 0.95f, 0.5f, 1f));
        maxTxt.alignment = TextAnchor.MiddleCenter;
        SetFullStretch(maxTxt.GetComponent<RectTransform>());

        // B. Locked Badge
        var lockBadge = CreateUIObject("LockBadge", actionBox.transform);
        var lbRt = lockBadge.GetComponent<RectTransform>();
        lbRt.anchorMin = new Vector2(0.5f, 0.5f);
        lbRt.anchorMax = new Vector2(0.5f, 0.5f);
        lbRt.pivot = new Vector2(0.5f, 0.5f);
        lbRt.anchoredPosition = Vector2.zero;
        lbRt.sizeDelta = new Vector2(126f, 52f);

        var lbImg = lockBadge.AddComponent<Image>();
        lbImg.sprite = _btnBgSprite;
        lbImg.type = Image.Type.Sliced;
        lbImg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
        var lbOutline = lockBadge.AddComponent<Outline>();
        lbOutline.effectColor = new Color(0.35f, 0.45f, 0.6f, 0.7f);
        lbOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var lockedTxt = CreateLabel("LockedTxt", lockBadge.transform, "🔒 CHƯA MỞ", 15, FontStyle.Bold, new Color(0.72f, 0.8f, 0.92f, 1f));
        lockedTxt.alignment = TextAnchor.MiddleCenter;
        SetFullStretch(lockedTxt.GetComponent<RectTransform>());

        // C. Upgrade Button
        var upBtnObj = CreateUIObject("UpgradeBtn", actionBox.transform);
        var upRt = upBtnObj.GetComponent<RectTransform>();
        upRt.anchorMin = new Vector2(0.5f, 0.5f);
        upRt.anchorMax = new Vector2(0.5f, 0.5f);
        upRt.pivot = new Vector2(0.5f, 0.5f);
        upRt.anchoredPosition = Vector2.zero;
        upRt.sizeDelta = new Vector2(126f, 56f);

        var upImg = upBtnObj.AddComponent<Image>();
        upImg.sprite = _btnBgSprite;
        upImg.type = Image.Type.Sliced;
        upImg.color = new Color(0.14f, 0.62f, 0.35f, 1f);

        var upOutline = upBtnObj.AddComponent<Outline>();
        upOutline.effectColor = new Color(0.3f, 0.95f, 0.55f, 0.85f);
        upOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var upBtn = upBtnObj.AddComponent<Button>();
        upBtn.targetGraphic = upImg;
        var upColors = upBtn.colors;
        upColors.normalColor = Color.white;
        upColors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        upColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        upColors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        upBtn.colors = upColors;
        upBtn.onClick.AddListener(() =>
        {
            if (MetaProgressionService.Instance.UpgradeNode(node.Id))
            {
                AudioManager.Instance.PlayUI(UIType.PerkSelect);
            }
        });

        var upBtnLayout = upBtnObj.AddComponent<VerticalLayoutGroup>();
        upBtnLayout.spacing = 2f;
        upBtnLayout.padding = new RectOffset(4, 4, 4, 4);
        upBtnLayout.childControlWidth = true;
        upBtnLayout.childControlHeight = true;
        upBtnLayout.childForceExpandWidth = true;
        upBtnLayout.childForceExpandHeight = true;

        var costTxt = CreateLabel("Cost", upBtnObj.transform, $"💎 {node.NextCost}", 20, FontStyle.Bold, new Color(1f, 0.92f, 0.35f, 1f));
        costTxt.alignment = TextAnchor.MiddleCenter;

        var upLabelTxt = CreateLabel("Label", upBtnObj.transform, isVi ? "NÂNG CẤP" : "UPGRADE", 14, FontStyle.Bold, Color.white);
        upLabelTxt.alignment = TextAnchor.MiddleCenter;

        // Thêm Hover Trigger Component cho thẻ thiên phú
        var hoverTrigger = cardObj.AddComponent<TalentCardHoverHandler>();
        hoverTrigger.MenuUI = this;
        hoverTrigger.NodeId = node.Id;
        hoverTrigger.CardOutline = cardOutline;

        return new NodeUIItem
        {
            NodeId = node.Id,
            CardBg = cardImg,
            CardOutline = cardOutline,
            IconText = iconTxt,
            TierBadgeText = tierTxt,
            NameText = nameTxt,
            LevelBadgeText = lvlBadgeTxt,
            PipImages = pipImages,
            StatusTagObj = tagBox,
            StatusTagBg = tagImg,
            StatusTagOutline = tagOutline,
            StatusTagText = tagTxt,
            UpgradeBtn = upBtn,
            UpgradeCostText = costTxt,
            UpgradeBtnText = upLabelTxt,
            MaxedBadge = maxBadge,
            MaxedBadgeText = maxTxt,
            LockedBadge = lockBadge,
            LockedBadgeText = lockedTxt
        };
    }

    private void CreateBranchMasteryCard(Transform parent, MetaProgressionService.TalentBranch branch)
    {
        bool isHealth = branch == MetaProgressionService.TalentBranch.Health;
        Color accentColor = isHealth ? new Color(0.2f, 0.9f, 0.5f, 1f) : new Color(1f, 0.38f, 0.38f, 1f);
        Color cardBg = isHealth ? new Color(0.06f, 0.13f, 0.12f, 0.98f) : new Color(0.14f, 0.08f, 0.10f, 0.98f);

        var cardObj = CreateUIObject(isHealth ? "MasteryCard_Health" : "MasteryCard_Offense", parent);
        var cardImg = cardObj.AddComponent<Image>();
        cardImg.sprite = _cardBgSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = cardBg;

        var cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = accentColor * 0.75f;
        cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var cardLe = cardObj.AddComponent<LayoutElement>();
        cardLe.preferredHeight = 124f;
        cardLe.minHeight = 124f;
        cardLe.flexibleHeight = 0f;
        cardLe.flexibleWidth = 1f;

        var hLayout = cardObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 12f;
        hLayout.padding = new RectOffset(12, 12, 10, 10);
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.childAlignment = TextAnchor.MiddleCenter;

        // 1. Icon Box
        var iconBox = CreateUIObject("IconBox", cardObj.transform);
        var ibLe = iconBox.AddComponent<LayoutElement>();
        ibLe.preferredWidth = 58f;
        ibLe.minWidth = 58f;
        ibLe.flexibleWidth = 0f;

        var ibImg = iconBox.AddComponent<Image>();
        ibImg.sprite = _badgeBgSprite;
        ibImg.type = Image.Type.Sliced;
        ibImg.color = new Color(0.07f, 0.10f, 0.15f, 0.95f);
        var ibOutline = iconBox.AddComponent<Outline>();
        ibOutline.effectColor = accentColor * 0.85f;
        ibOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var ibLayout = iconBox.AddComponent<VerticalLayoutGroup>();
        ibLayout.spacing = 2f;
        ibLayout.padding = new RectOffset(2, 2, 8, 8);
        ibLayout.childControlWidth = true;
        ibLayout.childControlHeight = true;
        ibLayout.childForceExpandWidth = true;
        ibLayout.childForceExpandHeight = true;

        var iconTxt = CreateLabel("Icon", iconBox.transform, isHealth ? "🛡️" : "⚔️", 26, FontStyle.Bold, accentColor);
        iconTxt.alignment = TextAnchor.MiddleCenter;

        var tierTxt = CreateLabel("Tier", iconBox.transform, "TỔNG", 12, FontStyle.Bold, new Color(0.85f, 0.92f, 1f, 0.9f));
        tierTxt.alignment = TextAnchor.MiddleCenter;

        // 2. Info Box
        var infoBox = CreateUIObject("InfoBox", cardObj.transform);
        var infoLe = infoBox.AddComponent<LayoutElement>();
        infoLe.preferredWidth = 0f;
        infoLe.flexibleWidth = 1f;

        var infoLayout = infoBox.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 3f;
        infoLayout.padding = new RectOffset(2, 2, 2, 2);
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        var titleTxt = CreateLabel("MasteryTitle", infoBox.transform, "", 17, FontStyle.Bold, accentColor);
        titleTxt.alignment = TextAnchor.MiddleLeft;
        var tLe = titleTxt.gameObject.AddComponent<LayoutElement>();
        tLe.preferredHeight = 24f;
        tLe.flexibleHeight = 0f;

        var line1Txt = CreateLabel("StatLine1", infoBox.transform, "", 14, FontStyle.Bold, new Color(0.95f, 0.98f, 1f, 1f));
        line1Txt.alignment = TextAnchor.MiddleLeft;
        var l1Le = line1Txt.gameObject.AddComponent<LayoutElement>();
        l1Le.preferredHeight = 22f;
        l1Le.flexibleHeight = 0f;

        var line2Txt = CreateLabel("StatLine2", infoBox.transform, "", 14, FontStyle.Bold, new Color(0.95f, 0.98f, 1f, 1f));
        line2Txt.alignment = TextAnchor.MiddleLeft;
        var l2Le = line2Txt.gameObject.AddComponent<LayoutElement>();
        l2Le.preferredHeight = 22f;
        l2Le.flexibleHeight = 0f;

        // 3. Right Action / Status Box
        var actionBox = CreateUIObject("ActionBox", cardObj.transform);
        var abLe = actionBox.AddComponent<LayoutElement>();
        abLe.preferredWidth = 130f;
        abLe.minWidth = 130f;
        abLe.flexibleWidth = 0f;

        var badgeObj = CreateUIObject("MasteryBadge", actionBox.transform);
        var bRt = badgeObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(126f, 52f);

        var bImg = badgeObj.AddComponent<Image>();
        bImg.sprite = _btnBgSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = isHealth ? new Color(0.08f, 0.28f, 0.16f, 0.95f) : new Color(0.28f, 0.10f, 0.14f, 0.95f);
        var bOutline = badgeObj.AddComponent<Outline>();
        bOutline.effectColor = accentColor * 0.9f;
        bOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var badgeTxt = CreateLabel("BadgeTxt", badgeObj.transform, isHealth ? "★ THỂ LỰC" : "★ CHIẾN LỰC", 15, FontStyle.Bold, accentColor);
        badgeTxt.alignment = TextAnchor.MiddleCenter;
        SetFullStretch(badgeTxt.GetComponent<RectTransform>());

        if (isHealth)
        {
            _healthMasteryTitle = titleTxt;
            _healthMasteryLine1 = line1Txt;
            _healthMasteryLine2 = line2Txt;
            _healthMasteryBadge = badgeTxt;
        }
        else
        {
            _offenseMasteryTitle = titleTxt;
            _offenseMasteryLine1 = line1Txt;
            _offenseMasteryLine2 = line2Txt;
            _offenseMasteryBadge = badgeTxt;
        }
    }

    private void RefreshTalentTreeUI()
    {
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;
        int totalSpent = MetaProgressionService.Instance.GetTotalSpentStones();

        _talentSummaryText.text = isVi
            ? $"ĐÃ ĐẦU TƯ: <b>{totalSpent:N0}</b> 💎 LINH THẠCH"
            : $"INVESTED: <b>{totalSpent:N0}</b> 💎 SPIRIT STONES";

        _respecBtnText.text = isVi ? "🔄 TẨY ĐIỂM (HOÀN 100%)" : "🔄 RESPEC (100% REFUND)";

        if (_healthBranchTitle != null)
        {
            _healthBranchTitle.text = isVi ? "🛡️ THỂ LỰC (SURVIVAL)" : "🛡️ SURVIVAL • VITALITY";
            _healthBranchSub.text = isVi ? "Khí Huyết & Thân Thể Bất Hoại" : "Max HP, Regen & Damage Mitigation";
            _offenseBranchTitle.text = isVi ? "⚔️ CÔNG KÍCH (OFFENSE)" : "⚔️ OFFENSE • COMBAT";
            _offenseBranchSub.text = isVi ? "Kiếm Ý & Sát Thương Toàn Diện" : "Global Damage, Cooldown & Crit";
            _fortuneBranchTitle.text = isVi ? "✨ VẬN MỆNH (FORTUNE)" : "✨ FORTUNE • PROSPERITY";
            _fortuneBranchSub.text = isVi ? "Cơ Duyên & Đạo Tài Vô Song" : "Speed, Magnet, Drops & Rerolls";
        }

        foreach (var kvp in _nodeUIItems)
        {
            var node = MetaProgressionService.Instance.GetNode(kvp.Key);
            if (node == null) continue;

            var item = kvp.Value;
            item.NameText.text = isVi ? node.NameVi : node.NameEn;

            Color branchAccent = node.Branch switch
            {
                MetaProgressionService.TalentBranch.Health => new Color(0.2f, 0.9f, 0.5f, 1f),
                MetaProgressionService.TalentBranch.Offense => new Color(1f, 0.38f, 0.38f, 1f),
                _ => new Color(1f, 0.82f, 0.25f, 1f)
            };

            bool isLocked = MetaProgressionService.Instance.IsNodeLocked(node.Id);

            GetNodeStatStrings(node, isVi, out string curBonus, out string nextBonus, out string maxBonus, out string shortTag);
            item.StatusTagText.text = shortTag;

            if (node.IsMaxed)
            {
                item.MaxedBadge.SetActive(true);
                item.LockedBadge.SetActive(false);
                item.UpgradeBtn.gameObject.SetActive(false);
                item.MaxedBadgeText.text = isVi ? "✔ TỐI ĐA" : "✔ MAXED";
                item.LevelBadgeText.text = "MAX";
                item.LevelBadgeText.color = new Color(1f, 0.88f, 0.3f, 1f);
                item.CardOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.8f);

                item.StatusTagText.color = new Color(1f, 0.88f, 0.35f, 1f);
                item.StatusTagOutline.effectColor = new Color(1f, 0.85f, 0.25f, 0.7f);
                item.StatusTagBg.color = new Color(0.18f, 0.14f, 0.06f, 0.95f);

                for (int i = 0; i < item.PipImages.Count; i++)
                {
                    item.PipImages[i].color = new Color(1f, 0.88f, 0.3f, 1f);
                }
            }
            else if (isLocked)
            {
                item.MaxedBadge.SetActive(false);
                item.LockedBadge.SetActive(true);
                item.UpgradeBtn.gameObject.SetActive(false);
                item.LockedBadgeText.text = isVi ? "🔒 CHƯA MỞ" : "🔒 LOCKED";

                item.StatusTagText.color = new Color(1f, 0.55f, 0.55f, 1f);
                item.StatusTagOutline.effectColor = new Color(0.9f, 0.3f, 0.3f, 0.6f);
                item.StatusTagBg.color = new Color(0.18f, 0.08f, 0.10f, 0.95f);

                item.LevelBadgeText.text = $"Lv {node.CurrentLevel}/{node.MaxLevel}";
                item.LevelBadgeText.color = new Color(0.65f, 0.72f, 0.85f, 0.85f);
                item.CardOutline.effectColor = new Color(0.3f, 0.35f, 0.45f, 0.4f);

                for (int i = 0; i < item.PipImages.Count; i++)
                {
                    item.PipImages[i].color = (i < node.CurrentLevel) ? branchAccent : new Color(0.12f, 0.16f, 0.24f, 0.85f);
                }
            }
            else
            {
                item.MaxedBadge.SetActive(false);
                item.LockedBadge.SetActive(false);
                item.UpgradeBtn.gameObject.SetActive(true);

                item.StatusTagText.color = node.CurrentLevel > 0 ? Color.white : new Color(0.75f, 0.85f, 0.95f, 0.85f);
                item.StatusTagOutline.effectColor = branchAccent * 0.45f;
                item.StatusTagBg.color = new Color(0.06f, 0.10f, 0.16f, 0.9f);

                item.LevelBadgeText.text = $"Lv {node.CurrentLevel}/{node.MaxLevel}";
                item.LevelBadgeText.color = branchAccent;
                item.CardOutline.effectColor = branchAccent * 0.85f;

                for (int i = 0; i < item.PipImages.Count; i++)
                {
                    item.PipImages[i].color = (i < node.CurrentLevel) ? branchAccent : new Color(0.12f, 0.16f, 0.24f, 0.85f);
                }

                item.UpgradeCostText.text = $"💎 {node.NextCost:N0}";
                bool canAfford = MetaProgressionService.Instance.CanUpgrade(node.Id);
                item.UpgradeBtn.interactable = canAfford;

                if (canAfford)
                {
                    item.UpgradeCostText.color = new Color(1f, 0.92f, 0.35f, 1f);
                    item.UpgradeBtnText.text = isVi ? "NÂNG CẤP" : "UPGRADE";
                    item.UpgradeBtnText.color = Color.white;
                    item.UpgradeBtn.image.color = new Color(0.14f, 0.62f, 0.35f, 1f);
                }
                else
                {
                    item.UpgradeCostText.color = new Color(1f, 0.45f, 0.45f, 1f);
                    item.UpgradeBtnText.text = isVi ? "THIẾU ĐÁ" : "NEED STONES";
                    item.UpgradeBtnText.color = new Color(0.75f, 0.75f, 0.8f, 0.75f);
                    item.UpgradeBtn.image.color = new Color(0.18f, 0.22f, 0.3f, 0.7f);
                }
            }
        }

        // Cập nhật tooltip nếu đang hover vào node
        if (!string.IsNullOrEmpty(_currentHoveredNodeId) && _talentTooltipObj != null && _talentTooltipObj.activeSelf)
        {
            ShowTalentTooltip(_currentHoveredNodeId);
        }

        // Cập nhật Thẻ Tổng Quan Hiệu Quả Nhánh Thể Lực
        if (_healthMasteryLine1 != null)
        {
            _healthMasteryTitle.text = isVi ? "🛡️ TỔNG HIỆU QUẢ THỂ LỰC" : "🛡️ SURVIVAL MASTERY";
            _healthMasteryBadge.text = isVi ? "★ THỂ LỰC" : "★ VITALITY";

            float hp = MetaProgressionService.Instance.GetBonusMaxHP();
            float regen = MetaProgressionService.Instance.GetBonusHPRegen();
            float armor = MetaProgressionService.Instance.GetDamageReductionPercent() * 100f;
            bool revival = MetaProgressionService.Instance.HasRevival();

            _healthMasteryLine1.text = isVi
                ? $"• Máu tối đa: <color=#4ADE80><b>+{hp:0} HP</b></color>    • Hồi máu: <color=#4ADE80><b>+{regen:0.0} HP/s</b></color>"
                : $"• Max HP: <color=#4ADE80><b>+{hp:0} HP</b></color>    • Health Regen: <color=#4ADE80><b>+{regen:0.0} HP/s</b></color>";

            string revivalStr = revival
                ? (isVi ? "<color=#4ADE80><b>ĐÃ KÍCH HOẠT</b></color>" : "<color=#4ADE80><b>ACTIVE</b></color>")
                : (isVi ? "<color=#94A3B8>Chưa mở</color>" : "<color=#94A3B8>Locked</color>");

            _healthMasteryLine2.text = isVi
                ? $"• Giảm sát thương: <color=#4ADE80><b>-{armor:0.0}%</b></color>    • Hồi sinh: {revivalStr}"
                : $"• Damage Taken: <color=#4ADE80><b>-{armor:0.0}%</b></color>    • Revival: {revivalStr}";
        }

        // Cập nhật Thẻ Tổng Quan Hiệu Quả Nhánh Công Kích
        if (_offenseMasteryLine1 != null)
        {
            _offenseMasteryTitle.text = isVi ? "⚔️ TỔNG HIỆU QUẢ CÔNG KÍCH" : "⚔️ OFFENSE MASTERY";
            _offenseMasteryBadge.text = isVi ? "★ CHIẾN LỰC" : "★ COMBAT";

            float atkPercent = (MetaProgressionService.Instance.GetGlobalATKMultiplier() - 1f) * 100f;
            float cdr = MetaProgressionService.Instance.GetCooldownReductionPercent() * 100f;
            float crit = MetaProgressionService.Instance.GetCritChancePercent() * 100f;
            float critDmg = MetaProgressionService.Instance.GetCritDamageMultiplier();

            _offenseMasteryLine1.text = isVi
                ? $"• Sát thương: <color=#F87171><b>+{atkPercent:0}%</b></color>    • Giảm hồi chiêu: <color=#F87171><b>-{cdr:0}%</b></color>"
                : $"• Global DMG: <color=#F87171><b>+{atkPercent:0}%</b></color>    • Cooldown: <color=#F87171><b>-{cdr:0}%</b></color>";

            _offenseMasteryLine2.text = isVi
                ? $"• Tỉ lệ Bạo kích: <color=#F87171><b>+{crit:0}%</b></color>    • Sát thương Bạo: <color=#F87171><b>x{critDmg:0.0}</b></color>"
                : $"• Crit Chance: <color=#F87171><b>+{crit:0}%</b></color>    • Crit Damage: <color=#F87171><b>x{critDmg:0.0}</b></color>";
        }
    }

    // =========================================================================
    // SETTINGS PANEL
    // =========================================================================
    private void BuildSettingsPanel(Transform parent)
    {
        _settingsPanel = CreateUIObject("SettingsPanel", parent);
        SetFullStretch(_settingsPanel.GetComponent<RectTransform>(), 0f, 90f);

        var card = CreateUIObject("SettingsCard", _settingsPanel.transform);
        var cRt = card.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(580f, 560f);
        cRt.anchoredPosition = new Vector2(0f, -15f);

        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = _cardBgSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.12f, 0.16f, 0.24f, 0.98f);

        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(30, 30, 24, 24);
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        var title = CreateLabel("SettingsTitle", card.transform, "CÀI ĐẶT TRÒ CHƠI / SETTINGS", 22, FontStyle.Bold, new Color(0.95f, 0.85f, 0.45f, 1f));
        title.alignment = TextAnchor.MiddleCenter;

        CreateDivider(card.transform);

        // 1. Audio Sliders
        CreateSliderRow("MasterVol", card.transform, "🔊 Master Volume", AudioManager.Instance.MasterVolume, v => AudioManager.Instance.SetMasterVolume(v));
        CreateSliderRow("BGMVol", card.transform, "🎵 Nhạc Nền (BGM)", AudioManager.Instance.BGMVolume, v => AudioManager.Instance.SetBGMVolume(v));
        CreateSliderRow("SFXVol", card.transform, "⚔️ Hiệu Ứng (SFX)", AudioManager.Instance.SFXVolume, v => AudioManager.Instance.SetSFXVolume(v));

        // 2. Mute toggle button
        var muteBtn = CreateSimpleButton("MuteBtn", card.transform, AudioManager.Instance.IsMuted ? "🔇 BẬT ÂM THANH (UNMUTE)" : "🔊 TẮT ÂM THANH (MUTE)", 14, new Vector2(0f, 40f), () =>
        {
            AudioManager.Instance.ToggleMute();
            RefreshSettingsMuteBtn();
        });
        _settingsMuteBtnText = muteBtn.GetComponentInChildren<Text>();

        CreateDivider(card.transform);

        // 3. Language toggle
        CreateSimpleButton("LangToggleBtn", card.transform, "🌐 Đổi Ngôn Ngữ / Switch Language", 14, new Vector2(0f, 40f), () =>
        {
            GameLocalization.ToggleLanguage();
            RefreshSettingsMuteBtn();
        });

        // 4. Fullscreen toggle
        CreateSimpleButton("FullscreenBtn", card.transform, "🖥️ Toàn Màn Hình (Fullscreen Toggle)", 14, new Vector2(0f, 40f), () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
        });

        // 5. Back Button
        CreateSimpleButton("BackHomeBtn", card.transform, "◀ QUAY LẠI MENU CHÍNH", 14, new Vector2(0f, 40f), () =>
        {
            SwitchState(MenuState.Home);
        });
    }

    private void RefreshSettingsMuteBtn()
    {
        if (_settingsMuteBtnText != null)
        {
            bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;
            if (AudioManager.Instance.IsMuted)
            {
                _settingsMuteBtnText.text = isVi ? "🔇 BẬT ÂM THANH (UNMUTE)" : "🔇 UNMUTE AUDIO";
                _settingsMuteBtnText.color = new Color(1f, 0.45f, 0.45f);
            }
            else
            {
                _settingsMuteBtnText.text = isVi ? "🔊 TẮT ÂM THANH (MUTE)" : "🔊 MUTE AUDIO";
                _settingsMuteBtnText.color = Color.white;
            }
        }
    }

    // =========================================================================
    // STATE MACHINE & CHUYỂN TAB
    // =========================================================================
    private void SwitchState(MenuState newState)
    {
        _currentState = newState;
        _homePanel.SetActive(_currentState == MenuState.Home);
        _codexPanel.SetActive(_currentState == MenuState.Codex);
        _talentsPanel.SetActive(_currentState == MenuState.Talents);
        _settingsPanel.SetActive(_currentState == MenuState.Settings);

        HideTalentTooltip();

        if (_currentState == MenuState.Codex)
        {
            RebuildCodexList();
        }
        else if (_currentState == MenuState.Talents)
        {
            RefreshTalentTreeUI();
        }
    }

    private void OnPlayClicked()
    {
        // Tải Scene Gameplay
        SceneManager.LoadScene("SampleScene");
    }

    private void RefreshAllUI()
    {
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;
        int stones = MetaProgressionService.Instance.SpiritStones;
        _currencyText.text = isVi ? $"💎 {stones:N0} Linh Thạch" : $"💎 {stones:N0} Spirit Stones";
        _langBtnText.text = isVi ? "🌐 VI" : "🌐 EN";

        if (_currentState == MenuState.Talents)
        {
            RefreshTalentTreeUI();
        }
        else if (_currentState == MenuState.Codex)
        {
            RebuildCodexList();
        }
    }

    private void UpdateLocalization()
    {
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        _playBtnText.text = isVi ? "⚔️  VÀO TRẬN (PLAY)" : "⚔️  START EXPEDITION (PLAY)";
        _codexBtnText.text = isVi ? "📖  BÁCH KHOA TOÀN THƯ (CODEX)" : "📖  ANCIENT CODEX (LORE & STATS)";
        _talentsBtnText.text = isVi ? "⚡  CÂY THIÊN PHÚ (TALENTS)" : "⚡  TALENT TREE (UPGRADES)";
        _settingsBtnText.text = isVi ? "⚙️  CÀI ĐẶT (SETTINGS)" : "⚙️  SETTINGS";
        _quitBtnText.text = isVi ? "🚪  THOÁT (QUIT)" : "🚪  QUIT GAME";

        _tabEnemiesBtn.GetComponentInChildren<Text>().text = isVi ? "QUÁI VẬT (BESTIARY)" : "DEMONIC BEASTS";
        _tabSkillsBtn.GetComponentInChildren<Text>().text = isVi ? "VŨ KHÍ & ĐẠN (WEAPONS)" : "WEAPONS & SKILLS";
        _tabRelicsBtn.GetComponentInChildren<Text>().text = isVi ? "CỔ VẬT (RELICS)" : "ANCIENT RELICS";

        RefreshAllUI();
    }

    // =========================================================================
    // UI FACTORY HELPERS
    // =========================================================================
    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private Text CreateLabel(string name, Transform parent, string text, int fontSize, FontStyle style, Color color)
    {
        var go = CreateUIObject(name, parent);
        var txt = go.AddComponent<Text>();
        txt.font = _defaultFont;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.text = text;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        return txt;
    }

    private Button CreateSimpleButton(string name, Transform parent, string label, int fontSize, Vector2 size, Action onClick)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        if (size != Vector2.zero)
        {
            rt.sizeDelta = size;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            le.minWidth = size.x;
            le.minHeight = size.y;
        }

        var img = go.AddComponent<Image>();
        img.sprite = _btnBgSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.2f, 0.28f, 0.42f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.5f);
        btn.colors = colors;

        if (onClick != null)
        {
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlayUI(UIType.Click);
                onClick();
            });
        }

        var txtObj = CreateUIObject("Text", go.transform);
        SetFullStretch(txtObj.GetComponent<RectTransform>());
        var txt = txtObj.AddComponent<Text>();
        txt.font = _defaultFont;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;

        return btn;
    }

    private Button CreateMenuButton(string name, Transform parent, string label, Color baseColor, float height, Action onClick)
    {
        var btn = CreateSimpleButton(name, parent, label, 18, new Vector2(0f, height), onClick);
        btn.image.color = baseColor;
        return btn;
    }

    private void CreateDivider(Transform parent, Color? color = null)
    {
        var div = CreateUIObject("Divider", parent);
        var dImg = div.AddComponent<Image>();
        dImg.color = color ?? new Color(0.25f, 0.35f, 0.5f, 0.4f);
        var rt = div.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 2f);
        var le = div.AddComponent<LayoutElement>();
        le.preferredHeight = 2f;
        le.minHeight = 2f;
        le.flexibleWidth = 1f;
    }

    private Slider CreateSliderRow(string name, Transform parent, string label, float initialValue, Action<float> onValueChanged)
    {
        var row = CreateUIObject(name + "_Row", parent);
        var rRt = row.GetComponent<RectTransform>();
        rRt.sizeDelta = new Vector2(0f, 36f);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 36f;
        le.minHeight = 36f;
        le.flexibleWidth = 1f;

        var hLayout = row.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 15f;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;

        var lbl = CreateLabel(name + "_Label", row.transform, label, 15, FontStyle.Bold, Color.white);
        var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
        lblLe.preferredWidth = 180f;

        var sliderObj = CreateUIObject(name + "_Slider", row.transform);
        var sRt = sliderObj.GetComponent<RectTransform>();
        sRt.sizeDelta = new Vector2(230f, 20f);
        var sLe = sliderObj.AddComponent<LayoutElement>();
        sLe.preferredWidth = 230f;
        sLe.preferredHeight = 22f;

        var bgObj = CreateUIObject("Background", sliderObj.transform);
        var bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.sizeDelta = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        var fillArea = CreateUIObject("Fill Area", sliderObj.transform);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.25f);
        faRt.anchorMax = new Vector2(1f, 0.75f);
        faRt.sizeDelta = new Vector2(-10f, 0f);

        var fillObj = CreateUIObject("Fill", fillArea.transform);
        var fRt = fillObj.GetComponent<RectTransform>();
        fRt.sizeDelta = Vector2.zero;
        var fImg = fillObj.AddComponent<Image>();
        fImg.color = new Color(0.25f, 0.65f, 1f, 0.95f);

        var handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.sizeDelta = new Vector2(-10f, 0f);
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;

        var handleObj = CreateUIObject("Handle", handleArea.transform);
        var hRt = handleObj.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(16f, 24f);
        var hImg = handleObj.AddComponent<Image>();
        hImg.color = new Color(0.95f, 0.85f, 0.45f, 1f);

        var valText = CreateLabel(name + "_Val", row.transform, $"{(int)(initialValue * 100)}%", 15, FontStyle.Bold, new Color(0.95f, 0.85f, 0.45f, 1f));
        var valLe = valText.gameObject.AddComponent<LayoutElement>();
        valLe.preferredWidth = 55f;

        var slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fRt;
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initialValue;

        slider.onValueChanged.AddListener(v =>
        {
            valText.text = $"{(int)(v * 100)}%";
            onValueChanged?.Invoke(v);
        });

        return slider;
    }

    private void SetFullStretch(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private Sprite CreateRoundedRectSprite(int w, int h, int radius, Color fillColor, Color borderColor)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float rSq = radius * radius;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int cx = (x < radius) ? radius : (x > w - 1 - radius) ? w - 1 - radius : x;
                int cy = (y < radius) ? radius : (y > h - 1 - radius) ? h - 1 - radius : y;
                float dSq = (x - cx) * (x - cx) + (y - cy) * (y - cy);

                if (dSq > rSq)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    bool isBorder = (x < 2 || x >= w - 2 || y < 2 || y >= h - 2 || dSq > (radius - 2) * (radius - 2));
                    tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    // =========================================================================
    // TALENT TOOLTIP & HOVER HANDLERS
    // =========================================================================
    private void BuildTalentTooltip(Transform parent)
    {
        _talentTooltipObj = CreateUIObject("TalentTooltip", parent);
        _talentTooltipRt = _talentTooltipObj.GetComponent<RectTransform>();
        _talentTooltipRt.pivot = new Vector2(0f, 1f);
        _talentTooltipRt.sizeDelta = new Vector2(460f, 0f);

        var img = _talentTooltipObj.AddComponent<Image>();
        img.sprite = _cardBgSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);

        _talentTooltipOutline = _talentTooltipObj.AddComponent<Outline>();
        _talentTooltipOutline.effectColor = new Color(0.3f, 0.8f, 1f, 0.9f);
        _talentTooltipOutline.effectDistance = new Vector2(2.5f, -2.5f);

        var shadow = _talentTooltipObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(4f, -4f);

        var cg = _talentTooltipObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        var vLayout = _talentTooltipObj.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 10f;
        vLayout.padding = new RectOffset(18, 18, 16, 16);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        var csf = _talentTooltipObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Header Row: [IconBadge 52x52] [Title + Branch]
        var headerRow = CreateUIObject("HeaderRow", _talentTooltipObj.transform);
        var hrLe = headerRow.AddComponent<LayoutElement>();
        hrLe.preferredHeight = 54f;
        hrLe.flexibleHeight = 0f;

        var hrLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
        hrLayout.spacing = 12f;
        hrLayout.childControlWidth = true;
        hrLayout.childControlHeight = true;
        hrLayout.childForceExpandWidth = false;
        hrLayout.childForceExpandHeight = true;

        var iconBox = CreateUIObject("IconBox", headerRow.transform);
        var ibLe = iconBox.AddComponent<LayoutElement>();
        ibLe.preferredWidth = 52f;
        ibLe.preferredHeight = 52f;
        ibLe.minWidth = 52f;
        ibLe.minHeight = 52f;
        var ibImg = iconBox.AddComponent<Image>();
        ibImg.sprite = _badgeBgSprite;
        ibImg.type = Image.Type.Sliced;
        ibImg.color = new Color(0.08f, 0.12f, 0.20f, 0.95f);
        var ibOutline = iconBox.AddComponent<Outline>();
        ibOutline.effectColor = new Color(0.3f, 0.8f, 1f, 0.8f);
        _tooltipIconText = CreateLabel("Icon", iconBox.transform, "HP", 24, FontStyle.Bold, Color.white);
        _tooltipIconText.alignment = TextAnchor.MiddleCenter;
        SetFullStretch(_tooltipIconText.GetComponent<RectTransform>());

        var titleBox = CreateUIObject("TitleBox", headerRow.transform);
        var tbLe = titleBox.AddComponent<LayoutElement>();
        tbLe.flexibleWidth = 1f;
        var tbLayout = titleBox.AddComponent<VerticalLayoutGroup>();
        tbLayout.spacing = 3f;
        tbLayout.childControlWidth = true;
        tbLayout.childControlHeight = true;
        tbLayout.childForceExpandWidth = true;
        tbLayout.childForceExpandHeight = false;

        _tooltipNameText = CreateLabel("Name", titleBox.transform, "Vitality Surge", 22, FontStyle.Bold, Color.white);
        _tooltipNameText.alignment = TextAnchor.MiddleLeft;

        _tooltipBranchTierText = CreateLabel("BranchTier", titleBox.transform, "SURVIVAL • TIER 1", 14, FontStyle.Bold, new Color(0.7f, 0.85f, 1f, 0.85f));
        _tooltipBranchTierText.alignment = TextAnchor.MiddleLeft;

        // Divider
        var divObj = CreateUIObject("Divider", _talentTooltipObj.transform);
        _tooltipDividerImg = divObj.AddComponent<Image>();
        _tooltipDividerImg.color = new Color(0.3f, 0.7f, 1f, 0.5f);
        var divLe = divObj.AddComponent<LayoutElement>();
        divLe.preferredHeight = 2f;
        divLe.minHeight = 2f;
        divLe.flexibleWidth = 1f;

        // Description text
        _tooltipDescText = CreateLabel("Description", _talentTooltipObj.transform, "", 16, FontStyle.Normal, new Color(0.92f, 0.96f, 1f, 0.95f));
        _tooltipDescText.alignment = TextAnchor.MiddleLeft;
        var descLe = _tooltipDescText.gameObject.AddComponent<LayoutElement>();
        descLe.flexibleWidth = 1f;

        // Stat Progression Container (Dark panel)
        var statBox = CreateUIObject("StatProgressionBox", _talentTooltipObj.transform);
        var sbImg = statBox.AddComponent<Image>();
        sbImg.sprite = _badgeBgSprite;
        sbImg.type = Image.Type.Sliced;
        sbImg.color = new Color(0.06f, 0.09f, 0.15f, 0.95f);
        var sbOutline = statBox.AddComponent<Outline>();
        sbOutline.effectColor = new Color(0.2f, 0.35f, 0.5f, 0.5f);
        var sbLayout = statBox.AddComponent<VerticalLayoutGroup>();
        sbLayout.spacing = 6f;
        sbLayout.padding = new RectOffset(12, 12, 10, 10);
        sbLayout.childControlWidth = true;
        sbLayout.childControlHeight = true;
        sbLayout.childForceExpandWidth = true;
        sbLayout.childForceExpandHeight = false;

        _tooltipCurrentStatText = CreateLabel("CurrentStat", statBox.transform, "", 16, FontStyle.Bold, new Color(0.35f, 0.9f, 0.55f, 1f));
        _tooltipNextStatText = CreateLabel("NextStat", statBox.transform, "", 16, FontStyle.Bold, new Color(1f, 0.85f, 0.35f, 1f));
        _tooltipMaxStatText = CreateLabel("MaxStat", statBox.transform, "", 15, FontStyle.Normal, new Color(0.7f, 0.8f, 0.95f, 0.85f));

        // Footer Hint / Cost
        _tooltipFooterText = CreateLabel("Footer", _talentTooltipObj.transform, "", 15, FontStyle.Bold, new Color(0.85f, 0.92f, 1f, 0.9f));
        _tooltipFooterText.alignment = TextAnchor.MiddleLeft;

        _talentTooltipObj.SetActive(false);
    }

    public void OnTalentCardHoverEnter(string nodeId)
    {
        _currentHoveredNodeId = nodeId;
        ShowTalentTooltip(nodeId);
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        UpdateTooltipPosition(mousePos);
    }

    public void OnTalentCardHoverExit(string nodeId)
    {
        if (_currentHoveredNodeId == nodeId)
        {
            _currentHoveredNodeId = null;
            HideTalentTooltip();
        }
    }

    private void ShowTalentTooltip(string nodeId)
    {
        var node = MetaProgressionService.Instance.GetNode(nodeId);
        if (node == null || _talentTooltipObj == null) return;

        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        Color branchAccent = node.Branch switch
        {
            MetaProgressionService.TalentBranch.Health => new Color(0.2f, 0.9f, 0.5f, 1f),
            MetaProgressionService.TalentBranch.Offense => new Color(1f, 0.38f, 0.38f, 1f),
            _ => new Color(1f, 0.82f, 0.25f, 1f)
        };

        _talentTooltipOutline.effectColor = branchAccent * 0.9f;
        _tooltipDividerImg.color = branchAccent * 0.6f;

        _tooltipIconText.text = GetNodeSymbol(node.Id);
        _tooltipIconText.color = node.Tier >= 3 ? new Color(1f, 0.9f, 0.35f, 1f) : branchAccent;

        _tooltipNameText.text = isVi ? node.NameVi : node.NameEn;

        string branchName = node.Branch switch
        {
            MetaProgressionService.TalentBranch.Health => isVi ? "THỂ LỰC (SURVIVAL)" : "SURVIVAL • VITALITY",
            MetaProgressionService.TalentBranch.Offense => isVi ? "CÔNG KÍCH (OFFENSE)" : "OFFENSE • COMBAT",
            _ => isVi ? "VẬN MỆNH (FORTUNE)" : "FORTUNE • PROSPERITY"
        };
        string tierStr = node.Tier >= 3 ? "APEX" : $"TIER {node.Tier}";
        _tooltipBranchTierText.text = $"{branchName} • {tierStr} • Lv {node.CurrentLevel}/{node.MaxLevel}";
        _tooltipBranchTierText.color = branchAccent * 0.9f;

        _tooltipDescText.text = isVi ? node.DescriptionVi : node.DescriptionEn;

        GetNodeStatStrings(node, isVi, out string curBonus, out string nextBonus, out string maxBonus, out _);

        _tooltipCurrentStatText.text = curBonus;
        _tooltipNextStatText.text = node.IsMaxed
            ? (isVi ? "➤ Đã đạt cấp tối đa (Cảnh giới viên mãn)" : "➤ Already at maximum level")
            : nextBonus;
        _tooltipNextStatText.color = node.IsMaxed ? new Color(0.65f, 0.72f, 0.85f, 0.8f) : new Color(1f, 0.85f, 0.35f, 1f);
        _tooltipMaxStatText.text = maxBonus;

        bool isLocked = MetaProgressionService.Instance.IsNodeLocked(node.Id);
        if (node.IsMaxed)
        {
            _tooltipFooterText.text = isVi ? "✔ ĐÃ ĐẠT CẢNH GIỚI TỐI ĐA" : "✔ CURRENTLY AT MAXIMUM LEVEL";
            _tooltipFooterText.color = new Color(1f, 0.88f, 0.3f, 1f);
        }
        else if (isLocked)
        {
            string prereq = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
            _tooltipFooterText.text = isVi ? $"🔒 Yêu cầu tiên quyết: Mở khóa [{prereq}]" : $"🔒 Requirement: Unlock [{prereq}] first";
            _tooltipFooterText.color = new Color(1f, 0.45f, 0.45f, 1f);
        }
        else
        {
            bool canAfford = MetaProgressionService.Instance.CanUpgrade(node.Id);
            string affordStr = canAfford
                ? (isVi ? "<color=#4ADE80>Đủ điều kiện nâng</color>" : "<color=#4ADE80>Ready to upgrade</color>")
                : (isVi ? "<color=#F87171>Chưa đủ Linh Thạch</color>" : "<color=#F87171>Not enough Spirit Stones</color>");
            _tooltipFooterText.text = isVi
                ? $"💎 Chi phí: <b>{node.NextCost:N0}</b> Linh Thạch  ({affordStr})"
                : $"💎 Cost: <b>{node.NextCost:N0}</b> Spirit Stones  ({affordStr})";
            _tooltipFooterText.color = Color.white;
        }

        _talentTooltipObj.SetActive(true);
        _talentTooltipObj.transform.SetAsLastSibling();
    }

    private void HideTalentTooltip()
    {
        if (_talentTooltipObj != null)
        {
            _talentTooltipObj.SetActive(false);
        }
    }

    private void UpdateTooltipPosition(Vector2 mousePos)
    {
        if (_talentTooltipObj == null || _talentTooltipRt == null || _canvas == null) return;

        float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        float actualWidth = (_talentTooltipRt.rect.width > 0f ? _talentTooltipRt.rect.width : 460f) * scale;
        float actualHeight = (_talentTooltipRt.rect.height > 0f ? _talentTooltipRt.rect.height : 280f) * scale;

        float x = mousePos.x + 24f * scale;
        float y = mousePos.y - 14f * scale;

        if (x + actualWidth > Screen.width - 15f)
        {
            x = mousePos.x - actualWidth - 24f * scale;
        }
        if (x < 15f)
        {
            x = 15f;
        }

        if (y - actualHeight < 15f)
        {
            y = mousePos.y + actualHeight + 15f * scale;
        }
        if (y > Screen.height - 15f)
        {
            y = Screen.height - 15f;
        }

        _talentTooltipRt.position = new Vector3(x, y, 0f);
    }

    private void GetNodeStatStrings(MetaProgressionService.TalentNodeInfo node, bool isVi,
        out string currentBonus, out string nextBonus, out string maxBonus, out string shortTag)
    {
        bool isLocked = MetaProgressionService.Instance.IsNodeLocked(node.Id);
        int cur = node.CurrentLevel;
        int next = cur + 1;

        switch (node.Id)
        {
            case "hp_max":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 15} HP Máu tối đa" : $"✦ Current Lv {cur}: +{cur * 15} Max HP";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 15} HP Máu tối đa (+15 HP)" : $"➤ Next Lv {next}: +{next * 15} Max HP (+15 HP)";
                maxBonus = isVi ? "★ Cảnh giới: +75 HP Máu tối đa" : "★ Max Potential: +75 Max HP";
                if (isLocked) shortTag = isVi ? "🔒 Khóa" : "🔒 Locked";
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+15 HP/Cấp)" : "Inactive (+15 HP/Lv)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +75 HP" : "★ MAXED: +75 HP";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 15} HP" : $"✦ ACTIVE: +{cur * 15} HP";
                break;

            case "hp_regen":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{(cur * 0.5f):0.0} HP/giây Tự hồi máu" : $"✦ Current Lv {cur}: +{(cur * 0.5f):0.0} HP/s Health Regen";
                nextBonus = isVi ? $"➤ Cấp {next}: +{(next * 0.5f):0.0} HP/giây (+0.5 HP/s)" : $"➤ Next Lv {next}: +{(next * 0.5f):0.0} HP/s (+0.5 HP/s)";
                maxBonus = isVi ? "★ Cảnh giới: +1.5 HP/giây Hồi máu" : "★ Max Potential: +1.5 HP/s Health Regen";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+0.5 HP/s)" : "Inactive (+0.5 HP/s)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +1.5 HP/s" : "★ MAXED: +1.5 HP/s";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{(cur * 0.5f):0.0} HP/s" : $"✦ ACTIVE: +{(cur * 0.5f):0.0} HP/s";
                break;

            case "hp_armor":
                currentBonus = isVi ? $"✦ Cấp {cur}: -{(cur * 3.5f):0.0}% Sát thương gánh chịu" : $"✦ Current Lv {cur}: -{(cur * 3.5f):0.0}% Damage Taken";
                nextBonus = isVi ? $"➤ Cấp {next}: -{(next * 3.5f):0.0}% Sát thương (-3.5%)" : $"➤ Next Lv {next}: -{(next * 3.5f):0.0}% Damage Taken (-3.5%)";
                maxBonus = isVi ? "★ Cảnh giới: -10.5% Sát thương gánh chịu" : "★ Max Potential: -10.5% Damage Taken";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (-3.5% Sát thương)" : "Inactive (-3.5% Damage)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: -10.5% Sát thương" : "★ MAXED: -10.5% Damage";
                else shortTag = isVi ? $"✦ HIỆN TẠI: -{(cur * 3.5f):0.0}% Sát thương" : $"✦ ACTIVE: -{(cur * 3.5f):0.0}% Damage";
                break;

            case "hp_revival":
                currentBonus = cur > 0 ? (isVi ? "✦ Cấp 1: Hồi sinh tức thì với 50% HP (1 lần/trận)" : "✦ Current Lv 1: Revive instantly with 50% HP (1x/run)")
                                       : (isVi ? "✦ Hiện tại: Chưa kích hoạt" : "✦ Current: Inactive");
                nextBonus = isVi ? "➤ Kích hoạt: Hồi sinh 50% HP khi cạn máu (1 lần/trận)" : "➤ Unlock: Revive with 50% HP upon fatal blow (1x/run)";
                maxBonus = isVi ? "★ Cảnh giới: Niết Bàn Trùng Sinh (50% HP)" : "★ Max Potential: Phoenix Rebirth (50% HP)";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (Hồi sinh 50% HP)" : "Inactive (50% HP Revive)";
                else shortTag = isVi ? "★ ĐÃ KÍCH HOẠT HỒI SINH (50% HP)" : "★ REVIVAL ACTIVE (50% HP)";
                break;

            case "atk_global":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 6}% Tổng sát thương mọi vũ khí" : $"✦ Current Lv {cur}: +{cur * 6}% Global Weapon ATK";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 6}% Sát thương (+6%)" : $"➤ Next Lv {next}: +{next * 6}% Global ATK (+6%)";
                maxBonus = isVi ? "★ Cảnh giới: +30% Tổng sát thương vũ khí" : "★ Max Potential: +30% Global Weapon ATK";
                if (isLocked) shortTag = isVi ? "🔒 Khóa" : "🔒 Locked";
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+6% Sát thương/Cấp)" : "Inactive (+6% ATK/Lv)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +30% Sát thương" : "★ MAXED: +30% Global ATK";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 6}% Sát thương" : $"✦ ACTIVE: +{cur * 6}% Global ATK";
                break;

            case "atk_cdr":
                currentBonus = isVi ? $"✦ Cấp {cur}: -{cur * 5}% Thời gian hồi chiêu vũ khí" : $"✦ Current Lv {cur}: -{cur * 5}% Weapon Cooldown";
                nextBonus = isVi ? $"➤ Cấp {next}: -{next * 5}% Hồi chiêu (-5%)" : $"➤ Next Lv {next}: -{next * 5}% Cooldown (-5%)";
                maxBonus = isVi ? "★ Cảnh giới: -15% Thời gian hồi chiêu" : "★ Max Potential: -15% Weapon Cooldown";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (-5% Hồi chiêu)" : "Inactive (-5% Cooldown)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: -15% Hồi chiêu" : "★ MAXED: -15% Cooldown";
                else shortTag = isVi ? $"✦ HIỆN TẠI: -{cur * 5}% Hồi chiêu" : $"✦ ACTIVE: -{cur * 5}% Cooldown";
                break;

            case "atk_crit":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 4}% Tỉ lệ bạo kích (x2 sát thương)" : $"✦ Current Lv {cur}: +{cur * 4}% Crit Chance (x2 DMG)";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 4}% Bạo kích (+4%)" : $"➤ Next Lv {next}: +{next * 4}% Crit Chance (+4%)";
                maxBonus = isVi ? "★ Cảnh giới: +12% Tỉ lệ bạo kích (x2 sát thương)" : "★ Max Potential: +12% Crit Chance (x2 DMG)";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+4% Bạo kích)" : "Inactive (+4% Crit)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +12% Bạo kích" : "★ MAXED: +12% Crit Chance";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 4}% Bạo kích" : $"✦ ACTIVE: +{cur * 4}% Crit Chance";
                break;

            case "atk_apex":
                currentBonus = cur > 0 ? (isVi ? "✦ Cấp 1: +15% Công & Bạo kích tăng lên x2.5" : "✦ Current Lv 1: +15% ATK & Crit DMG increased to x2.5")
                                       : (isVi ? "✦ Hiện tại: Chưa kích hoạt" : "✦ Current: Inactive");
                nextBonus = isVi ? "➤ Kích hoạt: +15% Công & Sát thương Bạo kích x2.5" : "➤ Unlock: +15% ATK & Crit DMG set to x2.5";
                maxBonus = isVi ? "★ Cảnh giới: Bá Thể Độc Tôn (+15% ATK & x2.5 Crit)" : "★ Max Potential: Colossal Dominance (+15% ATK & x2.5 Crit)";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+15% Công & x2.5 Bạo)" : "Inactive (+15% ATK & x2.5 Crit)";
                else shortTag = isVi ? "★ ĐẠO QUẢ: +15% CÔNG & x2.5 BẠO" : "★ APEX: +15% ATK & x2.5 CRIT";
                break;

            case "util_speed":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 4}% Tốc độ di chuyển nhân vật" : $"✦ Current Lv {cur}: +{cur * 4}% Movement Speed";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 4}% Tốc độ (+4%)" : $"➤ Next Lv {next}: +{next * 4}% Speed (+4%)";
                maxBonus = isVi ? "★ Cảnh giới: +20% Tốc độ di chuyển" : "★ Max Potential: +20% Movement Speed";
                if (isLocked) shortTag = isVi ? "🔒 Khóa" : "🔒 Locked";
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+4% Tốc độ/Cấp)" : "Inactive (+4% Speed/Lv)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +20% Tốc độ chạy" : "★ MAXED: +20% Move Speed";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 4}% Tốc độ chạy" : $"✦ ACTIVE: +{cur * 4}% Move Speed";
                break;

            case "util_pickup":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 20}% Bán kính nam châm hút ngọc & đồ" : $"✦ Current Lv {cur}: +{cur * 20}% EXP & Item Magnet Range";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 20}% Bán kính (+20%)" : $"➤ Next Lv {next}: +{next * 20}% Magnet Range (+20%)";
                maxBonus = isVi ? "★ Cảnh giới: +60% Bán kính nam châm" : "★ Max Potential: +60% Magnet Range";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+20% Nam châm)" : "Inactive (+20% Magnet)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +60% Bán kính nhặt" : "★ MAXED: +60% Magnet Range";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 20}% Bán kính nhặt" : $"✦ ACTIVE: +{cur * 20}% Magnet Range";
                break;

            case "util_drop":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 12}% Tỉ lệ rớt Rương & Ngọc lớn" : $"✦ Current Lv {cur}: +{cur * 12}% Drop Rate for Chests & Items";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 12}% Tỉ lệ (+12%)" : $"➤ Next Lv {next}: +{next * 12}% Drop Rate (+12%)";
                maxBonus = isVi ? "★ Cảnh giới: +36% Tỉ lệ rớt vật phẩm" : "★ Max Potential: +36% Drop Rate";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+12% Tỉ lệ rớt)" : "Inactive (+12% Drop)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +36% Tỉ lệ rớt" : "★ MAXED: +36% Drop Rate";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 12}% Tỉ lệ rớt" : $"✦ ACTIVE: +{cur * 12}% Drop Rate";
                break;

            case "util_gold":
                currentBonus = isVi ? $"✦ Cấp {cur}: +{cur * 15}% Linh Thạch & Vàng thu được" : $"✦ Current Lv {cur}: +{cur * 15}% Spirit Stones & Gold Gain";
                nextBonus = isVi ? $"➤ Cấp {next}: +{next * 15}% Thu nhập (+15%)" : $"➤ Next Lv {next}: +{next * 15}% Gold & Stones (+15%)";
                maxBonus = isVi ? "★ Cảnh giới: +45% Linh Thạch & Vàng" : "★ Max Potential: +45% Spirit Stones & Gold";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+15% Thu nhập)" : "Inactive (+15% Gold)";
                else if (node.IsMaxed) shortTag = isVi ? "★ TỐI ĐA: +45% Thu nhập" : "★ MAXED: +45% Gold Gain";
                else shortTag = isVi ? $"✦ HIỆN TẠI: +{cur * 15}% Thu nhập" : $"✦ ACTIVE: +{cur * 15}% Gold Gain";
                break;

            case "util_apex":
                currentBonus = cur > 0 ? (isVi ? "✦ Cấp 1: +2 Lượt Reroll ván & +25% Vàng" : "✦ Current Lv 1: +2 Free Rerolls/run & +25% Gold")
                                       : (isVi ? "✦ Hiện tại: Chưa kích hoạt" : "✦ Current: Inactive");
                nextBonus = isVi ? "➤ Kích hoạt: +2 Lượt Reroll bài & +25% Vàng thưởng" : "➤ Unlock: +2 Free Perk Rerolls in run & +25% Extra Gold";
                maxBonus = isVi ? "★ Cảnh giới: Tài Phú Vô Song (+2 Reroll & +25% Gold)" : "★ Max Potential: Master of Fortune (+2 Rerolls & +25% Gold)";
                if (isLocked)
                {
                    string p = MetaProgressionService.Instance.GetPrerequisiteNodeName(node.Id, isVi);
                    shortTag = isVi ? $"🔒 Cần: {p}" : $"🔒 Requires: {p}";
                }
                else if (cur == 0) shortTag = isVi ? "Chưa mở (+2 Reroll & +25% Vàng)" : "Inactive (+2 Rerolls & +25% Gold)";
                else shortTag = isVi ? "★ ĐẠO QUẢ: +2 REROLL & +25% VÀNG" : "★ APEX: +2 REROLLS & +25% GOLD";
                break;

            default:
                currentBonus = isVi ? $"✦ Cấp {cur}" : $"✦ Current Lv {cur}";
                nextBonus = isVi ? $"➤ Cấp {next}" : $"➤ Next Lv {next}";
                maxBonus = isVi ? $"★ Tối đa Lv {node.MaxLevel}" : $"★ Max Lv {node.MaxLevel}";
                shortTag = $"Lv {cur}/{node.MaxLevel}";
                break;
        }
    }

    private class TalentCardHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public MainMenuUI MenuUI;
        public string NodeId;
        public Outline CardOutline;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CardOutline != null)
            {
                CardOutline.effectDistance = new Vector2(2.5f, -2.5f);
            }
            MenuUI?.OnTalentCardHoverEnter(NodeId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject != null &&
                eventData.pointerCurrentRaycast.gameObject.transform.IsChildOf(transform))
            {
                return;
            }

            if (CardOutline != null)
            {
                CardOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            MenuUI?.OnTalentCardHoverExit(NodeId);
        }
    }
}
