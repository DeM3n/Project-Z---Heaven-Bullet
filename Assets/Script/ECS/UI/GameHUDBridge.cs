using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Hybrid Bridge đóng vai trò Điều phối viên (Coordinator / Facade) cho toàn bộ hệ thống UI:
/// - Kết nối các View hiển thị (HUD, Level-Up, Rương Báu, Game Over, Banner).
/// - Kết nối các Domain Service (PerkSelector, ChestRewardService, RunLifecycleService).
/// - Quản lý máy trạng thái Popup Modal (HUDModalStateMachine) và các phím tắt bàn phím.
/// Tuân thủ nghiêm ngặt nguyên lý SOLID, Pure ECS + Hybrid Bridge.
/// </summary>
public class GameHUDBridge : MonoBehaviour
{
    private static GameHUDBridge _instance;

    // --- ECS ---
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private bool _ecsInitialized;

    // --- State & Progression ---
    private int _lastPlayerLevel = 1;
    private int _pendingLevelUps = 0;
    private int _rerollsRemaining = 3;

    // --- UI Canvas & Views ---
    private Canvas _canvas;
    private GameHUDView _hudView;
    private LevelUpModalView _levelUpView;
    private AncientChestModalView _chestView;
    private GameOverModalView _gameOverView;
    private BannerNotificationView _bannerView;

    // --- State Machine & Domain Services ---
    private HUDModalStateMachine _modalStateMachine;
    private PerkSelector _perkSelector;
    private List<IPerkDefinition> _perkDefinitions;
    private ChestRewardService _chestRewardService;
    private RunLifecycleService _runLifecycleService;

    // --- Mega Wave Notification Tracking ---
    private bool _lastMegaWaveActive = false;
    private int _lastAnnouncedMegaWave = 0;

    // --- Active Modal Cache ---
    private readonly List<IPerkDefinition> _currentPerks = new List<IPerkDefinition>();
    private readonly HashSet<string> _lastExcludedPerkIds = new HashSet<string>();
    private IRelicDefinition _currentRelic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<GameHUDBridge>() == null)
        {
            var go = new GameObject("[GameHUDBridge]");
            go.AddComponent<GameHUDBridge>();
            DontDestroyOnLoad(go);
        }
    }

    /// <summary>
    /// Phương thức static tương thích ngược để các hệ thống khác (TreasureGoblinVisualBridge) gọi thông báo.
    /// </summary>
    public static void ShowBannerNotification(string msg)
    {
        _instance?._bannerView?.Trigger(msg);
    }

    private void Awake()
    {
        _instance = this;
        EnsureEventSystem();
        BuildCanvasAndViews();
        InitServices();
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += UpdateLanguageUI;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= UpdateLanguageUI;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        InitECS();
        UpdateLanguageUI();
        bool isGameplay = SceneManager.GetActiveScene().name != "MainMenuScene" && SceneManager.GetActiveScene().name != "LoadingScene";
        if (_canvas != null) _canvas.gameObject.SetActive(isGameplay);
    }

    private void Update()
    {
        // 1. Cập nhật banner notification
        _bannerView?.UpdateTimer(Time.unscaledDeltaTime);

        // 2. Xử lý Input phím tắt
        HandleInputShortcuts();

        // 3. Cập nhật đồng hồ sinh tồn
        _runLifecycleService?.UpdateTimer(Time.deltaTime);

        // 4. Đồng bộ ECS & Giao diện HUD
        if (!_ecsInitialized && !InitECS()) return;
        if (_playerQuery.IsEmpty) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();

        // Cập nhật HUD Realtime từ Snapshot
        var snapshot = PlayerHudSnapshotReader.Read(_entityManager, playerEntity, _runLifecycleService.SurvivalTime);
        _hudView.UpdateSnapshot(snapshot);

        // 5. Kiểm tra thông báo đợt tổng tấn công Mega Wave
        if (snapshot.IsMegaWaveActive && !_lastMegaWaveActive)
        {
            _lastMegaWaveActive = true;
            _lastAnnouncedMegaWave = snapshot.MegaWaveMinute;
            ShowBannerNotification(GameLocalization.MegaWaveAlert(snapshot.MegaWaveMinute));
        }
        else if (!snapshot.IsMegaWaveActive && _lastMegaWaveActive)
        {
            _lastMegaWaveActive = false;
            ShowBannerNotification(GameLocalization.MegaWaveCleared(_lastAnnouncedMegaWave));
        }

        // 6. Kiểm tra sự kiện lên cấp (Level-Up)
        if (snapshot.Level > _lastPlayerLevel)
        {
            _pendingLevelUps += (snapshot.Level - _lastPlayerLevel);
            _lastPlayerLevel = snapshot.Level;

            if (_modalStateMachine.CurrentState == HUDModalState.None)
            {
                ShowNextLevelUpReward(playerEntity);
            }
        }

        // 6. Kiểm tra sự kiện mở Rương (ChestOpenEvent)
        if (_modalStateMachine.CurrentState == HUDModalState.None && _chestRewardService.CheckAndConsumeChestEvent())
        {
            ShowChestReward(playerEntity);
        }

        // 7. Kiểm tra người chơi hy sinh (PlayerDeadTag)
        if (_modalStateMachine.CurrentState != HUDModalState.GameOver && _runLifecycleService.CheckPlayerDead())
        {
            TriggerGameOver();
        }
    }

    private void HandleInputShortcuts()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Phím [L]: Đổi ngôn ngữ nhanh
        if (kb.lKey.wasPressedThisFrame)
        {
            GameLocalization.ToggleLanguage();
        }

        // Phím Level-Up: [R] để Reroll, [1-3] để chọn Perk
        if (_modalStateMachine.CurrentState == HUDModalState.LevelUp)
        {
            if (kb.rKey.wasPressedThisFrame)
            {
                OnRerollClicked();
            }
            else if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                _levelUpView.SelectCardByIndex(0);
            }
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                _levelUpView.SelectCardByIndex(1);
            }
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                _levelUpView.SelectCardByIndex(2);
            }
        }

        // Phím Rương: [Space] hoặc [Enter] để thu nhận
        if (_modalStateMachine.CurrentState == HUDModalState.ChestReward)
        {
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            {
                OnCollectChestReward();
            }
        }
    }

    #region --- Level-Up Flow ---

    private void ShowNextLevelUpReward(Entity playerEntity)
    {
        if (_pendingLevelUps <= 0)
        {
            _levelUpView.Hide();
            _modalStateMachine.CloseModal();
            return;
        }

        _modalStateMachine.TransitionTo(HUDModalState.LevelUp);

        _lastExcludedPerkIds.Clear();
        foreach (var p in _currentPerks) _lastExcludedPerkIds.Add(p.PerkId);

        _currentPerks.Clear();
        _currentPerks.AddRange(_perkSelector.SelectPerks(_entityManager, playerEntity, 3, _lastExcludedPerkIds));

        var options = new List<PerkOptionData>();
        foreach (var p in _currentPerks)
        {
            options.Add(p.GetOptionData(_entityManager, playerEntity));
        }

        _levelUpView.Show(options, _rerollsRemaining);
    }

    private void OnPerkSelected(PerkOptionData perkData)
    {
        if (_playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        var perk = _currentPerks.Find(p => p.PerkId == perkData.PerkId);
        perk?.Apply(_entityManager, playerEntity);

        _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
        ShowNextLevelUpReward(playerEntity);
    }

    private void OnRerollClicked()
    {
        if (_rerollsRemaining <= 0 || _playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        _rerollsRemaining--;
        _levelUpView.UpdateRerolls(_rerollsRemaining);

        _lastExcludedPerkIds.Clear();
        foreach (var p in _currentPerks) _lastExcludedPerkIds.Add(p.PerkId);

        _currentPerks.Clear();
        _currentPerks.AddRange(_perkSelector.SelectPerks(_entityManager, playerEntity, 3, _lastExcludedPerkIds));

        var options = new List<PerkOptionData>();
        foreach (var p in _currentPerks)
        {
            options.Add(p.GetOptionData(_entityManager, playerEntity));
        }

        _levelUpView.Show(options, _rerollsRemaining);
    }

    #endregion

    #region --- Ancient Chest Flow ---

    private void ShowChestReward(Entity playerEntity)
    {
        _modalStateMachine.TransitionTo(HUDModalState.ChestReward);
        _currentRelic = _chestRewardService.SelectRandomRelic();
        _chestView.Show(_currentRelic.GetRewardData(_entityManager, playerEntity), this);
    }

    private void OnCollectChestReward()
    {
        if (_playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        if (_currentRelic != null)
        {
            var (stones, extraRerolls) = _chestRewardService.CollectReward(_currentRelic, playerEntity);
            _runLifecycleService.AddStones(stones);
            _rerollsRemaining += extraRerolls;
            _levelUpView.UpdateRerolls(_rerollsRemaining);
        }

        _chestView.Hide();

        if (_pendingLevelUps > 0)
        {
            ShowNextLevelUpReward(playerEntity);
        }
        else
        {
            _modalStateMachine.CloseModal();
        }
    }

    #endregion

    #region --- Game Over Flow ---

    private void TriggerGameOver()
    {
        _modalStateMachine.TransitionTo(HUDModalState.GameOver);
        var stats = _runLifecycleService.CalculateGameOverStats(_lastPlayerLevel);
        _gameOverView.Show(stats);
    }

    #endregion

    #region --- Khởi tạo & Cấu hình ---

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadWrite<HealthData>(),
            ComponentType.ReadWrite<PlayerLevelData>()
        );

        _perkDefinitions = PerkRegistry.CreateDefaultDefinitions(_entityManager);
        _perkSelector = new PerkSelector(_perkDefinitions);
        _chestRewardService = new ChestRewardService(_entityManager);
        _runLifecycleService = new RunLifecycleService(_entityManager);

        _ecsInitialized = true;
        return true;
    }

    private void BuildCanvasAndViews()
    {
        var oldCanvas = GameObject.Find("GameHUD_Canvas");
        if (oldCanvas != null) Destroy(oldCanvas);

        var canvasObj = new GameObject("GameHUD_Canvas");
        DontDestroyOnLoad(canvasObj);

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Khởi tạo các Views
        _hudView = new GameHUDView(canvasObj.transform);
        _levelUpView = new LevelUpModalView(canvasObj.transform);
        _chestView = new AncientChestModalView(canvasObj.transform);
        _gameOverView = new GameOverModalView(canvasObj.transform);
        _bannerView = new BannerNotificationView(canvasObj.transform);

        // Đăng ký Callbacks từ Views
        _levelUpView.OnPerkSelected += OnPerkSelected;
        _levelUpView.OnRerollClicked += OnRerollClicked;
        _chestView.OnCollectClicked += OnCollectChestReward;
        _gameOverView.OnRestartClicked += () =>
        {
            _gameOverView.Hide();
            _modalStateMachine.CloseModal();
            _runLifecycleService.RestartCurrentScene();
        };
        _gameOverView.OnMainMenuClicked += () =>
        {
            _gameOverView.Hide();
            _modalStateMachine.CloseModal();
            _runLifecycleService.ReturnToMainMenu();
        };
    }

    private void InitServices()
    {
        _modalStateMachine = new HUDModalStateMachine();
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(eventSystemObj);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isGameplay = scene.name != "MainMenuScene" && scene.name != "LoadingScene";
        if (_canvas != null) _canvas.gameObject.SetActive(isGameplay);

        if (isGameplay)
        {
            _lastPlayerLevel = 1;
            _pendingLevelUps = 0;
            _rerollsRemaining = 3 + (MetaProgressionService.Instance != null ? MetaProgressionService.Instance.GetBonusRerolls() : 0);
            _currentPerks.Clear();
            _lastExcludedPerkIds.Clear();

            _levelUpView?.Hide();
            _chestView?.Hide();
            _gameOverView?.Hide();

            _modalStateMachine?.CloseModal();
            _runLifecycleService?.ResetRunState();
            _runLifecycleService?.CleanupOldRunEntities();
            InitECS();
        }
        else
        {
            _levelUpView?.Hide();
            _chestView?.Hide();
            _gameOverView?.Hide();
            _modalStateMachine?.CloseModal();
        }
    }

    private void UpdateLanguageUI()
    {
        _hudView?.UpdateLanguage();
        _gameOverView?.UpdateLanguage();

        if (_modalStateMachine != null && _modalStateMachine.CurrentState == HUDModalState.LevelUp && !_playerQuery.IsEmpty)
        {
            Entity p = _playerQuery.GetSingletonEntity();
            var options = new List<PerkOptionData>();
            foreach (var perk in _currentPerks)
            {
                options.Add(perk.GetOptionData(_entityManager, p));
            }
            _levelUpView?.UpdateLanguage(options);
        }

        if (_modalStateMachine != null && _modalStateMachine.CurrentState == HUDModalState.ChestReward && !_playerQuery.IsEmpty)
        {
            Entity p = _playerQuery.GetSingletonEntity();
            _chestView?.UpdateLanguage(type => _chestRewardService.GetRelicByType(type).GetRewardData(_entityManager, p));
        }
    }

    #endregion
}
