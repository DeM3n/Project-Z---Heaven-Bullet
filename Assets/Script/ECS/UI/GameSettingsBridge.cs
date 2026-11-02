using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Hybrid Bridge đóng vai trò Điều phối viên (Coordinator / Facade) cho hệ thống Cài đặt và Dev Mode:
/// - Kết nối TopBarSettingsButtonsView, PauseMenuView, ResumeCountdownView, DevControlPanelView.
/// - Kết nối DevSkillService, DevCheatService và SettingsInputHandler.
/// - Quản lý đếm ngược 3 giây trước khi Resume trận đấu.
/// - Tuyệt đối không chứa mã lệnh ECS trực tiếp (0 ECS leakage) hay tự ý ghi Time.timeScale.
/// </summary>
public class GameSettingsBridge : MonoBehaviour
{
    private static GameSettingsBridge _instance;

    private Canvas _canvas;
    private TopBarSettingsButtonsView _topBarView;
    private PauseMenuView _pauseView;
    private ResumeCountdownView _countdownView;
    private SettingsInputHandler _inputHandler;
    private DevSkillService _skillService;

#if UNITY_EDITOR || DEBUG
    private DevControlPanelView _devView;
    private DevCheatService _cheatService;
#endif

    private bool _isDevPanelOpen = false;
    private bool _isCountingDown = false;
    private Coroutine _countdownCoroutine;

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

    #region --- Pause Menu Controller & 3-Second Resume Countdown ---

    public void TogglePause()
    {
        if (_isCountingDown)
        {
            CancelCountdownAndReopenPause();
            return;
        }

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

        if (_isCountingDown)
        {
            CancelCountdownAndReopenPause();
            return;
        }

#if UNITY_EDITOR || DEBUG
        if (_isDevPanelOpen) CloseDevPanel();
#endif

        PauseService.Acquire(PauseReason.PauseMenu);
        _pauseView?.Show();
    }

    public void ResumeGame()
    {
        _pauseView?.Hide();

        if (gameObject.activeInHierarchy)
        {
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(ResumeCountdownRoutine());
        }
        else
        {
            PauseService.Release(PauseReason.PauseMenu);
        }
    }

    private void CancelCountdownAndReopenPause()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _isCountingDown = false;
        _countdownView?.Hide();
        _pauseView?.Show();
    }

    private IEnumerator ResumeCountdownRoutine()
    {
        _isCountingDown = true;
        _countdownView?.Show();

        // Đếm 3
        _countdownView?.SetCount("3", new Color(1f, 0.85f, 0.25f));
        yield return AnimateCountdownSecond();

        // Đếm 2
        _countdownView?.SetCount("2", new Color(1f, 0.65f, 0.2f));
        yield return AnimateCountdownSecond();

        // Đếm 1
        _countdownView?.SetCount("1", new Color(1f, 0.45f, 0.2f));
        yield return AnimateCountdownSecond();

        // GO!
        _countdownView?.SetCount("GO!", new Color(0.2f, 1f, 0.4f));
        _countdownView?.SetScale(1.15f);

        float flashTime = 0f;
        while (flashTime < 0.35f)
        {
            flashTime += Time.unscaledDeltaTime;
            yield return null;
        }

        _countdownView?.Hide();
        _isCountingDown = false;
        _countdownCoroutine = null;

        // Chỉ khi kết thúc hoàn toàn 3 giây đếm ngược thì mới nhả token PauseMenu để unpause game!
        PauseService.Release(PauseReason.PauseMenu);
    }

    private IEnumerator AnimateCountdownSecond()
    {
        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Hiệu ứng scale punch: từ 1.35f nảy về 1.0f
            float scale = Mathf.Lerp(1.35f, 1.0f, Mathf.SmoothStep(0f, 1f, t * 2.5f));
            _countdownView?.SetScale(scale);
            yield return null;
        }
    }

    public void RestartGame()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _isCountingDown = false;
        _countdownView?.Hide();
        PauseService.ClearAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _isCountingDown = false;
        _countdownView?.Hide();
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
        if (_isCountingDown)
        {
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
            _isCountingDown = false;
            _countdownView?.Hide();
        }

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
        scaler.dynamicPixelsPerUnit = 3f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Top Bar Buttons
        _topBarView = new TopBarSettingsButtonsView(canvasObj.transform);
        _topBarView.OnPauseClicked += TogglePause;

        // 2. Pause Menu View
        _pauseView = new PauseMenuView(canvasObj.transform);
        _pauseView.OnResumeClicked += ResumeGame;
        _pauseView.OnRestartClicked += RestartGame;
        _pauseView.OnLanguageClicked += GameLocalization.ToggleLanguage;
        _pauseView.OnMainMenuClicked += ReturnToMainMenu;
#if UNITY_EDITOR || DEBUG
        _pauseView.OnDevModeClicked += OpenDevPanel;
#endif

        // 3. Resume Countdown View
        _countdownView = new ResumeCountdownView(canvasObj.transform);

        // 4. Dev Panel
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

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _isCountingDown = false;
        _countdownView?.Hide();

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
