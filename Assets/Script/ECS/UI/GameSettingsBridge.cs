using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Hybrid Bridge quản lý:
/// 1. Menu Cài đặt & Tạm dừng (Pause / Settings Menu): Hỗ trợ nút UI bánh răng [⚙] và phím [ESC] / [P].
/// 2. Chế độ Lập trình viên (Developer Control Panel): Hỗ trợ nút [🛠️ DEV] và phím [F1] / [~] để
///    tự do điều chỉnh cấp độ kỹ năng (Flying Sword, Slash Sword, Arcane Volley, Fusion) và các công cụ cheat/test.
/// </summary>
public class GameSettingsBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private EntityQuery _flyingSwordQuery;
    private EntityQuery _slashSwordQuery;
    private EntityQuery _projectileQuery;
    private EntityQuery _fusionQuery;
    private EntityQuery _enemyQuery;
    private bool _ecsInitialized;

    // --- State ---
    private bool _isPaused = false;
    private bool _isDevPanelOpen = false;
    private bool _isGodMode = false;
    private bool _isSpeedBoosted = false;
    private float _originalMoveSpeed = 5.0f;

    // --- UI Canvases & Panels ---
    private Canvas _canvas;
    private GameObject _pausePanel;
    private GameObject _devPanel;

    // Pause UI Elements
    private Text _pauseTitleText;
    private Text _resumeBtnText;
    private Text _restartBtnText;
    private Text _devModeBtnText;

    // Dev UI Elements
    private Text _devHeaderTitle;
    private Text _flyingSwordLvlText;
    private Text _slashSwordLvlText;
    private Text _projectileLvlText;
    private Text _fusionStateText;
    private Text _godModeBtnText;
    private Text _speedBtnText;

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
        BuildUI();
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += UpdateLanguageUI;
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= UpdateLanguageUI;
    }

    private void Start()
    {
        InitECS();
        UpdateLanguageUI();
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;

        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadWrite<HealthData>(),
            ComponentType.ReadWrite<MovementData>(),
            ComponentType.ReadWrite<PlayerLevelData>());

        _flyingSwordQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FlyingSwordSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _slashSwordQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SlashSwordSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _projectileQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ProjectileSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _fusionQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SwordFusionTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _enemyQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EnemyTag>(),
            ComponentType.ReadWrite<HealthData>());

        _ecsInitialized = true;
        return true;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            // Phím [Escape] hoặc [P]: Bật/Tắt Menu Pause
            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            {
                if (_isDevPanelOpen)
                {
                    CloseDevPanel();
                }
                else
                {
                    TogglePause();
                }
            }

            // Phím [F1] hoặc phím [`] (Backquote): Bật/Tắt Chế độ Developer
            if (kb.f1Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)
            {
                ToggleDevPanel();
            }
        }

        // Nếu bật God Mode: Luôn giữ máu nhân vật ở mức tối đa
        if (_isGodMode && _ecsInitialized && !_playerQuery.IsEmpty)
        {
            Entity player = _playerQuery.GetSingletonEntity();
            var hp = _entityManager.GetComponentData<HealthData>(player);
            if (hp.CurrentHP < hp.MaxHP)
            {
                hp.CurrentHP = hp.MaxHP;
                _entityManager.SetComponentData(player, hp);
            }
        }
    }

    // =========================================================================
    // ĐIỀU KHIỂN PAUSE & DEVELOPER PANEL
    // =========================================================================
    public void TogglePause()
    {
        if (_isPaused)
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
        _isPaused = true;
        Time.timeScale = 0f;
        if (_pausePanel != null) _pausePanel.SetActive(true);
        if (_devPanel != null) _devPanel.SetActive(false);
        _isDevPanelOpen = false;
    }

    public void ResumeGame()
    {
        _isPaused = false;
        _isDevPanelOpen = false;
        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_devPanel != null) _devPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

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
        _isPaused = true;
        Time.timeScale = 0f;

        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_devPanel != null)
        {
            _devPanel.SetActive(true);
            RefreshDevSkillTexts();
        }
    }

    public void CloseDevPanel()
    {
        _isDevPanelOpen = false;
        if (_devPanel != null) _devPanel.SetActive(false);

        // Nếu lúc mở Dev panel mà Pause menu đang bật thì quay lại Pause menu, ngược lại tiếp tục game
        if (_isPaused && _pausePanel != null && _pausePanel.activeSelf)
        {
            // Vẫn giữ pause
        }
        else
        {
            ResumeGame();
        }
    }

    // =========================================================================
    // CÁC HÀM XỬ LÝ CHỈNH CẤP ĐỘ KỸ NĂNG & DEV CHEATS TRONG ECS
    // =========================================================================
    public void SetFlyingSwordLevel(int delta, int directLevel = -1)
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_flyingSwordQuery.IsEmpty) return;

        Entity skill = _flyingSwordQuery.GetSingletonEntity();
        var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skill);
        int targetLvl = (directLevel >= 0) ? directLevel : math.clamp(runtime.Level + delta, 0, 4);
        runtime.Level = targetLvl;
        _entityManager.SetComponentData(skill, runtime);

        RefreshDevSkillTexts();
    }

    public void SetSlashSwordLevel(int delta, int directLevel = -1)
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_slashSwordQuery.IsEmpty) return;

        Entity skill = _slashSwordQuery.GetSingletonEntity();
        var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skill);
        int targetLvl = (directLevel >= 0) ? directLevel : math.clamp(runtime.Level + delta, 0, 4);
        runtime.Level = targetLvl;
        _entityManager.SetComponentData(skill, runtime);

        RefreshDevSkillTexts();
    }

    public void SetProjectileLevel(int delta, int directLevel = -1)
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_projectileQuery.IsEmpty) return;

        Entity skill = _projectileQuery.GetSingletonEntity();
        var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skill);
        int targetLvl = (directLevel >= 0) ? directLevel : math.clamp(runtime.Level + delta, 0, 5);
        runtime.Level = targetLvl;
        _entityManager.SetComponentData(skill, runtime);

        RefreshDevSkillTexts();
    }

    public void ToggleFusion()
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_fusionQuery.IsEmpty) return;

        Entity skill = _fusionQuery.GetSingletonEntity();
        var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skill);
        runtime.Level = (runtime.Level > 0) ? 0 : 1;
        runtime.Timer = 0.5f; // Đòn đầu tiên xuất chiến sau 0.5s nếu bật
        _entityManager.SetComponentData(skill, runtime);

        RefreshDevSkillTexts();
    }

    public void MaxAllSkills()
    {
        SetFlyingSwordLevel(0, 4);
        SetSlashSwordLevel(0, 4);
        SetProjectileLevel(0, 5);

        if (!_ecsInitialized && !InitECS()) return;
        if (!_fusionQuery.IsEmpty)
        {
            Entity skill = _fusionQuery.GetSingletonEntity();
            var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skill);
            runtime.Level = 1;
            runtime.Timer = 0.5f;
            _entityManager.SetComponentData(skill, runtime);
        }

        RefreshDevSkillTexts();
    }

    public void CheatHealFull()
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_playerQuery.IsEmpty) return;

        Entity player = _playerQuery.GetSingletonEntity();
        var hp = _entityManager.GetComponentData<HealthData>(player);
        hp.CurrentHP = hp.MaxHP;
        _entityManager.SetComponentData(player, hp);
    }

    public void ToggleGodMode()
    {
        _isGodMode = !_isGodMode;
        CheatHealFull();
        if (_godModeBtnText != null)
        {
            _godModeBtnText.text = GameLocalization.DevGodMode(_isGodMode);
        }
    }

    public void CheatLevelUp()
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_playerQuery.IsEmpty) return;

        Entity player = _playerQuery.GetSingletonEntity();
        var levelData = _entityManager.GetComponentData<PlayerLevelData>(player);
        levelData.Level += 1;
        levelData.CurrentExp = 0;
        _entityManager.SetComponentData(player, levelData);
    }

    public void CheatKillAllEnemies()
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_enemyQuery.IsEmpty) return;

        using var enemies = _enemyQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (_entityManager.Exists(enemies[i]) && _entityManager.HasComponent<HealthData>(enemies[i]))
            {
                var hp = _entityManager.GetComponentData<HealthData>(enemies[i]);
                hp.CurrentHP = 0f; // Trừ sạch máu để kích hoạt DeathCleanupSystem rơi ngọc EXP
                _entityManager.SetComponentData(enemies[i], hp);
            }
        }
    }

    public void ToggleSpeedBoost()
    {
        if (!_ecsInitialized && !InitECS()) return;
        if (_playerQuery.IsEmpty) return;

        Entity player = _playerQuery.GetSingletonEntity();
        var move = _entityManager.GetComponentData<MovementData>(player);

        _isSpeedBoosted = !_isSpeedBoosted;
        if (_isSpeedBoosted)
        {
            _originalMoveSpeed = move.MoveSpeed;
            move.MoveSpeed *= 2.2f;
        }
        else
        {
            move.MoveSpeed = _originalMoveSpeed;
        }
        _entityManager.SetComponentData(player, move);

        if (_speedBtnText != null)
        {
            _speedBtnText.text = GameLocalization.DevSpeedBoost(_isSpeedBoosted);
        }
    }

    private void RefreshDevSkillTexts()
    {
        if (!_ecsInitialized && !InitECS()) return;

        if (_flyingSwordLvlText != null && !_flyingSwordQuery.IsEmpty)
        {
            int lvl = _flyingSwordQuery.GetSingleton<SkillRuntimeData>().Level;
            _flyingSwordLvlText.text = $"[ LV.{lvl} ]";
            _flyingSwordLvlText.color = lvl switch
            {
                0 => new Color(0.6f, 0.6f, 0.6f),
                4 => new Color(1f, 0.85f, 0.2f),
                _ => new Color(0.2f, 0.9f, 1f)
            };
        }

        if (_slashSwordLvlText != null && !_slashSwordQuery.IsEmpty)
        {
            int lvl = _slashSwordQuery.GetSingleton<SkillRuntimeData>().Level;
            _slashSwordLvlText.text = $"[ LV.{lvl} ]";
            _slashSwordLvlText.color = lvl switch
            {
                0 => new Color(0.6f, 0.6f, 0.6f),
                4 => new Color(1f, 0.85f, 0.2f),
                _ => new Color(0.85f, 0.45f, 1f)
            };
        }

        if (_projectileLvlText != null && !_projectileQuery.IsEmpty)
        {
            int lvl = _projectileQuery.GetSingleton<SkillRuntimeData>().Level;
            _projectileLvlText.text = $"[ LV.{lvl} ]";
            _projectileLvlText.color = lvl switch
            {
                0 => new Color(0.6f, 0.6f, 0.6f),
                5 => new Color(1f, 0.85f, 0.2f),
                _ => new Color(1f, 0.75f, 0.2f)
            };
        }

        if (_fusionStateText != null && !_fusionQuery.IsEmpty)
        {
            bool fused = _fusionQuery.GetSingleton<SkillRuntimeData>().Level >= 1;
            _fusionStateText.text = fused ? "[ ACTIVE ✦ ]" : "[ DISABLED ]";
            _fusionStateText.color = fused ? new Color(1f, 0.85f, 0.15f) : new Color(0.6f, 0.6f, 0.6f);
        }
    }

    private void UpdateLanguageUI()
    {
        if (_pauseTitleText != null) _pauseTitleText.text = GameLocalization.PauseHeader;
        if (_resumeBtnText != null) _resumeBtnText.text = GameLocalization.ResumeButton;
        if (_restartBtnText != null) _restartBtnText.text = GameLocalization.RestartButton;
        if (_devModeBtnText != null) _devModeBtnText.text = GameLocalization.DevModeButton;
        if (_devHeaderTitle != null) _devHeaderTitle.text = GameLocalization.DevPanelHeader;
        if (_godModeBtnText != null) _godModeBtnText.text = GameLocalization.DevGodMode(_isGodMode);
        if (_speedBtnText != null) _speedBtnText.text = GameLocalization.DevSpeedBoost(_isSpeedBoosted);

        RefreshDevSkillTexts();
    }

    // =========================================================================
    // XÂY DỰNG PROCEDURAL UI (CANVAS, BUTTONS, PAUSE PANEL, DEV PANEL)
    // =========================================================================
    private void BuildUI()
    {
        var old = GameObject.Find("GameSettings_Canvas");
        if (old != null) Destroy(old);

        var canvasObj = new GameObject("GameSettings_Canvas");
        DontDestroyOnLoad(canvasObj);

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1001; // Hiển thị trên cả HUD và Level-Up

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Nút Settings / Pause [⚙] và nút [🛠️ DEV] ở góc trên bên phải
        BuildTopBarButtons(canvasObj.transform);

        // 2. Menu Pause & Settings
        BuildPauseMenu(canvasObj.transform);

        // 3. Menu Developer Control Panel
        BuildDevPanel(canvasObj.transform);
    }

    private void BuildTopBarButtons(Transform parent)
    {
        // Nút Settings / Pause [⚙]
        var pauseBtnObj = new GameObject("Pause_Button");
        pauseBtnObj.transform.SetParent(parent, false);

        var pRect = pauseBtnObj.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(1f, 1f);
        pRect.anchorMax = new Vector2(1f, 1f);
        pRect.pivot = new Vector2(1f, 1f);
        pRect.anchoredPosition = new Vector2(-140f, -24f);
        pRect.sizeDelta = new Vector2(40f, 38f);

        var pBg = pauseBtnObj.AddComponent<Image>();
        pBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        var pOutline = pauseBtnObj.AddComponent<Outline>();
        pOutline.effectColor = new Color(0.35f, 0.55f, 0.75f, 0.95f);
        pOutline.effectDistance = new Vector2(2f, -2f);

        var pBtn = pauseBtnObj.AddComponent<Button>();
        pBtn.onClick.AddListener(TogglePause);

        var pTextObj = new GameObject("Icon");
        pTextObj.transform.SetParent(pauseBtnObj.transform, false);
        var pTextRect = pTextObj.AddComponent<RectTransform>();
        pTextRect.sizeDelta = new Vector2(40f, 38f);
        var pText = pTextObj.AddComponent<Text>();
        pText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pText.fontSize = 20;
        pText.alignment = TextAnchor.MiddleCenter;
        pText.color = new Color(0.9f, 0.95f, 1f);
        pText.text = "⚙";

        // Nút Developer Mode [🛠️ DEV]
        var devBtnObj = new GameObject("Dev_Button");
        devBtnObj.transform.SetParent(parent, false);

        var dRect = devBtnObj.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(1f, 1f);
        dRect.anchorMax = new Vector2(1f, 1f);
        dRect.pivot = new Vector2(1f, 1f);
        dRect.anchoredPosition = new Vector2(-190f, -24f);
        dRect.sizeDelta = new Vector2(44f, 38f);

        var dBg = devBtnObj.AddComponent<Image>();
        dBg.color = new Color(0.12f, 0.08f, 0.18f, 0.95f);

        var dOutline = devBtnObj.AddComponent<Outline>();
        dOutline.effectColor = new Color(0.75f, 0.35f, 1f, 0.95f);
        dOutline.effectDistance = new Vector2(2f, -2f);

        var dBtn = devBtnObj.AddComponent<Button>();
        dBtn.onClick.AddListener(ToggleDevPanel);

        var dTextObj = new GameObject("Icon");
        dTextObj.transform.SetParent(devBtnObj.transform, false);
        var dTextRect = dTextObj.AddComponent<RectTransform>();
        dTextRect.sizeDelta = new Vector2(44f, 38f);
        var dText = dTextObj.AddComponent<Text>();
        dText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dText.fontSize = 17;
        dText.alignment = TextAnchor.MiddleCenter;
        dText.color = new Color(0.85f, 0.65f, 1f);
        dText.text = "🛠️";
    }

    private void BuildPauseMenu(Transform parent)
    {
        _pausePanel = new GameObject("Pause_Panel");
        _pausePanel.transform.SetParent(parent, false);

        var rect = _pausePanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var dim = _pausePanel.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);

        // Khung Panel trung tâm
        var box = new GameObject("CenterBox");
        box.transform.SetParent(_pausePanel.transform, false);
        var bRect = box.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.sizeDelta = new Vector2(380f, 380f);

        var bImg = box.AddComponent<Image>();
        bImg.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);
        var bOutline = box.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.55f, 0.8f, 0.95f);
        bOutline.effectDistance = new Vector2(3f, -3f);

        // Tiêu đề: ✦ GAME PAUSED ✦
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(box.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 0.86f);
        tRect.anchorMax = new Vector2(0.5f, 0.86f);
        tRect.sizeDelta = new Vector2(340f, 40f);

        _pauseTitleText = titleObj.AddComponent<Text>();
        _pauseTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _pauseTitleText.fontSize = 24;
        _pauseTitleText.fontStyle = FontStyle.Bold;
        _pauseTitleText.alignment = TextAnchor.MiddleCenter;
        _pauseTitleText.color = new Color(1f, 0.86f, 0.3f);
        _pauseTitleText.text = GameLocalization.PauseHeader;

        // Container các nút
        float startY = 80f;
        float spacingY = 54f;

        // 1. Resume Button
        CreateMenuButton(box.transform, new Vector2(0f, startY), new Vector2(280f, 44f),
            new Color(0.12f, 0.35f, 0.55f, 1f), new Color(0.3f, 0.75f, 1f),
            GameLocalization.ResumeButton, ResumeGame, out _resumeBtnText);

        // 2. Restart Button
        CreateMenuButton(box.transform, new Vector2(0f, startY - spacingY), new Vector2(280f, 44f),
            new Color(0.45f, 0.18f, 0.18f, 1f), new Color(1f, 0.45f, 0.45f),
            GameLocalization.RestartButton, RestartGame, out _restartBtnText);

        // 3. Language Toggle Button
        CreateMenuButton(box.transform, new Vector2(0f, startY - spacingY * 2), new Vector2(280f, 44f),
            new Color(0.18f, 0.22f, 0.3f, 1f), new Color(0.55f, 0.75f, 1f),
            "🌐 LANGUAGE / NGÔN NGỮ", GameLocalization.ToggleLanguage, out _);

        // 4. Developer Mode Button
        CreateMenuButton(box.transform, new Vector2(0f, startY - spacingY * 3), new Vector2(280f, 44f),
            new Color(0.28f, 0.14f, 0.42f, 1f), new Color(0.85f, 0.45f, 1f),
            GameLocalization.DevModeButton, OpenDevPanel, out _devModeBtnText);

        _pausePanel.SetActive(false);
    }

    private void BuildDevPanel(Transform parent)
    {
        _devPanel = new GameObject("Dev_Panel");
        _devPanel.transform.SetParent(parent, false);

        var rect = _devPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var dim = _devPanel.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.02f, 0.04f, 0.92f);

        // Khung rộng rãi cho Dev Panel
        var box = new GameObject("DevBox");
        box.transform.SetParent(_devPanel.transform, false);
        var bRect = box.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.sizeDelta = new Vector2(760f, 560f);

        var bImg = box.AddComponent<Image>();
        bImg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);
        var bOutline = box.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.65f, 0.35f, 1f, 0.95f);
        bOutline.effectDistance = new Vector2(3f, -3f);

        // Header Title
        var headerObj = new GameObject("Header");
        headerObj.transform.SetParent(box.transform, false);
        var hRect = headerObj.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0.5f, 0.93f);
        hRect.anchorMax = new Vector2(0.5f, 0.93f);
        hRect.sizeDelta = new Vector2(700f, 36f);

        _devHeaderTitle = headerObj.AddComponent<Text>();
        _devHeaderTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _devHeaderTitle.fontSize = 22;
        _devHeaderTitle.fontStyle = FontStyle.Bold;
        _devHeaderTitle.alignment = TextAnchor.MiddleCenter;
        _devHeaderTitle.color = new Color(0.85f, 0.55f, 1f);
        _devHeaderTitle.text = GameLocalization.DevPanelHeader;

        // =====================================================================
        // SECTION 1: SKILL LEVEL CONTROLLERS
        // =====================================================================
        float curY = 190f;

        // 1. Flying Sword Controller (Lv 0 -> 4)
        BuildSkillRow(box.transform, "Flying Sword", curY, new Color(0.2f, 0.9f, 1f), 4,
            () => SetFlyingSwordLevel(-1),
            () => SetFlyingSwordLevel(1),
            (lvl) => SetFlyingSwordLevel(0, lvl),
            out _flyingSwordLvlText);

        curY -= 56f;
        // 2. Crescent Blade / Slash Sword Controller (Lv 0 -> 4)
        BuildSkillRow(box.transform, "Crescent Blade", curY, new Color(0.85f, 0.45f, 1f), 4,
            () => SetSlashSwordLevel(-1),
            () => SetSlashSwordLevel(1),
            (lvl) => SetSlashSwordLevel(0, lvl),
            out _slashSwordLvlText);

        curY -= 56f;
        // 3. Arcane Volley Controller (Lv 0 -> 5)
        BuildSkillRow(box.transform, "Arcane Volley", curY, new Color(1f, 0.75f, 0.2f), 5,
            () => SetProjectileLevel(-1),
            () => SetProjectileLevel(1),
            (lvl) => SetProjectileLevel(0, lvl),
            out _projectileLvlText);

        curY -= 56f;
        // 4. Fusion Toggle Controller
        BuildFusionRow(box.transform, curY, out _fusionStateText);

        curY -= 56f;
        // 5. MAX ALL SKILLS BUTTON
        CreateMenuButton(box.transform, new Vector2(0f, curY), new Vector2(500f, 40f),
            new Color(0.4f, 0.3f, 0.05f, 1f), new Color(1f, 0.85f, 0.2f),
            GameLocalization.DevMaxAllSkills, MaxAllSkills, out _);

        // =====================================================================
        // SECTION 2: UTILITY CHEATS
        // =====================================================================
        curY -= 60f;
        // Hàng Cheats 1: Full HP, God Mode, Level Up
        float cheatBtnWidth = 220f;
        float cheatBtnHeight = 38f;

        CreateMenuButton(box.transform, new Vector2(-240f, curY), new Vector2(cheatBtnWidth, cheatBtnHeight),
            new Color(0.35f, 0.08f, 0.12f, 1f), new Color(1f, 0.4f, 0.5f),
            GameLocalization.DevHealFull, CheatHealFull, out _);

        CreateMenuButton(box.transform, new Vector2(0f, curY), new Vector2(cheatBtnWidth, cheatBtnHeight),
            new Color(0.12f, 0.25f, 0.38f, 1f), new Color(0.3f, 0.85f, 1f),
            GameLocalization.DevGodMode(false), ToggleGodMode, out _godModeBtnText);

        CreateMenuButton(box.transform, new Vector2(240f, curY), new Vector2(cheatBtnWidth, cheatBtnHeight),
            new Color(0.28f, 0.22f, 0.06f, 1f), new Color(1f, 0.9f, 0.35f),
            GameLocalization.DevLevelUp, CheatLevelUp, out _);

        curY -= 48f;
        // Hàng Cheats 2: Kill All, Speed Boost, Close
        CreateMenuButton(box.transform, new Vector2(-240f, curY), new Vector2(cheatBtnWidth, cheatBtnHeight),
            new Color(0.35f, 0.12f, 0.32f, 1f), new Color(1f, 0.45f, 0.95f),
            GameLocalization.DevKillAll, CheatKillAllEnemies, out _);

        CreateMenuButton(box.transform, new Vector2(0f, curY), new Vector2(cheatBtnWidth, cheatBtnHeight),
            new Color(0.12f, 0.3f, 0.18f, 1f), new Color(0.4f, 1f, 0.55f),
            GameLocalization.DevSpeedBoost(false), ToggleSpeedBoost, out _speedBtnText);

        CreateMenuButton(box.transform, new Vector2(240f, curY), new Vector2(cheatBtnWidth, cheatBtnHeight),
            new Color(0.25f, 0.25f, 0.28f, 1f), new Color(0.85f, 0.85f, 0.95f),
            GameLocalization.CloseButton, CloseDevPanel, out _);

        _devPanel.SetActive(false);
    }

    private void BuildSkillRow(Transform parent, string skillName, float yPos, Color themeColor, int maxLevel,
        Action onMinus, Action onPlus, Action<int> onDirectLevel, out Text levelDisplay)
    {
        var rowObj = new GameObject($"Row_{skillName}");
        rowObj.transform.SetParent(parent, false);
        var rRect = rowObj.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.anchoredPosition = new Vector2(0f, yPos);
        rRect.sizeDelta = new Vector2(700f, 44f);

        var bg = rowObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.13f, 0.18f, 0.9f);
        var outl = rowObj.AddComponent<Outline>();
        outl.effectColor = new Color(themeColor.r * 0.5f, themeColor.g * 0.5f, themeColor.b * 0.5f, 0.8f);
        outl.effectDistance = new Vector2(1.5f, -1.5f);

        // Tên kỹ năng
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nRect = nameObj.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0f, 0.5f);
        nRect.pivot = new Vector2(0f, 0.5f);
        nRect.anchoredPosition = new Vector2(16f, 0f);
        nRect.sizeDelta = new Vector2(190f, 32f);

        var nText = nameObj.AddComponent<Text>();
        nText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nText.fontSize = 16;
        nText.fontStyle = FontStyle.Bold;
        nText.alignment = TextAnchor.MiddleLeft;
        nText.color = themeColor;
        nText.text = skillName;

        // Level Display Text
        var lvlObj = new GameObject("LevelText");
        lvlObj.transform.SetParent(rowObj.transform, false);
        var lRect = lvlObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0.5f, 0.5f);
        lRect.anchoredPosition = new Vector2(250f, 0f);
        lRect.sizeDelta = new Vector2(80f, 32f);

        levelDisplay = lvlObj.AddComponent<Text>();
        levelDisplay.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        levelDisplay.fontSize = 16;
        levelDisplay.fontStyle = FontStyle.Bold;
        levelDisplay.alignment = TextAnchor.MiddleCenter;
        levelDisplay.color = Color.white;
        levelDisplay.text = "[ LV.1 ]";

        // Nút [-]
        CreateSmallButton(rowObj.transform, new Vector2(310f, 0f), "—", onMinus);

        // Nút [+]
        CreateSmallButton(rowObj.transform, new Vector2(352f, 0f), "+", onPlus);

        // Các nút bấm trực tiếp [0], [1], [2], [3], [4], [5]
        float btnStartX = 415f;
        for (int i = 0; i <= maxLevel; i++)
        {
            int lvl = i;
            CreateDirectLevelButton(rowObj.transform, new Vector2(btnStartX + i * 44f, 0f), lvl,
                () => onDirectLevel(lvl));
        }
    }

    private void BuildFusionRow(Transform parent, float yPos, out Text stateDisplay)
    {
        var rowObj = new GameObject("Row_SwordFusion");
        rowObj.transform.SetParent(parent, false);
        var rRect = rowObj.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.5f);
        rRect.anchorMax = new Vector2(0.5f, 0.5f);
        rRect.anchoredPosition = new Vector2(0f, yPos);
        rRect.sizeDelta = new Vector2(700f, 44f);

        var bg = rowObj.AddComponent<Image>();
        bg.color = new Color(0.14f, 0.12f, 0.05f, 0.95f);
        var outl = rowObj.AddComponent<Outline>();
        outl.effectColor = new Color(1f, 0.85f, 0.15f, 0.8f);
        outl.effectDistance = new Vector2(1.5f, -1.5f);

        // Tên kỹ năng
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(rowObj.transform, false);
        var nRect = nameObj.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0f, 0.5f);
        nRect.pivot = new Vector2(0f, 0.5f);
        nRect.anchoredPosition = new Vector2(16f, 0f);
        nRect.sizeDelta = new Vector2(210f, 32f);

        var nText = nameObj.AddComponent<Text>();
        nText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nText.fontSize = 16;
        nText.fontStyle = FontStyle.Bold;
        nText.alignment = TextAnchor.MiddleLeft;
        nText.color = new Color(1f, 0.85f, 0.2f);
        nText.text = "Celestial Sword Fusion";

        // State Display Text
        var stObj = new GameObject("StateText");
        stObj.transform.SetParent(rowObj.transform, false);
        var sRect = stObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 0.5f);
        sRect.anchorMax = new Vector2(0f, 0.5f);
        sRect.pivot = new Vector2(0.5f, 0.5f);
        sRect.anchoredPosition = new Vector2(300f, 0f);
        sRect.sizeDelta = new Vector2(130f, 32f);

        stateDisplay = stObj.AddComponent<Text>();
        stateDisplay.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stateDisplay.fontSize = 15;
        stateDisplay.fontStyle = FontStyle.Bold;
        stateDisplay.alignment = TextAnchor.MiddleCenter;
        stateDisplay.color = new Color(1f, 0.85f, 0.15f);
        stateDisplay.text = "[ DISABLED ]";

        // Toggle Button
        CreateMenuButton(rowObj.transform, new Vector2(180f, 0f), new Vector2(230f, 34f),
            new Color(0.4f, 0.3f, 0.05f, 1f), new Color(1f, 0.9f, 0.35f),
            "⚡ TOGGLE FUSION", ToggleFusion, out _);
    }

    private void CreateSmallButton(Transform parent, Vector2 anchoredPos, string label, Action onClick)
    {
        var btnObj = new GameObject($"Btn_{label}");
        btnObj.transform.SetParent(parent, false);
        var rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(34f, 32f);

        var img = btnObj.AddComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.3f, 1f);
        var outl = btnObj.AddComponent<Outline>();
        outl.effectColor = new Color(0.45f, 0.65f, 0.85f);
        outl.effectDistance = new Vector2(1f, -1f);

        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        var tRect = txtObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(34f, 32f);
        var txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;
    }

    private void CreateDirectLevelButton(Transform parent, Vector2 anchoredPos, int level, Action onClick)
    {
        var btnObj = new GameObject($"Btn_Lv{level}");
        btnObj.transform.SetParent(parent, false);
        var rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(38f, 32f);

        var img = btnObj.AddComponent<Image>();
        img.color = level switch
        {
            0 => new Color(0.18f, 0.18f, 0.2f, 1f),
            _ => new Color(0.15f, 0.25f, 0.35f, 1f)
        };

        var outl = btnObj.AddComponent<Outline>();
        outl.effectColor = (level == 0) ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.35f, 0.75f, 1f);
        outl.effectDistance = new Vector2(1f, -1f);

        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        var tRect = txtObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(38f, 32f);
        var txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = (level == 0) ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.85f, 0.95f, 1f);
        txt.text = level == 0 ? "OFF" : $"L{level}";
    }

    private void CreateMenuButton(Transform parent, Vector2 anchoredPos, Vector2 size, Color bgColor, Color outlineColor,
        string label, Action onClick, out Text textComponent)
    {
        var btnObj = new GameObject($"Btn_{label}");
        btnObj.transform.SetParent(parent, false);

        var rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        var outl = btnObj.AddComponent<Outline>();
        outl.effectColor = outlineColor;
        outl.effectDistance = new Vector2(2f, -2f);

        var btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var tRect = textObj.AddComponent<RectTransform>();
        tRect.sizeDelta = size;

        textComponent = textObj.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 15;
        textComponent.fontStyle = FontStyle.Bold;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.color = Color.white;
        textComponent.text = label;
    }
}
