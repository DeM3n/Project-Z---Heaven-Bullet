using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Hybrid Bridge quản lý toàn bộ Giao diện HUD (HP, EXP, Level) ở góc trên bên trái
/// và Cửa sổ Level-Up Popup chọn 3 Perk kỹ năng khi lên cấp (Pause game) theo phong cách HD-2D Hi-Bit Pixel.
/// Hỗ trợ song ngữ (English & Vietnamese), mặc định English, có nút bấm và phím tắt [L] chuyển đổi tức thời.
/// </summary>
public class GameHUDBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private EntityQuery _flyingSwordSkillQuery;
    private EntityQuery _slashSwordSkillQuery;
    private EntityQuery _swordFusionSkillQuery;
    private EntityQuery _projectileSkillQuery;
    private EntityQuery _fireballSkillQuery;
    private EntityQuery _shieldSkillQuery;
    private EntityQuery _chestEventQuery;
    private bool _initialized;

    private static GameHUDBridge _instance;
    private int _lastPlayerLevel = 1;
    private int _pendingLevelUps = 0;
    private int _rerollsRemaining = 3;

    // --- UI Elements ---
    private Canvas _canvas;
    private Image _hpFillImage;
    private Text _hpText;
    private Image _expFillImage;
    private Text _expText;
    private Text _levelBadgeText;
    private Text _survivalTimerText;
    private float _gameSurvivalTime = 0f;

    private GameObject _levelUpPanel;
    private Text _levelUpTitleText;
    private Text _levelUpSubText;
    private Transform _cardsContainer;
    private Sprite _perkCardSprite;

    private Button _rerollButton;
    private Text _rerollButtonText;

    private Text _langButtonText;
    private Text _langHintText;

    // --- Banner Notification UI ---
    private GameObject _bannerObj;
    private Text _bannerText;
    private float _bannerTimer;

    // --- Ancient Chest Popup UI ---
    private GameObject _chestPanel;
    private Text _chestTitleText;
    private Text _chestSubText;
    private Text _relicIconText;
    private Text _relicNameText;
    private Text _relicBadgeText;
    private Text _relicDescText;
    private Text _collectButtonText;
    private RelicType _currentChestRelic;

    // Danh sách 3 Perk đang hiển thị trên popup hiện tại (để rebuild khi đổi ngôn ngữ)
    private readonly List<PerkType> _currentActivePerkTypes = new List<PerkType>();

    // Định nghĩa danh sách các loại Perk có thể nâng cấp
    private enum PerkType
    {
        FlyingSword,
        SlashSword,
        SwordFusion,
        ProjectileSkill,
        Fireball,
        Shield,
        Heal,
        MaxHp,
        MoveSpeed,
        PickupRange
    }

    private struct PerkOption
    {
        public PerkType Type;
        public int Level;
        public string Title;
        public string LevelBadge;
        public string Description;
        public Color HighlightColor;
    }

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

    private void Awake()
    {
        _instance = this;
        EnsureEventSystem();
        LoadSprites();
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
            ComponentType.ReadWrite<PlayerLevelData>());

        _flyingSwordSkillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FlyingSwordSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _slashSwordSkillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SlashSwordSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _swordFusionSkillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SwordFusionTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _projectileSkillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ProjectileSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _fireballSkillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FireballSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _shieldSkillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ShieldSkillTag>(),
            ComponentType.ReadWrite<SkillRuntimeData>());

        _chestEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ChestOpenEvent>());

        _initialized = true;
        return true;
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

    private void LoadSprites()
    {
        // Load Sprite thẻ bài nếu có
        _perkCardSprite = Resources.Load<Sprite>("Perk_Card_Art");
    }

    private void Update()
    {
        var kb = Keyboard.current;

        // Cập nhật Banner Notification
        if (_bannerTimer > 0f)
        {
            _bannerTimer -= Time.unscaledDeltaTime;
            if (_bannerTimer <= 0f && _bannerObj != null)
            {
                _bannerObj.SetActive(false);
            }
        }

        // Lắng nghe phím tắt [L] để đổi ngôn ngữ nhanh chóng
        if (kb != null && kb.lKey.wasPressedThisFrame)
        {
            GameLocalization.ToggleLanguage();
        }

        // Lắng nghe phím tắt [R] để Reroll thẻ bài khi bảng Level-Up đang mở
        if (kb != null && kb.rKey.wasPressedThisFrame && _levelUpPanel != null && _levelUpPanel.activeSelf)
        {
            OnRerollClicked();
        }

        // Lắng nghe phím [Space] hoặc [Enter] để nhận thưởng rương báu khi bảng Rương đang mở
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
        {
            if (_chestPanel != null && _chestPanel.activeSelf)
            {
                OnCollectChestReward();
            }
        }

        // Cập nhật Đồng hồ sinh tồn (Survival Timer: mm:ss)
        if (Time.timeScale > 0f)
        {
            _gameSurvivalTime += Time.deltaTime;
            if (_survivalTimerText != null)
            {
                int minutes = (int)(_gameSurvivalTime / 60f);
                int seconds = (int)(_gameSurvivalTime % 60f);
                _survivalTimerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        if (!_initialized && !InitECS()) return;
        if (_playerQuery.IsEmpty) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        HealthData hp = _entityManager.GetComponentData<HealthData>(playerEntity);
        PlayerLevelData levelData = _entityManager.GetComponentData<PlayerLevelData>(playerEntity);

        // 1. Cập nhật thanh Máu (HP) và Khiên (Shield)
        float hpPercent = Mathf.Clamp01(hp.CurrentHP / Mathf.Max(1f, hp.MaxHP));
        if (_hpFillImage != null) _hpFillImage.fillAmount = hpPercent;
        if (_hpText != null)
        {
            string hpString = $"{Mathf.Max(0, (int)hp.CurrentHP)} / {(int)hp.MaxHP}";
            if (_entityManager.HasComponent<PlayerShieldData>(playerEntity))
            {
                var shield = _entityManager.GetComponentData<PlayerShieldData>(playerEntity);
                if (shield.SkillLevel > 0)
                {
                    if (shield.CurrentCharges > 0)
                    {
                        string hitBadge = (shield.SkillLevel >= 4) ? $"({shield.HitsRemainingInCurrentShield}h)" : "";
                        hpString += $"  <color=#00e5ff>[🛡️ x{shield.CurrentCharges}{hitBadge}]</color>";
                    }
                    else
                    {
                        int remainSec = Mathf.Max(1, Mathf.CeilToInt(shield.RechargeCooldown - shield.RechargeTimer));
                        hpString += $"  <color=#80deea>[🛡️ {remainSec}s]</color>";
                    }
                }
            }
            _hpText.text = hpString;
        }

        // 2. Cập nhật thanh Kinh Nghiệm (EXP)
        float expPercent = Mathf.Clamp01((float)levelData.CurrentExp / Mathf.Max(1, levelData.ExpToNextLevel));
        if (_expFillImage != null) _expFillImage.fillAmount = expPercent;
        if (_expText != null) _expText.text = $"{levelData.CurrentExp} / {levelData.ExpToNextLevel} ({Mathf.RoundToInt(expPercent * 100f)}%)";

        // 3. Cập nhật Level Badge
        if (_levelBadgeText != null) _levelBadgeText.text = $"LV.{levelData.Level}";

        // 4. Phát hiện sự kiện lên cấp (Level-Up)
        if (levelData.Level > _lastPlayerLevel)
        {
            _pendingLevelUps += (levelData.Level - _lastPlayerLevel);
            _lastPlayerLevel = levelData.Level;

            if (_levelUpPanel != null && !_levelUpPanel.activeSelf)
            {
                ShowNextLevelUpReward();
            }
        }

        // 5. Phát hiện sự kiện mở Rương Cổ Vật (ChestOpenEvent)
        if (_chestEventQuery != null && !_chestEventQuery.IsEmpty && (_chestPanel == null || !_chestPanel.activeSelf))
        {
            var events = _chestEventQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < events.Length; i++)
            {
                _entityManager.DestroyEntity(events[i]);
            }
            events.Dispose();

            ShowChestReward();
        }
    }

    /// <summary>
    /// Đồng bộ toàn bộ văn bản UI khi ngôn ngữ thay đổi
    /// </summary>
    private void UpdateLanguageUI()
    {
        if (_langButtonText != null)
            _langButtonText.text = GameLocalization.LangButtonText;

        if (_langHintText != null)
            _langHintText.text = GameLocalization.LangHintText;

        if (_levelUpTitleText != null)
            _levelUpTitleText.text = GameLocalization.LevelUpHeader;

        if (_levelUpSubText != null)
            _levelUpSubText.text = GameLocalization.LevelUpSubtitle;

        if (_chestTitleText != null)
            _chestTitleText.text = GameLocalization.ChestHeader;

        if (_chestSubText != null)
            _chestSubText.text = GameLocalization.ChestSubtitle;

        if (_collectButtonText != null)
            _collectButtonText.text = GameLocalization.CollectButtonText;

        UpdateRerollButtonUI();

        // Nếu bảng chọn Perk đang mở, làm mới các thẻ bài với ngôn ngữ mới
        if (_levelUpPanel != null && _levelUpPanel.activeSelf && _currentActivePerkTypes.Count > 0)
        {
            RenderCurrentCards();
        }

        // Nếu bảng Rương đang mở, làm mới nội dung Cổ Vật
        if (_chestPanel != null && _chestPanel.activeSelf)
        {
            RenderChestRewardUI();
        }
    }

    private void ShowNextLevelUpReward()
    {
        if (_pendingLevelUps <= 0)
        {
            if (_levelUpPanel != null) _levelUpPanel.SetActive(false);
            Time.timeScale = 1f; // Khôi phục tốc độ game
            return;
        }

        Time.timeScale = 0f; // Pause game khi chọn Perk
        if (_levelUpPanel != null) _levelUpPanel.SetActive(true);

        PopulatePerkCards();
    }

    private void PopulatePerkCards()
    {
        var prevTypes = new HashSet<PerkType>(_currentActivePerkTypes);
        _currentActivePerkTypes.Clear();
        _currentActivePerkTypes.AddRange(GenerateRandomPerkTypes(3, prevTypes));
        RenderCurrentCards();
    }

    private void RenderCurrentCards()
    {
        if (_cardsContainer == null) return;

        // Xóa các card cũ
        foreach (Transform child in _cardsContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var type in _currentActivePerkTypes)
        {
            PerkOption option = BuildPerkOption(type);
            CreateCardUI(option);
        }
    }

    private PerkOption BuildPerkOption(PerkType type)
    {
        switch (type)
        {
            case PerkType.SwordFusion:
                return new PerkOption
                {
                    Type = PerkType.SwordFusion,
                    Level = 1,
                    Title = GameLocalization.SwordFusionTitle,
                    LevelBadge = GameLocalization.SwordFusionBadge,
                    Description = GameLocalization.SwordFusionDesc,
                    HighlightColor = new Color(1f, 0.85f, 0.15f) // Hoàng kim rực rỡ
                };

            case PerkType.FlyingSword:
            {
                int currentSwordLevel = GetFlyingSwordLevel();
                int nextLvl = Mathf.Min(4, currentSwordLevel + 1);
                return new PerkOption
                {
                    Type = PerkType.FlyingSword,
                    Level = nextLvl,
                    Title = GameLocalization.FlyingSwordTitle,
                    LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                    Description = GameLocalization.FlyingSwordDesc(nextLvl),
                    HighlightColor = new Color(0.2f, 0.9f, 1f) // Cyan sapphire
                };
            }

            case PerkType.SlashSword:
            {
                int currentSlashLevel = GetSlashSwordLevel();
                int nextLvl = Mathf.Min(4, currentSlashLevel + 1);
                return new PerkOption
                {
                    Type = PerkType.SlashSword,
                    Level = nextLvl,
                    Title = GameLocalization.SlashSwordTitle,
                    LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                    Description = GameLocalization.SlashSwordDesc(nextLvl),
                    HighlightColor = new Color(0.85f, 0.45f, 1f) // Violet Amethyst
                };
            }

            case PerkType.ProjectileSkill:
            {
                int currentProjLevel = GetProjectileSkillLevel();
                int nextLvl = currentProjLevel + 1;
                return new PerkOption
                {
                    Type = PerkType.ProjectileSkill,
                    Level = nextLvl,
                    Title = GameLocalization.ProjectileSkillTitle,
                    LevelBadge = GameLocalization.LevelBadge(nextLvl),
                    Description = GameLocalization.ProjectileSkillDesc(nextLvl),
                    HighlightColor = new Color(1f, 0.75f, 0.2f) // Amber Gold
                };
            }

            case PerkType.Fireball:
            {
                int currentFbLevel = GetFireballSkillLevel();
                int nextLvl = Mathf.Min(4, currentFbLevel + 1);
                return new PerkOption
                {
                    Type = PerkType.Fireball,
                    Level = nextLvl,
                    Title = GameLocalization.FireballTitle,
                    LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                    Description = GameLocalization.FireballDesc(nextLvl),
                    HighlightColor = new Color(1f, 0.42f, 0.12f) // Flaming Orange-Red
                };
            }

            case PerkType.Shield:
            {
                int currentShieldLevel = GetShieldSkillLevel();
                int nextLvl = Mathf.Min(4, currentShieldLevel + 1);
                return new PerkOption
                {
                    Type = PerkType.Shield,
                    Level = nextLvl,
                    Title = GameLocalization.ShieldTitle,
                    LevelBadge = GameLocalization.LevelBadge(nextLvl, nextLvl == 4),
                    Description = GameLocalization.ShieldDesc(nextLvl),
                    HighlightColor = new Color(0f, 0.9f, 1f) // Diamond Cyan
                };
            }

            case PerkType.Heal:
                return new PerkOption
                {
                    Type = PerkType.Heal,
                    Level = 1,
                    Title = GameLocalization.HealTitle,
                    LevelBadge = GameLocalization.RecoveryBadge,
                    Description = GameLocalization.HealDesc,
                    HighlightColor = new Color(0.3f, 1f, 0.4f) // Emerald Green
                };

            case PerkType.MaxHp:
                return new PerkOption
                {
                    Type = PerkType.MaxHp,
                    Level = 1,
                    Title = GameLocalization.MaxHpTitle,
                    LevelBadge = GameLocalization.UpgradeBadge,
                    Description = GameLocalization.MaxHpDesc,
                    HighlightColor = new Color(1f, 0.35f, 0.45f) // Ruby Red
                };

            case PerkType.MoveSpeed:
                return new PerkOption
                {
                    Type = PerkType.MoveSpeed,
                    Level = 1,
                    Title = GameLocalization.MoveSpeedTitle,
                    LevelBadge = GameLocalization.UpgradeBadge,
                    Description = GameLocalization.MoveSpeedDesc,
                    HighlightColor = new Color(0.9f, 0.5f, 1f) // Amethyst Purple
                };

            case PerkType.PickupRange:
                return new PerkOption
                {
                    Type = PerkType.PickupRange,
                    Level = 1,
                    Title = GameLocalization.PickupRangeTitle,
                    LevelBadge = GameLocalization.UtilityBadge,
                    Description = GameLocalization.PickupRangeDesc,
                    HighlightColor = new Color(0.2f, 0.8f, 0.9f) // Sky Blue
                };

            default:
                return default;
        }
    }

    private List<PerkType> GenerateRandomPerkTypes(int count, HashSet<PerkType> excludedTypes = null)
    {
        List<PerkType> available = new List<PerkType>();

        // Kiểm tra điều kiện Hợp Thể (Sword Fusion): Cả 2 kiếm đều max level 4 và chưa hợp thể
        bool canFuse = GetFlyingSwordLevel() >= 4 && GetSlashSwordLevel() >= 4 && !IsSwordFused();
        if (canFuse)
        {
            available.Add(PerkType.SwordFusion);
        }

        // 1. Phi kiếm (Flying Sword)
        int currentSwordLevel = GetFlyingSwordLevel();
        if (currentSwordLevel < 4)
        {
            available.Add(PerkType.FlyingSword);
        }

        // 2. Trảm Yêu Kiếm (Slash Sword)
        int currentSlashLevel = GetSlashSwordLevel();
        if (currentSlashLevel < 4)
        {
            available.Add(PerkType.SlashSword);
        }

        // 3. Đạn Ma Pháp (Arcane Volley)
        int currentProjLevel = GetProjectileSkillLevel();
        if (currentProjLevel < 5)
        {
            available.Add(PerkType.ProjectileSkill);
        }

        // 4. Cầu Lửa Viêm Ma (Fireball)
        int currentFbLevel = GetFireballSkillLevel();
        if (currentFbLevel < 4)
        {
            available.Add(PerkType.Fireball);
        }

        // 5. Khiên Hộ Thể (Aegis Shield)
        int currentShieldLevel = GetShieldSkillLevel();
        if (currentShieldLevel < 4)
        {
            available.Add(PerkType.Shield);
        }

        // 5. Tiên Dược Hồi Máu (Heal)
        available.Add(PerkType.Heal);

        // 5. Thể Lực Cương Thể (Max HP)
        available.Add(PerkType.MaxHp);

        // 6. Thần Hành Thuật (Move Speed)
        available.Add(PerkType.MoveSpeed);

        // 7. Hút Linh Thạch (Pickup Range)
        available.Add(PerkType.PickupRange);

        // Lọc bớt các perk vừa hiển thị khi reroll (trừ SwordFusion luôn được ưu tiên)
        if (excludedTypes != null && excludedTypes.Count > 0)
        {
            List<PerkType> filtered = new List<PerkType>();
            foreach (var item in available)
            {
                if (item == PerkType.SwordFusion || !excludedTypes.Contains(item))
                {
                    filtered.Add(item);
                }
            }
            if (filtered.Count >= count)
            {
                available = filtered;
            }
        }

        // Nếu có thẻ Hợp Thể (SwordFusion), tách ra để ưu tiên hiển thị đầu tiên
        bool hasFusion = available.Contains(PerkType.SwordFusion);
        if (hasFusion) available.Remove(PerkType.SwordFusion);

        // Shuffle ngẫu nhiên
        for (int i = 0; i < available.Count; i++)
        {
            int rnd = UnityEngine.Random.Range(i, available.Count);
            (available[i], available[rnd]) = (available[rnd], available[i]);
        }

        if (hasFusion)
        {
            available.Insert(0, PerkType.SwordFusion);
        }

        int selectCount = Mathf.Min(count, available.Count);
        return available.GetRange(0, selectCount);
    }

    private void ApplyPerk(PerkOption perk)
    {
        if (_playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        switch (perk.Type)
        {
            case PerkType.SwordFusion:
                if (!_swordFusionSkillQuery.IsEmpty)
                {
                    Entity skillEntity = _swordFusionSkillQuery.GetSingletonEntity();
                    var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                    runtime.Level = 1;
                    runtime.Timer = 0.5f; // Triệu hồi kiếm khổng lồ đầu tiên sau 0.5s!
                    _entityManager.SetComponentData(skillEntity, runtime);
                }
                break;

            case PerkType.FlyingSword:
                if (!_flyingSwordSkillQuery.IsEmpty)
                {
                    Entity skillEntity = _flyingSwordSkillQuery.GetSingletonEntity();
                    var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                    runtime.Level = Mathf.Min(4, runtime.Level + 1);
                    _entityManager.SetComponentData(skillEntity, runtime);
                }
                break;

            case PerkType.SlashSword:
                if (!_slashSwordSkillQuery.IsEmpty)
                {
                    Entity skillEntity = _slashSwordSkillQuery.GetSingletonEntity();
                    var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                    runtime.Level = Mathf.Min(4, runtime.Level + 1);
                    _entityManager.SetComponentData(skillEntity, runtime);
                }
                break;

            case PerkType.ProjectileSkill:
                if (!_projectileSkillQuery.IsEmpty)
                {
                    Entity skillEntity = _projectileSkillQuery.GetSingletonEntity();
                    var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                    runtime.Level += 1;
                    _entityManager.SetComponentData(skillEntity, runtime);
                }
                break;

            case PerkType.Fireball:
                if (!_fireballSkillQuery.IsEmpty)
                {
                    Entity skillEntity = _fireballSkillQuery.GetSingletonEntity();
                    var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                    runtime.Level = Mathf.Min(4, runtime.Level + 1);
                    _entityManager.SetComponentData(skillEntity, runtime);
                }
                break;

            case PerkType.Shield:
                if (!_shieldSkillQuery.IsEmpty)
                {
                    Entity skillEntity = _shieldSkillQuery.GetSingletonEntity();
                    var runtime = _entityManager.GetComponentData<SkillRuntimeData>(skillEntity);
                    runtime.Level = Mathf.Min(4, runtime.Level + 1);
                    _entityManager.SetComponentData(skillEntity, runtime);
                }
                break;

            case PerkType.Heal:
                var hpHeal = _entityManager.GetComponentData<HealthData>(playerEntity);
                hpHeal.CurrentHP = Mathf.Min(hpHeal.MaxHP, hpHeal.CurrentHP + hpHeal.MaxHP * 0.4f);
                _entityManager.SetComponentData(playerEntity, hpHeal);
                break;

            case PerkType.MaxHp:
                var hpMax = _entityManager.GetComponentData<HealthData>(playerEntity);
                hpMax.MaxHP += 30f;
                hpMax.CurrentHP += 30f;
                _entityManager.SetComponentData(playerEntity, hpMax);
                break;

            case PerkType.MoveSpeed:
                var move = _entityManager.GetComponentData<MovementData>(playerEntity);
                move.MoveSpeed *= 1.15f;
                _entityManager.SetComponentData(playerEntity, move);
                break;

            case PerkType.PickupRange:
                var pickup = _entityManager.GetComponentData<PickupRangeData>(playerEntity);
                pickup.Range += 1.5f;
                _entityManager.SetComponentData(playerEntity, pickup);
                break;
        }

        _pendingLevelUps--;
        ShowNextLevelUpReward();
    }

    private void OnRerollClicked()
    {
        if (_rerollsRemaining <= 0) return;
        _rerollsRemaining--;
        UpdateRerollButtonUI();
        PopulatePerkCards();
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

    private bool IsSwordFused()
    {
        if (!_swordFusionSkillQuery.IsEmpty)
        {
            return _swordFusionSkillQuery.GetSingleton<SkillRuntimeData>().Level >= 1;
        }
        return false;
    }

    private int GetFlyingSwordLevel()
    {
        if (!_flyingSwordSkillQuery.IsEmpty)
        {
            return _flyingSwordSkillQuery.GetSingleton<SkillRuntimeData>().Level;
        }
        return 1;
    }

    private int GetSlashSwordLevel()
    {
        if (!_slashSwordSkillQuery.IsEmpty)
        {
            return _slashSwordSkillQuery.GetSingleton<SkillRuntimeData>().Level;
        }
        return 1;
    }

    private int GetProjectileSkillLevel()
    {
        if (!_projectileSkillQuery.IsEmpty)
        {
            return _projectileSkillQuery.GetSingleton<SkillRuntimeData>().Level;
        }
        return 1;
    }

    private int GetFireballSkillLevel()
    {
        if (!_fireballSkillQuery.IsEmpty)
        {
            return _fireballSkillQuery.GetSingleton<SkillRuntimeData>().Level;
        }
        return 1;
    }

    private int GetShieldSkillLevel()
    {
        if (!_shieldSkillQuery.IsEmpty)
        {
            return _shieldSkillQuery.GetSingleton<SkillRuntimeData>().Level;
        }
        return 0;
    }

    // =========================================================================
    // XÂY DỰNG GIAO DIỆN PROCEDURAL UI (CANVAS, HUD, LEVEL-UP POPUP, LANGUAGE BTN)
    // =========================================================================
    private void BuildUI()
    {
        // 1. Tạo Canvas
        var oldCanvas = GameObject.Find("GameHUD_Canvas");
        if (oldCanvas != null) Destroy(oldCanvas);

        var canvasObj = new GameObject("GameHUD_Canvas");
        DontDestroyOnLoad(canvasObj);

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999; // Luôn hiển thị trên cùng

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); // Chuẩn 720p giúp HUD to rõ ràng trên mọi màn hình
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Tạo HUD góc trên bên trái
        BuildHUDPanel(canvasObj.transform);

        // 3. Tạo đồng hồ sinh tồn (Survival Timer: mm:ss) ở giữa phía trên
        BuildSurvivalTimer(canvasObj.transform);

        // 4. Tạo nút chuyển đổi ngôn ngữ ở góc trên bên phải
        BuildLanguageButton(canvasObj.transform);

        // 5. Tạo Level-Up Popup
        BuildLevelUpPanel(canvasObj.transform);

        // 6. Tạo Banner thông báo (Treasure Goblin Alert)
        BuildBannerNotification(canvasObj.transform);

        // 7. Tạo Ancient Chest Popup
        BuildChestPanel(canvasObj.transform);
    }

    private void BuildSurvivalTimer(Transform parent)
    {
        var timerObj = new GameObject("Survival_Timer");
        timerObj.transform.SetParent(parent, false);

        var rect = timerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(130f, 38f);

        var bg = timerObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

        var outline = timerObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.45f, 0.6f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(timerObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _survivalTimerText = textObj.AddComponent<Text>();
        _survivalTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _survivalTimerText.fontSize = 20;
        _survivalTimerText.fontStyle = FontStyle.Bold;
        _survivalTimerText.alignment = TextAnchor.MiddleCenter;
        _survivalTimerText.color = new Color(1f, 0.9f, 0.4f);
        _survivalTimerText.text = "00:00";

        var textOutline = textObj.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1f, -1f);
    }

    private void BuildHUDPanel(Transform parent)
    {
        var hudRoot = new GameObject("HUD_TopLeft");
        hudRoot.transform.SetParent(parent, false);

        var rect = hudRoot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(350f, 78f); // Kích thước vừa vặn, thanh thoát

        // Khung nền HUD tối viền Hi-Bit
        var bgImage = hudRoot.AddComponent<Image>();
        bgImage.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

        // Thêm viền ngoài màu kim loại sáng rõ
        var outline = hudRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.45f, 0.6f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        // --- Level Badge (Huy hiệu cấp độ) ---
        var badgeObj = new GameObject("LevelBadge");
        badgeObj.transform.SetParent(hudRoot.transform, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(38f, 0f);
        badgeRect.sizeDelta = new Vector2(58f, 58f);

        var badgeImg = badgeObj.AddComponent<Image>();
        badgeImg.color = new Color(0.15f, 0.2f, 0.3f, 1f);
        var badgeOutline = badgeObj.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(1f, 0.85f, 0.25f, 1f); // Viền vàng kim rực rỡ
        badgeOutline.effectDistance = new Vector2(2f, -2f);

        var badgeTextObj = new GameObject("Text");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        var bTextRect = badgeTextObj.AddComponent<RectTransform>();
        bTextRect.sizeDelta = new Vector2(58f, 58f);
        _levelBadgeText = badgeTextObj.AddComponent<Text>();
        _levelBadgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _levelBadgeText.fontSize = 20;
        _levelBadgeText.fontStyle = FontStyle.Bold;
        _levelBadgeText.alignment = TextAnchor.MiddleCenter;
        _levelBadgeText.color = new Color(1f, 0.9f, 0.3f);
        _levelBadgeText.text = "LV.1";
        var bTextOutline = badgeTextObj.AddComponent<Outline>();
        bTextOutline.effectColor = Color.black;
        bTextOutline.effectDistance = new Vector2(1f, -1f);

        // --- Container thanh Máu & EXP ---
        var barsContainer = new GameObject("BarsContainer");
        barsContainer.transform.SetParent(hudRoot.transform, false);
        var barsRect = barsContainer.AddComponent<RectTransform>();
        barsRect.anchorMin = new Vector2(0f, 0f);
        barsRect.anchorMax = new Vector2(1f, 1f);
        barsRect.offsetMin = new Vector2(76f, 10f);
        barsRect.offsetMax = new Vector2(-12f, -10f);

        // === Thanh Máu (HP Bar) ===
        var hpBarObj = new GameObject("HP_Bar");
        hpBarObj.transform.SetParent(barsContainer.transform, false);
        var hpRect = hpBarObj.AddComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0f, 0.46f);
        hpRect.anchorMax = new Vector2(1f, 1f);
        hpRect.offsetMin = Vector2.zero;
        hpRect.offsetMax = Vector2.zero;

        var hpBg = hpBarObj.AddComponent<Image>();
        hpBg.color = new Color(0.18f, 0.05f, 0.08f, 0.95f); // Nền đỏ sẫm
        var hpBgOutline = hpBarObj.AddComponent<Outline>();
        hpBgOutline.effectColor = new Color(0.4f, 0.15f, 0.18f, 1f);
        hpBgOutline.effectDistance = new Vector2(1f, -1f);

        var hpFillObj = new GameObject("HP_Fill");
        hpFillObj.transform.SetParent(hpBarObj.transform, false);
        var hpFillRect = hpFillObj.AddComponent<RectTransform>();
        hpFillRect.anchorMin = Vector2.zero;
        hpFillRect.anchorMax = Vector2.one;
        hpFillRect.offsetMin = new Vector2(2f, 2f);
        hpFillRect.offsetMax = new Vector2(-2f, -2f);

        _hpFillImage = hpFillObj.AddComponent<Image>();
        _hpFillImage.type = Image.Type.Filled;
        _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        _hpFillImage.color = new Color(0.95f, 0.22f, 0.28f, 1f); // Đỏ ruby tươi sáng

        var hpTextObj = new GameObject("HP_Text");
        hpTextObj.transform.SetParent(hpBarObj.transform, false);
        var hpTextRect = hpTextObj.AddComponent<RectTransform>();
        hpTextRect.anchorMin = Vector2.zero;
        hpTextRect.anchorMax = Vector2.one;
        _hpText = hpTextObj.AddComponent<Text>();
        _hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hpText.fontSize = 14;
        _hpText.fontStyle = FontStyle.Bold;
        _hpText.alignment = TextAnchor.MiddleCenter;
        _hpText.color = Color.white;
        _hpText.text = "100 / 100";
        var hpTextOutline = hpTextObj.AddComponent<Outline>();
        hpTextOutline.effectColor = Color.black;
        hpTextOutline.effectDistance = new Vector2(1f, -1f);

        // === Thanh Kinh Nghiệm (EXP Bar) ===
        var expBarObj = new GameObject("EXP_Bar");
        expBarObj.transform.SetParent(barsContainer.transform, false);
        var expRect = expBarObj.AddComponent<RectTransform>();
        expRect.anchorMin = new Vector2(0f, 0f);
        expRect.anchorMax = new Vector2(1f, 0.38f);
        expRect.offsetMin = Vector2.zero;
        expRect.offsetMax = Vector2.zero;

        var expBg = expBarObj.AddComponent<Image>();
        expBg.color = new Color(0.04f, 0.12f, 0.2f, 0.95f); // Nền xanh sẫm
        var expBgOutline = expBarObj.AddComponent<Outline>();
        expBgOutline.effectColor = new Color(0.12f, 0.25f, 0.38f, 1f);
        expBgOutline.effectDistance = new Vector2(1f, -1f);

        var expFillObj = new GameObject("EXP_Fill");
        expFillObj.transform.SetParent(expBarObj.transform, false);
        var expFillRect = expFillObj.AddComponent<RectTransform>();
        expFillRect.anchorMin = Vector2.zero;
        expFillRect.anchorMax = Vector2.one;
        expFillRect.offsetMin = new Vector2(1.5f, 1.5f);
        expFillRect.offsetMax = new Vector2(-1.5f, -1.5f);

        _expFillImage = expFillObj.AddComponent<Image>();
        _expFillImage.type = Image.Type.Filled;
        _expFillImage.fillMethod = Image.FillMethod.Horizontal;
        _expFillImage.color = new Color(0.08f, 0.92f, 0.98f, 1f); // Cyan sapphire phát sáng

        var expTextObj = new GameObject("EXP_Text");
        expTextObj.transform.SetParent(expBarObj.transform, false);
        var expTextRect = expTextObj.AddComponent<RectTransform>();
        expTextRect.anchorMin = Vector2.zero;
        expTextRect.anchorMax = Vector2.one;
        _expText = expTextObj.AddComponent<Text>();
        _expText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _expText.fontSize = 11;
        _expText.fontStyle = FontStyle.Bold;
        _expText.alignment = TextAnchor.MiddleCenter;
        _expText.color = new Color(0.9f, 0.98f, 1f);
        _expText.text = "0 / 10 (0%)";
        var expTextOutline = expTextObj.AddComponent<Outline>();
        expTextOutline.effectColor = Color.black;
        expTextOutline.effectDistance = new Vector2(1f, -1f);
    }

    /// <summary>
    /// Nút chuyển đổi ngôn ngữ ở góc trên bên phải (Top-Right)
    /// </summary>
    private void BuildLanguageButton(Transform parent)
    {
        var langBtnObj = new GameObject("Language_Button");
        langBtnObj.transform.SetParent(parent, false);

        var rect = langBtnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(106f, 38f);

        var bg = langBtnObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        var outline = langBtnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.55f, 0.75f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        var btn = langBtnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = colors;
        btn.onClick.AddListener(GameLocalization.ToggleLanguage);

        // Label hiển thị EN / VI
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(langBtnObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.3f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        _langButtonText = labelObj.AddComponent<Text>();
        _langButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _langButtonText.fontSize = 15;
        _langButtonText.fontStyle = FontStyle.Bold;
        _langButtonText.alignment = TextAnchor.MiddleCenter;
        _langButtonText.color = new Color(1f, 0.9f, 0.4f);
        _langButtonText.text = GameLocalization.LangButtonText;

        // Subtext hiển thị phím tắt [L]
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(langBtnObj.transform, false);
        var hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0.35f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;

        _langHintText = hintObj.AddComponent<Text>();
        _langHintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _langHintText.fontSize = 9;
        _langHintText.alignment = TextAnchor.MiddleCenter;
        _langHintText.color = new Color(0.65f, 0.75f, 0.85f);
        _langHintText.text = GameLocalization.LangHintText;
    }

    private void BuildLevelUpPanel(Transform parent)
    {
        _levelUpPanel = new GameObject("LevelUp_Panel");
        _levelUpPanel.transform.SetParent(parent, false);

        var rect = _levelUpPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Nền tối mờ toàn màn hình (Dim Background)
        var dimImg = _levelUpPanel.AddComponent<Image>();
        dimImg.color = new Color(0.03f, 0.05f, 0.08f, 0.85f);

        // Header Title: ✦ LEVEL UP ✦
        var titleObj = new GameObject("HeaderTitle");
        titleObj.transform.SetParent(_levelUpPanel.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.82f);
        titleRect.anchorMax = new Vector2(0.5f, 0.82f);
        titleRect.sizeDelta = new Vector2(600f, 70f);

        _levelUpTitleText = titleObj.AddComponent<Text>();
        _levelUpTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _levelUpTitleText.fontSize = 42;
        _levelUpTitleText.fontStyle = FontStyle.Bold;
        _levelUpTitleText.alignment = TextAnchor.MiddleCenter;
        _levelUpTitleText.color = new Color(1f, 0.86f, 0.3f); // Vàng kim rực rỡ
        _levelUpTitleText.text = GameLocalization.LevelUpHeader;
        var titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.5f, 0.2f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(3f, -3f);

        // Subtitle hướng dẫn
        var subObj = new GameObject("SubTitle");
        subObj.transform.SetParent(_levelUpPanel.transform, false);
        var subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.74f);
        subRect.anchorMax = new Vector2(0.5f, 0.74f);
        subRect.sizeDelta = new Vector2(560f, 35f);

        _levelUpSubText = subObj.AddComponent<Text>();
        _levelUpSubText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _levelUpSubText.fontSize = 16;
        _levelUpSubText.alignment = TextAnchor.MiddleCenter;
        _levelUpSubText.color = new Color(0.75f, 0.82f, 0.9f);
        _levelUpSubText.text = GameLocalization.LevelUpSubtitle;

        // Container chứa 3 thẻ Card
        var containerObj = new GameObject("CardsContainer");
        containerObj.transform.SetParent(_levelUpPanel.transform, false);
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
        var rerollObj = new GameObject("Reroll_Button");
        rerollObj.transform.SetParent(_levelUpPanel.transform, false);
        var rerollRect = rerollObj.AddComponent<RectTransform>();
        rerollRect.anchorMin = new Vector2(0.5f, 0.13f);
        rerollRect.anchorMax = new Vector2(0.5f, 0.13f);
        rerollRect.sizeDelta = new Vector2(260f, 44f);

        var rerollBg = rerollObj.AddComponent<Image>();
        rerollBg.color = new Color(0.12f, 0.1f, 0.2f, 0.96f);

        var rerollOutline = rerollObj.AddComponent<Outline>();
        rerollOutline.effectColor = new Color(0.65f, 0.45f, 1f, 0.95f);
        rerollOutline.effectDistance = new Vector2(2f, -2f);

        _rerollButton = rerollObj.AddComponent<Button>();
        var rColors = _rerollButton.colors;
        rColors.normalColor = Color.white;
        rColors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        rColors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        rColors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        _rerollButton.colors = rColors;
        _rerollButton.onClick.AddListener(OnRerollClicked);

        var rTextObj = new GameObject("Text");
        rTextObj.transform.SetParent(rerollObj.transform, false);
        var rTextRect = rTextObj.AddComponent<RectTransform>();
        rTextRect.sizeDelta = new Vector2(260f, 44f);

        _rerollButtonText = rTextObj.AddComponent<Text>();
        _rerollButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _rerollButtonText.fontSize = 15;
        _rerollButtonText.fontStyle = FontStyle.Bold;
        _rerollButtonText.alignment = TextAnchor.MiddleCenter;
        _rerollButtonText.color = new Color(0.88f, 0.78f, 1f);
        UpdateRerollButtonUI();

        // Mặc định ẩn popup
        _levelUpPanel.SetActive(false);
    }

    private void CreateCardUI(PerkOption perk)
    {
        var cardObj = new GameObject($"Card_{perk.Title}");
        cardObj.transform.SetParent(_cardsContainer, false);

        var rect = cardObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250f, 370f);

        bool isFusion = perk.Type == PerkType.SwordFusion;

        // Nền thẻ bài Hi-Bit Pixel (Obsidian dark panel hoặc Royal Gold cho Fusion)
        var cardImg = cardObj.AddComponent<Image>();
        cardImg.color = isFusion
            ? new Color(0.18f, 0.13f, 0.04f, 0.98f)
            : new Color(0.1f, 0.13f, 0.18f, 0.98f);

        // Viền thẻ phát sáng theo màu kỹ năng (Dày hơn cho Fusion)
        var outline = cardObj.AddComponent<Outline>();
        outline.effectColor = perk.HighlightColor;
        outline.effectDistance = isFusion ? new Vector2(4f, -4f) : new Vector2(3f, -3f);

        // Nút Button tương tác khi bấm vào cả thẻ
        var btn = cardObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        btn.colors = colors;
        btn.onClick.AddListener(() => ApplyPerk(perk));

        // --- Badge Cấp độ (Phía trên) ---
        var badgeObj = new GameObject("Badge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 0.88f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.88f);
        badgeRect.sizeDelta = new Vector2(220f, 30f);

        var badgeText = badgeObj.AddComponent<Text>();
        badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        badgeText.fontSize = 13;
        badgeText.fontStyle = FontStyle.Bold;
        badgeText.alignment = TextAnchor.MiddleCenter;
        badgeText.color = perk.HighlightColor;
        badgeText.text = $"[ {perk.LevelBadge} ]";

        // --- Tiêu đề Kỹ năng ---
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.74f);
        titleRect.anchorMax = new Vector2(0.5f, 0.74f);
        titleRect.sizeDelta = new Vector2(230f, 40f);

        var titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 17;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = perk.Title;

        // Vạch kẻ ngăn cách
        var lineObj = new GameObject("Divider");
        lineObj.transform.SetParent(cardObj.transform, false);
        var lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.65f);
        lineRect.anchorMax = new Vector2(0.5f, 0.65f);
        lineRect.sizeDelta = new Vector2(180f, 2f);
        var lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(perk.HighlightColor.r, perk.HighlightColor.g, perk.HighlightColor.b, 0.5f);

        // --- Mô tả chi tiết ---
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(cardObj.transform, false);
        var descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.5f, 0.38f);
        descRect.anchorMax = new Vector2(0.5f, 0.38f);
        descRect.sizeDelta = new Vector2(215f, 140f);

        var descText = descObj.AddComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 13;
        descText.alignment = TextAnchor.UpperCenter;
        descText.color = new Color(0.85f, 0.9f, 0.95f);
        descText.text = perk.Description;

        // --- Nút Chọn (Button Label ở đáy) ---
        var selectBtnObj = new GameObject("SelectButton");
        selectBtnObj.transform.SetParent(cardObj.transform, false);
        var selectBtnRect = selectBtnObj.AddComponent<RectTransform>();
        selectBtnRect.anchorMin = new Vector2(0.5f, 0.1f);
        selectBtnRect.anchorMax = new Vector2(0.5f, 0.1f);
        selectBtnRect.sizeDelta = new Vector2(190f, 38f);

        var selectBtnImg = selectBtnObj.AddComponent<Image>();
        selectBtnImg.color = new Color(perk.HighlightColor.r * 0.3f, perk.HighlightColor.g * 0.3f, perk.HighlightColor.b * 0.3f, 1f);
        var sOutline = selectBtnObj.AddComponent<Outline>();
        sOutline.effectColor = perk.HighlightColor;
        sOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(selectBtnObj.transform, false);
        var btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.sizeDelta = new Vector2(190f, 38f);

        var btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 15;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        btnText.text = GameLocalization.SelectButtonText;
    }

    // =========================================================================
    // BANNER NOTIFICATION & ANCIENT CHEST POPUP MODAL
    // =========================================================================
    public static void ShowBannerNotification(string msg)
    {
        if (_instance != null)
        {
            _instance.TriggerBanner(msg);
        }
    }

    public void TriggerBanner(string msg)
    {
        if (_bannerObj != null && _bannerText != null)
        {
            _bannerText.text = msg;
            _bannerObj.SetActive(true);
            _bannerTimer = 3.5f;
        }
    }

    private void BuildBannerNotification(Transform parent)
    {
        _bannerObj = new GameObject("Banner_Notification");
        _bannerObj.transform.SetParent(parent, false);

        var rect = _bannerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -80f);
        rect.sizeDelta = new Vector2(560f, 42f);

        var bg = _bannerObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.02f, 0.94f);

        var outline = _bannerObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.82f, 0.2f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(_bannerObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _bannerText = textObj.AddComponent<Text>();
        _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _bannerText.fontSize = 16;
        _bannerText.fontStyle = FontStyle.Bold;
        _bannerText.alignment = TextAnchor.MiddleCenter;
        _bannerText.color = new Color(1f, 0.9f, 0.35f);
        _bannerText.text = "";

        _bannerObj.SetActive(false);
    }

    private void BuildChestPanel(Transform parent)
    {
        _chestPanel = new GameObject("Chest_Panel");
        _chestPanel.transform.SetParent(parent, false);

        var rect = _chestPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Nền tối mờ toàn màn hình
        var dimImg = _chestPanel.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.04f, 0.07f, 0.88f);

        // Khung Modal trung tâm
        var modalObj = new GameObject("ModalBox");
        modalObj.transform.SetParent(_chestPanel.transform, false);
        var modalRect = modalObj.AddComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);
        modalRect.sizeDelta = new Vector2(580f, 440f);

        var modalBg = modalObj.AddComponent<Image>();
        modalBg.color = new Color(0.06f, 0.08f, 0.14f, 0.98f);

        var modalOutline = modalObj.AddComponent<Outline>();
        modalOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.95f);
        modalOutline.effectDistance = new Vector2(3f, -3f);

        // Header Title
        var titleObj = new GameObject("HeaderTitle");
        titleObj.transform.SetParent(modalObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -22f);
        titleRect.sizeDelta = new Vector2(540f, 48f);

        _chestTitleText = titleObj.AddComponent<Text>();
        _chestTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _chestTitleText.fontSize = 28;
        _chestTitleText.fontStyle = FontStyle.Bold;
        _chestTitleText.alignment = TextAnchor.MiddleCenter;
        _chestTitleText.color = new Color(1f, 0.85f, 0.25f);
        _chestTitleText.text = GameLocalization.ChestHeader;

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
        subRect.anchoredPosition = new Vector2(0f, -68f);
        subRect.sizeDelta = new Vector2(500f, 30f);

        _chestSubText = subObj.AddComponent<Text>();
        _chestSubText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _chestSubText.fontSize = 14;
        _chestSubText.alignment = TextAnchor.MiddleCenter;
        _chestSubText.color = new Color(0.75f, 0.82f, 0.9f);
        _chestSubText.text = GameLocalization.ChestSubtitle;

        // Card hiển thị Cổ Vật
        var cardObj = new GameObject("RelicCard");
        cardObj.transform.SetParent(modalObj.transform, false);
        var cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 10f);
        cardRect.sizeDelta = new Vector2(460f, 190f);

        var cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);

        var cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.85f, 0.7f, 0.2f, 0.8f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

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
        _relicIconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _relicIconText.fontSize = 54;
        _relicIconText.alignment = TextAnchor.MiddleCenter;
        _relicIconText.color = Color.white;
        _relicIconText.text = "💎";

        // Tên Cổ Vật
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(cardObj.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(130f, -22f);
        nameRect.sizeDelta = new Vector2(-150f, 34f);

        _relicNameText = nameObj.AddComponent<Text>();
        _relicNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _relicNameText.fontSize = 22;
        _relicNameText.fontStyle = FontStyle.Bold;
        _relicNameText.alignment = TextAnchor.MiddleLeft;
        _relicNameText.color = new Color(1f, 0.88f, 0.35f);
        _relicNameText.text = "RELIC NAME";

        // Level Badge
        var badgeObj = new GameObject("Badge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.anchoredPosition = new Vector2(-20f, -22f);
        badgeRect.sizeDelta = new Vector2(70f, 30f);

        _relicBadgeText = badgeObj.AddComponent<Text>();
        _relicBadgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _relicBadgeText.fontSize = 16;
        _relicBadgeText.fontStyle = FontStyle.Bold;
        _relicBadgeText.alignment = TextAnchor.MiddleRight;
        _relicBadgeText.color = new Color(0.2f, 0.9f, 1f);
        _relicBadgeText.text = "LV.1";

        // Mô tả Cổ Vật
        var descObj = new GameObject("Desc");
        descObj.transform.SetParent(cardObj.transform, false);
        var descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 0f);
        descRect.anchoredPosition = new Vector2(130f, 20f);
        descRect.sizeDelta = new Vector2(-150f, -80f);

        _relicDescText = descObj.AddComponent<Text>();
        _relicDescText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _relicDescText.fontSize = 15;
        _relicDescText.alignment = TextAnchor.UpperLeft;
        _relicDescText.color = new Color(0.85f, 0.9f, 0.95f);
        _relicDescText.text = "Relic Description...";

        // Nút Thu Nhận
        var collectBtnObj = new GameObject("Collect_Button");
        collectBtnObj.transform.SetParent(modalObj.transform, false);
        var btnRect = collectBtnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 25f);
        btnRect.sizeDelta = new Vector2(240f, 48f);

        var btnBg = collectBtnObj.AddComponent<Image>();
        btnBg.color = new Color(0.95f, 0.78f, 0.15f, 1f);

        var btnOutline = collectBtnObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.35f, 0.2f, 0f, 0.95f);
        btnOutline.effectDistance = new Vector2(2f, -2f);

        var btn = collectBtnObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.AddListener(OnCollectChestReward);

        var btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(collectBtnObj.transform, false);
        var bTextRect = btnTextObj.AddComponent<RectTransform>();
        bTextRect.anchorMin = Vector2.zero;
        bTextRect.anchorMax = Vector2.one;
        bTextRect.offsetMin = Vector2.zero;
        bTextRect.offsetMax = Vector2.zero;

        _collectButtonText = btnTextObj.AddComponent<Text>();
        _collectButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _collectButtonText.fontSize = 18;
        _collectButtonText.fontStyle = FontStyle.Bold;
        _collectButtonText.alignment = TextAnchor.MiddleCenter;
        _collectButtonText.color = new Color(0.12f, 0.08f, 0.02f);
        _collectButtonText.text = GameLocalization.CollectButtonText;

        _chestPanel.SetActive(false);
    }

    private void ShowChestReward()
    {
        if (_chestPanel == null) return;
        Time.timeScale = 0f; // Pause game khi nhận rương
        _chestPanel.SetActive(true);

        // Chọn ngẫu nhiên 1 trong 5 Cổ Vật
        _currentChestRelic = (RelicType)UnityEngine.Random.Range(0, 5);
        RenderChestRewardUI();
    }

    private void RenderChestRewardUI()
    {
        if (_chestPanel == null || !_chestPanel.activeSelf) return;

        int currentLvl = 0;
        if (!_playerQuery.IsEmpty)
        {
            Entity p = _playerQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<PlayerRelicData>(p))
            {
                var r = _entityManager.GetComponentData<PlayerRelicData>(p);
                currentLvl = _currentChestRelic switch
                {
                    RelicType.WindrunnerBoots => r.BootsLevel,
                    RelicType.SpiritMagnet => r.MagnetLevel,
                    RelicType.HeartCrystal => r.VitalityLevel,
                    RelicType.PowerCharm => r.PowerLevel,
                    RelicType.FortuneDice => r.FortuneDiceLevel,
                    _ => 0
                };
            }
        }
        int nextLvl = currentLvl + 1;

        switch (_currentChestRelic)
        {
            case RelicType.WindrunnerBoots:
                _relicIconText.text = "👢";
                _relicNameText.text = GameLocalization.RelicBootsTitle;
                _relicBadgeText.text = $"LV.{nextLvl}";
                _relicDescText.text = GameLocalization.RelicBootsDesc(nextLvl);
                break;

            case RelicType.SpiritMagnet:
                _relicIconText.text = "🧲";
                _relicNameText.text = GameLocalization.RelicMagnetTitle;
                _relicBadgeText.text = $"LV.{nextLvl}";
                _relicDescText.text = GameLocalization.RelicMagnetDesc(nextLvl);
                break;

            case RelicType.HeartCrystal:
                _relicIconText.text = "💎";
                _relicNameText.text = GameLocalization.RelicVitalityTitle;
                _relicBadgeText.text = $"LV.{nextLvl}";
                _relicDescText.text = GameLocalization.RelicVitalityDesc(nextLvl);
                break;

            case RelicType.PowerCharm:
                _relicIconText.text = "⚔️";
                _relicNameText.text = GameLocalization.RelicPowerTitle;
                _relicBadgeText.text = $"LV.{nextLvl}";
                _relicDescText.text = GameLocalization.RelicPowerDesc(nextLvl);
                break;

            case RelicType.FortuneDice:
                _relicIconText.text = "🎲";
                _relicNameText.text = GameLocalization.RelicDiceTitle;
                _relicBadgeText.text = $"LV.{nextLvl}";
                _relicDescText.text = GameLocalization.RelicDiceDesc(nextLvl);
                break;
        }
    }

    private void OnCollectChestReward()
    {
        if (_playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        PlayerRelicData relicData = default;
        if (_entityManager.HasComponent<PlayerRelicData>(playerEntity))
        {
            relicData = _entityManager.GetComponentData<PlayerRelicData>(playerEntity);
        }
        else
        {
            relicData = new PlayerRelicData { DamageMultiplier = 1.0f };
            _entityManager.AddComponentData(playerEntity, relicData);
        }

        switch (_currentChestRelic)
        {
            case RelicType.WindrunnerBoots:
                relicData.BootsLevel++;
                if (_entityManager.HasComponent<MovementData>(playerEntity))
                {
                    var mov = _entityManager.GetComponentData<MovementData>(playerEntity);
                    mov.MoveSpeed *= 1.15f;
                    _entityManager.SetComponentData(playerEntity, mov);
                }
                break;

            case RelicType.SpiritMagnet:
                relicData.MagnetLevel++;
                if (_entityManager.HasComponent<PickupRangeData>(playerEntity))
                {
                    var pr = _entityManager.GetComponentData<PickupRangeData>(playerEntity);
                    pr.Range *= 1.50f;
                    _entityManager.SetComponentData(playerEntity, pr);
                }
                break;

            case RelicType.HeartCrystal:
                relicData.VitalityLevel++;
                if (_entityManager.HasComponent<HealthData>(playerEntity))
                {
                    var hp = _entityManager.GetComponentData<HealthData>(playerEntity);
                    hp.MaxHP += 30f;
                    hp.CurrentHP = hp.MaxHP; // Hồi đầy 100% máu
                    _entityManager.SetComponentData(playerEntity, hp);
                }
                break;

            case RelicType.PowerCharm:
                relicData.PowerLevel++;
                relicData.DamageMultiplier *= 1.15f;
                ScaleAllSkillsDamage(1.15f);
                break;

            case RelicType.FortuneDice:
                relicData.FortuneDiceLevel++;
                _rerollsRemaining += 2;
                UpdateRerollButtonUI();
                break;
        }

        _entityManager.SetComponentData(playerEntity, relicData);

        if (_chestPanel != null) _chestPanel.SetActive(false);

        // Nếu có Level-Up đang chờ thì mở bảng Level-Up, ngược lại unpause game
        if (_pendingLevelUps > 0)
        {
            ShowNextLevelUpReward();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    private void ScaleAllSkillsDamage(float mult)
    {
        if (!_projectileSkillQuery.IsEmpty)
        {
            var e = _projectileSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<ProjectileSkillData>(e))
            {
                var data = _entityManager.GetComponentData<ProjectileSkillData>(e);
                data.ProjectileDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_flyingSwordSkillQuery.IsEmpty)
        {
            var e = _flyingSwordSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<FlyingSwordSkillData>(e))
            {
                var data = _entityManager.GetComponentData<FlyingSwordSkillData>(e);
                data.BaseDamage *= mult;
                data.AoeDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_slashSwordSkillQuery.IsEmpty)
        {
            var e = _slashSwordSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<SlashSwordSkillData>(e))
            {
                var data = _entityManager.GetComponentData<SlashSwordSkillData>(e);
                data.BaseDamage *= mult;
                data.WaveDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_fireballSkillQuery.IsEmpty)
        {
            var e = _fireballSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<FireballSkillData>(e))
            {
                var data = _entityManager.GetComponentData<FireballSkillData>(e);
                data.BaseDamage *= mult;
                data.FireZoneDamagePerTick *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
        if (!_swordFusionSkillQuery.IsEmpty)
        {
            var e = _swordFusionSkillQuery.GetSingletonEntity();
            if (_entityManager.HasComponent<SwordFusionSkillData>(e))
            {
                var data = _entityManager.GetComponentData<SwordFusionSkillData>(e);
                data.ImpactDamage *= mult;
                data.WaveDamage *= mult;
                _entityManager.SetComponentData(e, data);
            }
        }
    }
}
