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
    private Button _quitBtn;
    private Text _quitBtnText;

    // --- Codex State & UI ---
    private CodexDatabase.EntryCategory _codexCategory = CodexDatabase.EntryCategory.Enemies;
    private string _selectedEntryId;
    private Transform _codexListContainer;
    private Text _codexEntryTitle;
    private Text _codexEntrySubtitle;
    private Text _codexEntryDesc;
    private Text _codexEntryStats;
    private Text _codexEntryMechanics;
    private Text _codexEntryTips;
    private Button _tabEnemiesBtn;
    private Button _tabSkillsBtn;
    private Button _tabRelicsBtn;

    // --- Talent Tree UI ---
    private Transform _healthNodesContainer;
    private Transform _offenseNodesContainer;
    private Transform _fortuneNodesContainer;
    private Text _talentSummaryText;
    private Button _respecBtn;
    private Text _respecBtnText;
    private readonly Dictionary<string, NodeUIItem> _nodeUIItems = new Dictionary<string, NodeUIItem>();

    private class NodeUIItem
    {
        public string NodeId;
        public Text NameText;
        public Text LevelText;
        public Text DescText;
        public Text CostText;
        public Button UpgradeBtn;
        public Text UpgradeBtnText;
    }

    // --- Fonts & Textures ---
    private Font _defaultFont;
    private Sprite _cardBgSprite;
    private Sprite _btnBgSprite;

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

            if (FindFirstObjectByType<MainMenuUI>() == null)
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
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void LoadResources()
    {
        _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

        _cardBgSprite = CreateRoundedRectSprite(128, 128, 16, new Color(0.12f, 0.15f, 0.22f, 0.95f), new Color(0.24f, 0.32f, 0.48f, 1f));
        _btnBgSprite = CreateRoundedRectSprite(64, 64, 12, new Color(0.18f, 0.24f, 0.36f, 1f), new Color(0.35f, 0.5f, 0.75f, 1f));
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
    }

    private void BuildHeader(Transform parent)
    {
        var headerObj = CreateUIObject("HeaderBar", parent);
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, 0f);
        headerRt.sizeDelta = new Vector2(0f, 90f);

        var headerImg = headerObj.AddComponent<Image>();
        headerImg.color = new Color(0.09f, 0.12f, 0.18f, 0.95f);

        // Game Title
        var titleObj = CreateUIObject("GameTitle", headerObj.transform);
        _titleText = titleObj.AddComponent<Text>();
        _titleText.font = _defaultFont;
        _titleText.fontSize = 32;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.color = new Color(0.95f, 0.85f, 0.45f, 1f);
        _titleText.alignment = TextAnchor.MiddleLeft;
        _titleText.text = "PROJECT Z: HEAVEN BULLET";
        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(0.4f, 1f);
        titleRt.anchoredPosition = new Vector2(40f, 0f);
        titleRt.sizeDelta = new Vector2(500f, 0f);

        // Tiền Tệ Linh Thạch (Spirit Stones)
        var curObj = CreateUIObject("CurrencyDisplay", headerObj.transform);
        _currencyText = curObj.AddComponent<Text>();
        _currencyText.font = _defaultFont;
        _currencyText.fontSize = 24;
        _currencyText.fontStyle = FontStyle.Bold;
        _currencyText.color = new Color(0.35f, 0.95f, 1f, 1f);
        _currencyText.alignment = TextAnchor.MiddleRight;
        _currencyText.text = "💎 500 Linh Thạch";
        var curRt = curObj.GetComponent<RectTransform>();
        curRt.anchorMin = new Vector2(0.5f, 0f);
        curRt.anchorMax = new Vector2(0.85f, 1f);
        curRt.offsetMin = curRt.offsetMax = Vector2.zero;

        // Nút Cheat/Test Thêm Linh Thạch
        _testAddCurrencyBtn = CreateSimpleButton("TestAddBtn", headerObj.transform, "+1000 💎", 15, new Vector2(100, 36), () =>
        {
            MetaProgressionService.Instance.AddSpiritStones(1000);
        });
        var testRt = _testAddCurrencyBtn.GetComponent<RectTransform>();
        testRt.anchorMin = new Vector2(0.86f, 0.5f);
        testRt.anchorMax = new Vector2(0.86f, 0.5f);
        testRt.anchoredPosition = new Vector2(55f, 0f);

        // Nút Đổi Ngôn Ngữ
        _langBtn = CreateSimpleButton("LangBtn", headerObj.transform, "🌐 [L] VI", 16, new Vector2(90, 42), () =>
        {
            GameLocalization.ToggleLanguage();
        });
        var langRt = _langBtn.GetComponent<RectTransform>();
        langRt.anchorMin = new Vector2(1f, 0.5f);
        langRt.anchorMax = new Vector2(1f, 0.5f);
        langRt.anchoredPosition = new Vector2(-60f, 0f);
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
    // CODEX PANEL (SÁCH / BÁCH KHOA TOÀN THƯ TRA CỨU)
    // =========================================================================
    private void BuildCodexPanel(Transform parent)
    {
        _codexPanel = CreateUIObject("CodexPanel", parent);
        SetFullStretch(_codexPanel.GetComponent<RectTransform>(), 0f, 90f);

        // Top Navigation Sub-Tabs
        var topTabsObj = CreateUIObject("TopTabs", _codexPanel.transform);
        var tabsRt = topTabsObj.GetComponent<RectTransform>();
        tabsRt.anchorMin = new Vector2(0f, 1f);
        tabsRt.anchorMax = new Vector2(1f, 1f);
        tabsRt.pivot = new Vector2(0.5f, 1f);
        tabsRt.anchoredPosition = new Vector2(0f, -20f);
        tabsRt.sizeDelta = new Vector2(1200f, 54f);

        var tabsLayout = topTabsObj.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 20f;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;

        _tabEnemiesBtn = CreateSimpleButton("TabEnemies", topTabsObj.transform, "👾 QUÁI VẬT (ENEMIES)", 18, Vector2.zero, () => SelectCodexCategory(CodexDatabase.EntryCategory.Enemies));
        _tabSkillsBtn = CreateSimpleButton("TabSkills", topTabsObj.transform, "⚔️ VŨ KHÍ & ĐẠN (WEAPONS)", 18, Vector2.zero, () => SelectCodexCategory(CodexDatabase.EntryCategory.Skills));
        _tabRelicsBtn = CreateSimpleButton("TabRelics", topTabsObj.transform, "💎 CỔ VẬT (RELICS)", 18, Vector2.zero, () => SelectCodexCategory(CodexDatabase.EntryCategory.Relics));

        // Nút Trở Về (Back)
        var backBtn = CreateSimpleButton("BackBtn", _codexPanel.transform, "◀ QUAY LẠI [ESC]", 16, new Vector2(140f, 44f), () => SwitchState(MenuState.Home));
        var backRt = backBtn.GetComponent<RectTransform>();
        backRt.anchorMin = new Vector2(0f, 1f);
        backRt.anchorMax = new Vector2(0f, 1f);
        backRt.anchoredPosition = new Vector2(90f, -47f);

        // Content Area: Split 2 cột (Trái: Danh sách chọn, Phải: Thẻ thông tin chi tiết)
        var contentObj = CreateUIObject("CodexContent", _codexPanel.transform);
        var contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.05f, 0.05f);
        contentRt.anchorMax = new Vector2(0.95f, 0.88f);
        contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;

        // Cột Trái (List)
        var leftCol = CreateUIObject("LeftList", contentObj.transform);
        var leftRt = leftCol.GetComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0f, 0f);
        leftRt.anchorMax = new Vector2(0.32f, 1f);
        leftRt.offsetMin = leftRt.offsetMax = Vector2.zero;

        var scroll = leftCol.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = CreateUIObject("Viewport", leftCol.transform);
        SetFullStretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.8f);
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
        vLayout.padding = new RectOffset(10, 10, 10, 10);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        var fitter = container.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cRt;
        _codexListContainer = container.transform;

        // Cột Phải (Detail Inspection Card)
        var rightCol = CreateUIObject("RightDetail", contentObj.transform);
        var rightRt = rightCol.GetComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(0.34f, 0f);
        rightRt.anchorMax = new Vector2(1f, 1f);
        rightRt.offsetMin = rightRt.offsetMax = Vector2.zero;

        var cardImg = rightCol.AddComponent<Image>();
        cardImg.sprite = _cardBgSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

        // Layout bên trong Card chi tiết
        var detailPad = CreateUIObject("DetailPadding", rightCol.transform);
        SetFullStretch(detailPad.GetComponent<RectTransform>(), 30f, 30f);
        var dLayout = detailPad.AddComponent<VerticalLayoutGroup>();
        dLayout.spacing = 14f;
        dLayout.childControlWidth = true;
        dLayout.childControlHeight = false;

        _codexEntryTitle = CreateLabel("EntryTitle", detailPad.transform, "Tên Mục", 28, FontStyle.Bold, new Color(1f, 0.85f, 0.4f, 1f));
        _codexEntrySubtitle = CreateLabel("EntrySubtitle", detailPad.transform, "Phân loại", 18, FontStyle.Italic, new Color(0.55f, 0.85f, 1f, 1f));
        _codexEntryDesc = CreateLabel("EntryDesc", detailPad.transform, "Mô tả truyền thuyết...", 16, FontStyle.Normal, Color.white);

        // Divider
        CreateDivider(detailPad.transform);

        _codexEntryStats = CreateLabel("EntryStats", detailPad.transform, "Bảng thông số chiến đấu...", 16, FontStyle.Normal, new Color(0.9f, 0.95f, 0.8f, 1f));
        _codexEntryMechanics = CreateLabel("EntryMechanics", detailPad.transform, "Cơ chế đặc biệt...", 15, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f, 0.95f));
        _codexEntryTips = CreateLabel("EntryTips", detailPad.transform, "Lời khuyên chiến thuật...", 15, FontStyle.Italic, new Color(1f, 0.65f, 0.45f, 1f));
    }

    private void SelectCodexCategory(CodexDatabase.EntryCategory category)
    {
        _codexCategory = category;
        RebuildCodexList();
    }

    private void RebuildCodexList()
    {
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
            var btn = CreateSimpleButton($"Item_{entry.Id}", _codexListContainer, isVi ? entry.TitleVi : entry.TitleEn, 16, new Vector2(0f, 48f), () =>
            {
                DisplayCodexDetail(entry);
            });
            var txt = btn.GetComponentInChildren<Text>();
            txt.alignment = TextAnchor.MiddleLeft;
            btn.image.color = new Color(0.15f, 0.2f, 0.3f, 0.9f);
        }

        // Chọn item đầu tiên mặc định
        DisplayCodexDetail(entries[0]);
    }

    private void DisplayCodexDetail(CodexDatabase.CodexEntry entry)
    {
        if (entry == null) return;
        _selectedEntryId = entry.Id;
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        _codexEntryTitle.text = isVi ? entry.TitleVi : entry.TitleEn;
        _codexEntrySubtitle.text = isVi ? entry.SubtitleVi : entry.SubtitleEn;
        _codexEntryDesc.text = isVi ? entry.DescriptionVi : entry.DescriptionEn;

        // Ghép chuỗi stats
        var statsDict = isVi ? entry.StatsVi : entry.StatsEn;
        string statsStr = isVi ? "<b>[ THÔNG SỐ CỐT LÕI ]</b>\n" : "<b>[ CORE SPECIFICATIONS ]</b>\n";
        foreach (var kvp in statsDict)
        {
            statsStr += $"• {kvp.Key}: <b>{kvp.Value}</b>\n";
        }
        _codexEntryStats.text = statsStr.TrimEnd();

        _codexEntryMechanics.text = isVi
            ? $"<b>Cơ Chế:</b> {entry.MechanicsVi}"
            : $"<b>Mechanics:</b> {entry.MechanicsEn}";

        _codexEntryTips.text = isVi
            ? $"💡 <b>Mẹo Chiến Thuật:</b> {entry.TipsVi}"
            : $"💡 <b>Tactical Tip:</b> {entry.TipsEn}";
    }

    // =========================================================================
    // TALENT TREE PANEL (CÂY THIÊN PHÚ NÂNG CẤP VĨNH VIỄN)
    // =========================================================================
    private void BuildTalentTreePanel(Transform parent)
    {
        _talentsPanel = CreateUIObject("TalentsPanel", parent);
        SetFullStretch(_talentsPanel.GetComponent<RectTransform>(), 0f, 90f);

        // Header Sub-bar: Nút Quay Lại + Tổng Linh Thạch + Nút Tẩy Điểm
        var topBarObj = CreateUIObject("TalentTopBar", _talentsPanel.transform);
        var barRt = topBarObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -20f);
        barRt.sizeDelta = new Vector2(1200f, 50f);

        var backBtn = CreateSimpleButton("BackBtn", topBarObj.transform, "◀ QUAY LẠI [ESC]", 16, new Vector2(140f, 44f), () => SwitchState(MenuState.Home));
        var bRt = backBtn.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0.5f);
        bRt.anchorMax = new Vector2(0f, 0.5f);
        bRt.anchoredPosition = new Vector2(70f, 0f);

        _talentSummaryText = CreateLabel("TalentSummary", topBarObj.transform, "Đã Dùng: 0 Linh Thạch", 18, FontStyle.Bold, new Color(0.4f, 0.9f, 1f, 1f));
        var sRt = _talentSummaryText.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.5f);
        sRt.anchorMax = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = new Vector2(0f, 0f);
        sRt.sizeDelta = new Vector2(400f, 40f);
        _talentSummaryText.alignment = TextAnchor.MiddleCenter;

        _respecBtn = CreateSimpleButton("RespecBtn", topBarObj.transform, "🔄 TẨY ĐIỂM (HOÀN 100%)", 15, new Vector2(230f, 44f), () =>
        {
            MetaProgressionService.Instance.ResetAllTalents();
        });
        _respecBtn.image.color = new Color(0.85f, 0.3f, 0.25f, 1f);
        _respecBtnText = _respecBtn.GetComponentInChildren<Text>();
        var rRt = _respecBtn.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(1f, 0.5f);
        rRt.anchorMax = new Vector2(1f, 0.5f);
        rRt.anchoredPosition = new Vector2(-120f, 0f);

        // 3 Cột tương ứng 3 Nhánh
        var columnsObj = CreateUIObject("ColumnsContainer", _talentsPanel.transform);
        var colsRt = columnsObj.GetComponent<RectTransform>();
        colsRt.anchorMin = new Vector2(0.04f, 0.04f);
        colsRt.anchorMax = new Vector2(0.96f, 0.88f);
        colsRt.offsetMin = colsRt.offsetMax = Vector2.zero;

        var hLayout = columnsObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 24f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;

        _healthNodesContainer = CreateBranchColumn(columnsObj.transform, "🛡️ THỂ LỰC (SURVIVAL)", new Color(0.2f, 0.8f, 0.4f, 1f));
        _offenseNodesContainer = CreateBranchColumn(columnsObj.transform, "⚔️ CÔNG KÍCH (OFFENSE)", new Color(1f, 0.35f, 0.3f, 1f));
        _fortuneNodesContainer = CreateBranchColumn(columnsObj.transform, "✨ VẬN MỆNH (FORTUNE)", new Color(0.95f, 0.8f, 0.25f, 1f));

        BuildTalentNodesUI();
    }

    private Transform CreateBranchColumn(Transform parent, string title, Color accentColor)
    {
        var colObj = CreateUIObject($"Col_{title}", parent);
        var colImg = colObj.AddComponent<Image>();
        colImg.sprite = _cardBgSprite;
        colImg.type = Image.Type.Sliced;
        colImg.color = new Color(0.1f, 0.13f, 0.2f, 0.95f);

        var vLayout = colObj.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 14f;
        vLayout.padding = new RectOffset(16, 16, 16, 16);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;

        var headerLabel = CreateLabel("BranchHeader", colObj.transform, title, 20, FontStyle.Bold, accentColor);
        headerLabel.alignment = TextAnchor.MiddleCenter;
        var hRt = headerLabel.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(0f, 32f);

        CreateDivider(colObj.transform);

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
    }

    private NodeUIItem CreateNodeCard(Transform parent, MetaProgressionService.TalentNodeInfo node)
    {
        var cardObj = CreateUIObject($"Node_{node.Id}", parent);
        var cardImg = cardObj.AddComponent<Image>();
        cardImg.sprite = _btnBgSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.15f, 0.19f, 0.28f, 0.9f);

        var cardLayout = cardObj.AddComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 6f;
        cardLayout.padding = new RectOffset(12, 12, 10, 10);
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = false;

        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        // Header dòng 1: Tên + Level
        var topRow = CreateUIObject("TopRow", cardObj.transform);
        var topLayout = topRow.AddComponent<HorizontalLayoutGroup>();
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;
        var nameTxt = CreateLabel("NodeName", topRow.transform, isVi ? node.NameVi : node.NameEn, 15, FontStyle.Bold, Color.white);
        var lvlTxt = CreateLabel("NodeLevel", topRow.transform, $"Lv {node.CurrentLevel}/{node.MaxLevel}", 14, FontStyle.Bold, new Color(0.4f, 0.9f, 1f, 1f));
        lvlTxt.alignment = TextAnchor.MiddleRight;

        // Mô tả chỉ số
        var descTxt = CreateLabel("NodeDesc", cardObj.transform, isVi ? node.DescriptionVi : node.DescriptionEn, 13, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f, 0.9f));

        // Dòng cuối: Giá + Nút Nâng Cấp
        var bottomRow = CreateUIObject("BottomRow", cardObj.transform);
        var bLayout = bottomRow.AddComponent<HorizontalLayoutGroup>();
        bLayout.childControlWidth = true;
        bLayout.childControlHeight = true;

        var costTxt = CreateLabel("NodeCost", bottomRow.transform, node.IsMaxed ? "TỐI ĐA" : $"💎 {node.NextCost}", 14, FontStyle.Bold, new Color(1f, 0.85f, 0.3f, 1f));
        costTxt.alignment = TextAnchor.MiddleLeft;

        string upgradeLabel = isVi ? "NÂNG CẤP" : "UPGRADE";
        var upgradeBtn = CreateSimpleButton("UpgradeBtn", bottomRow.transform, upgradeLabel, 13, new Vector2(100f, 32f), () =>
        {
            MetaProgressionService.Instance.UpgradeNode(node.Id);
        });

        return new NodeUIItem
        {
            NodeId = node.Id,
            NameText = nameTxt,
            LevelText = lvlTxt,
            DescText = descTxt,
            CostText = costTxt,
            UpgradeBtn = upgradeBtn,
            UpgradeBtnText = upgradeBtn.GetComponentInChildren<Text>()
        };
    }

    private void RefreshTalentTreeUI()
    {
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;
        int totalSpent = MetaProgressionService.Instance.GetTotalSpentStones();

        _talentSummaryText.text = isVi
            ? $"Đã Đầu Tư: <b>{totalSpent:N0}</b> 💎 Linh Thạch"
            : $"Invested: <b>{totalSpent:N0}</b> 💎 Spirit Stones";

        _respecBtnText.text = isVi ? "🔄 TẨY ĐIỂM (HOÀN 100%)" : "🔄 RESPEC (100% REFUND)";

        foreach (var kvp in _nodeUIItems)
        {
            var node = MetaProgressionService.Instance.GetNode(kvp.Key);
            if (node == null) continue;

            var item = kvp.Value;
            item.NameText.text = isVi ? node.NameVi : node.NameEn;
            item.DescText.text = isVi ? node.DescriptionVi : node.DescriptionEn;
            item.LevelText.text = $"Lv {node.CurrentLevel}/{node.MaxLevel}";

            if (node.IsMaxed)
            {
                item.CostText.text = isVi ? "MAX CẤP" : "MAXED";
                item.CostText.color = new Color(0.4f, 0.95f, 0.5f, 1f);
                item.UpgradeBtn.interactable = false;
                item.UpgradeBtnText.text = "✓";
                item.UpgradeBtn.image.color = new Color(0.2f, 0.35f, 0.25f, 0.6f);
            }
            else
            {
                item.CostText.text = $"💎 {node.NextCost:N0}";
                bool canBuy = MetaProgressionService.Instance.CanUpgrade(node.Id);
                item.UpgradeBtn.interactable = canBuy;
                item.CostText.color = canBuy ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(0.7f, 0.4f, 0.4f, 0.8f);
                item.UpgradeBtnText.text = isVi ? "NÂNG" : "UPGRADE";
                item.UpgradeBtn.image.color = canBuy
                    ? new Color(0.2f, 0.65f, 0.35f, 1f)
                    : new Color(0.25f, 0.28f, 0.35f, 0.6f);
            }
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
        cRt.sizeDelta = new Vector2(560f, 480f);
        cRt.anchoredPosition = new Vector2(0f, -20f);

        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = _cardBgSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.12f, 0.16f, 0.24f, 0.98f);

        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        var title = CreateLabel("SettingsTitle", card.transform, "CÀI ĐẶT TRÒ CHƠI", 24, FontStyle.Bold, new Color(0.95f, 0.85f, 0.45f, 1f));
        title.alignment = TextAnchor.MiddleCenter;

        CreateDivider(card.transform);

        // Language toggle
        CreateSimpleButton("LangToggleBtn", card.transform, "🌐 Đổi Ngôn Ngữ / Switch Language", 16, new Vector2(0f, 48f), () =>
        {
            GameLocalization.ToggleLanguage();
        });

        // Fullscreen toggle
        CreateSimpleButton("FullscreenBtn", card.transform, "🖥️ Toàn Màn Hình (Fullscreen Toggle)", 16, new Vector2(0f, 48f), () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
        });

        // Back Button
        CreateSimpleButton("BackHomeBtn", card.transform, "◀ QUAY LẠI MENU CHÍNH", 16, new Vector2(0f, 48f), () =>
        {
            SwitchState(MenuState.Home);
        });
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

        _tabEnemiesBtn.GetComponentInChildren<Text>().text = isVi ? "👾 QUÁI VẬT (ENEMIES)" : "👾 DEMONIC BEASTS";
        _tabSkillsBtn.GetComponentInChildren<Text>().text = isVi ? "⚔️ VŨ KHÍ & ĐẠN (WEAPONS)" : "⚔️ BLADES & SPELLS";
        _tabRelicsBtn.GetComponentInChildren<Text>().text = isVi ? "💎 CỔ VẬT (RELICS)" : "💎 ANCIENT RELICS";

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
        return txt;
    }

    private Button CreateSimpleButton(string name, Transform parent, string label, int fontSize, Vector2 size, Action onClick)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        if (size != Vector2.zero) rt.sizeDelta = size;

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
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(() => onClick());

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

    private void CreateDivider(Transform parent)
    {
        var div = CreateUIObject("Divider", parent);
        var dImg = div.AddComponent<Image>();
        dImg.color = new Color(0.25f, 0.35f, 0.5f, 0.4f);
        var rt = div.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 2f);
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
}
