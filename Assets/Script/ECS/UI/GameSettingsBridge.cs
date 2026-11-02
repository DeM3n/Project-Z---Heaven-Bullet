using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Hybrid Bridge đóng vai trò Điều phối viên (Coordinator / Facade) cho hệ thống Cài đặt và Dev Mode:
/// - Kết nối TopBarSettingsButtonsView, PauseMenuView, DevControlPanelView.
/// - Kết nối DevSkillService, DevCheatService và SettingsInputHandler.
/// - Tuyệt đối không chứa mã lệnh ECS trực tiếp (0 ECS leakage) hay tự ý ghi Time.timeScale.
/// </summary>
public class GameSettingsBridge : MonoBehaviour
{
    private static GameSettingsBridge _instance;

    private Canvas _canvas;
    private TopBarSettingsButtonsView _topBarView;
    private PauseMenuView _pauseView;
    private SettingsInputHandler _inputHandler;
    private DevSkillService _skillService;

#if UNITY_EDITOR || DEBUG
    private DevControlPanelView _devView;
    private DevCheatService _cheatService;
#endif

    private bool _isDevPanelOpen = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<GameSettingsBridge>() == null)
        {
            var go = new GameObject("[GameSettingsBridge]");
            go.AddComponent<GameSettingsBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        _instance = this;
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
        UpdateLanguageUI();
        bool isGameplay = SceneManager.GetActiveScene().name != "MainMenuScene";
        _topBarView?.SetVisible(isGameplay);
    }

    private void Update()
    {
        _inputHandler?.Update(_isDevPanelOpen);
    }

    #region --- Pause Menu Controller ---

    public void TogglePause()
    {
        if (PauseService.HasToken(PauseReason.PauseMenu))
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (PauseService.HasAnyHUDModal()) return;

#if UNITY_EDITOR || DEBUG
        if (_isDevPanelOpen) CloseDevPanel();
#endif

        PauseService.Acquire(PauseReason.PauseMenu);
        _pauseView?.Show();
    }

    public void ResumeGame()
    {
        PauseService.Release(PauseReason.PauseMenu);
        _pauseView?.Hide();
    }

    public void RestartGame()
    {
        PauseService.ClearAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        PauseService.ClearAll();
        _pauseView?.Hide();
#if UNITY_EDITOR || DEBUG
        _devView?.Hide();
        _isDevPanelOpen = false;
#endif
        SceneManager.LoadScene("MainMenuScene");
    }

    #endregion

    #region --- Developer Panel Controller ---

#if UNITY_EDITOR || DEBUG

    public void ToggleDevPanel()
    {
        if (_isDevPanelOpen)
        {
            CloseDevPanel();
        }
        else
        {
            OpenDevPanel();
        }
    }

    public void OpenDevPanel()
    {
        _isDevPanelOpen = true;
        _pauseView?.Hide();

        PauseService.Acquire(PauseReason.DevPanel);
        _devView?.Show();
        RefreshDevPanel();
    }

    public void CloseDevPanel()
    {
        _isDevPanelOpen = false;
        _devView?.Hide();
        PauseService.Release(PauseReason.DevPanel);

        // Nếu lúc mở Dev Panel mà Pause Menu đang mở thì quay lại Pause Menu
        if (PauseService.HasToken(PauseReason.PauseMenu))
        {
            _pauseView?.Show();
        }
    }

    private void RefreshDevPanel()
    {
        if (_devView != null && _skillService != null && _cheatService != null)
        {
            _devView.Refresh(_skillService.GetAllSkillStates(), _cheatService.GetDevActions());
        }
    }

#endif

    #endregion

    #region --- Khởi tạo Canvas, Views & Services ---

    private void BuildCanvasAndViews()
    {
        var old = GameObject.Find("GameSettings_Canvas");
        if (old != null) Destroy(old);

        var canvasObj = new GameObject("GameSettings_Canvas");
        DontDestroyOnLoad(canvasObj);

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1001;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Top Bar Buttons
        _topBarView = new TopBarSettingsButtonsView(canvasObj.transform);
        _topBarView.OnPauseClicked += TogglePause;

        // 2. Pause Menu
        _pauseView = new PauseMenuView(canvasObj.transform);
        _pauseView.OnResumeClicked += ResumeGame;
        _pauseView.OnRestartClicked += RestartGame;
        _pauseView.OnLanguageClicked += GameLocalization.ToggleLanguage;
        _pauseView.OnMainMenuClicked += ReturnToMainMenu;

        // 3. Dev Panel
        _skillService = new DevSkillService();

#if UNITY_EDITOR || DEBUG
        _cheatService = new DevCheatService();

        // Tạo danh sách cheat có đính kèm callback làm mới UI
        var cheatActions = _cheatService.GetDevActions();
        foreach (var action in cheatActions)
        {
            var origAction = action.ExecuteAction;
            if (action.ActionId == "LevelUp")
            {
                // Khi Level-Up: Đóng Dev Panel trước rồi mới tăng cấp để nhường chỗ cho Popup Level-Up
                action.ExecuteAction = () =>
                {
                    CloseDevPanel();
                    origAction();
                };
            }
            else
            {
                action.ExecuteAction = () =>
                {
                    origAction();
                    RefreshDevPanel();
                };
            }
        }

        _devView = new DevControlPanelView(canvasObj.transform, _skillService.GetDevSkillInfos(), cheatActions);
        _devView.OnSkillLevelDelta += (skillId, delta) =>
        {
            _skillService.SetSkillLevel(skillId, delta);
            RefreshDevPanel();
        };
        _devView.OnSkillDirectLevel += (skillId, lvl) =>
        {
            _skillService.SetSkillLevel(skillId, 0, lvl);
            RefreshDevPanel();
        };
        _devView.OnToggleFusion += () =>
        {
            _skillService.ToggleFusion();
            RefreshDevPanel();
        };
        _devView.OnMaxAllSkills += () =>
        {
            _skillService.MaxAllSkills();
            RefreshDevPanel();
        };
        _devView.OnCloseClicked += CloseDevPanel;

        _topBarView.OnDevClicked += ToggleDevPanel;
#endif
    }

    private void InitServices()
    {
        _inputHandler = new SettingsInputHandler();
        _inputHandler.OnTogglePause += TogglePause;
#if UNITY_EDITOR || DEBUG
        _inputHandler.OnToggleDevPanel += ToggleDevPanel;
        _inputHandler.OnCloseDevPanel += CloseDevPanel;
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isGameplay = scene.name != "MainMenuScene";
        _topBarView?.SetVisible(isGameplay);

        _pauseView?.Hide();
#if UNITY_EDITOR || DEBUG
        _devView?.Hide();
        _isDevPanelOpen = false;
        _cheatService?.Reset();
#endif
        PauseService.ClearAll();
    }

    private void UpdateLanguageUI()
    {
        _pauseView?.UpdateLanguage();
#if UNITY_EDITOR || DEBUG
        _devView?.UpdateLanguage();
        RefreshDevPanel();
#endif
    }

    #endregion
}
