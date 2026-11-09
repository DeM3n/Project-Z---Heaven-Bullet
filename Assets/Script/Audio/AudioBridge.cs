using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cầu nối âm thanh Hybrid ECS -> Managed AudioManager.
/// Tự động lắng nghe trạng thái trận đấu từ ECS unmanaged data:
/// - EXP Orbs absorbed -> Kích hoạt Pitch Escalation Stacking.
/// - Player damaged -> Kích hoạt PlayerHurt SFX.
/// - Low HP (<25%) -> Kích hoạt Heartbeat âm thanh.
/// - Wave State (Normal / MegaWave) -> Tù và chiến trận & chuyển đổi BGM tương ứng.
/// - Player Death -> Chuyển sang GameOver BGM.
/// </summary>
public class AudioBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private EntityQuery _waveQuery;
    private bool _initialized;

    private WaveState _lastWaveState = WaveState.NormalTimeline;
    private float _heartbeatTimer;
    private bool _isGameOverBgmPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<AudioBridge>() == null)
        {
            var go = new GameObject("[AudioBridge]");
            go.AddComponent<AudioBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        InitECS();
        CheckSceneBGM(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _initialized = false;
        _isGameOverBgmPlaying = false;
        _lastWaveState = WaveState.NormalTimeline;
        InitECS();
        CheckSceneBGM(scene.name);
    }

    private void CheckSceneBGM(string sceneName)
    {
        if (sceneName.Contains("MainMenu"))
        {
            AudioManager.Instance.PlayBGM(BGMTrack.MainMenu, 1.5f);
        }
        else
        {
            // Trong scene trận đấu
            AudioManager.Instance.PlayBGM(BGMTrack.BattleNormal, 1.5f);
        }
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<HealthData>());

        _waveQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<WaveManagerData>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        UpdatePlayerAudio();
        UpdateWaveMusic();
    }

    private void UpdatePlayerAudio()
    {
        if (_playerQuery.IsEmpty) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();

        // 1. Kiểm tra trạng thái chết
        if (_entityManager.HasComponent<PlayerDeadTag>(playerEntity))
        {
            if (!_isGameOverBgmPlaying)
            {
                _isGameOverBgmPlaying = true;
                AudioManager.Instance.PlayBGM(BGMTrack.GameOver, 1.0f);
            }
            return;
        }

        // 2. Đảm bảo Player có PlayerAudioFeedbackData
        if (!_entityManager.HasComponent<PlayerAudioFeedbackData>(playerEntity))
        {
            _entityManager.AddComponentData(playerEntity, new PlayerAudioFeedbackData());
            return;
        }

        var audioFeedback = _entityManager.GetComponentData<PlayerAudioFeedbackData>(playerEntity);
        bool modified = false;

        // 3. Phản hồi Nhặt Ngọc EXP (Pitch Escalation)
        if (audioFeedback.ExpOrbsAbsorbed > 0)
        {
            for (int i = 0; i < audioFeedback.ExpOrbsAbsorbed; i++)
            {
                AudioManager.Instance.PlayExpPickup();
            }
            audioFeedback.ExpOrbsAbsorbed = 0;
            modified = true;
        }

        // 4. Phản hồi nhận sát thương (Player Hurt)
        if (audioFeedback.TookDamage)
        {
            AudioManager.Instance.PlaySFX(SFXType.PlayerHurt);
            audioFeedback.TookDamage = false;
            modified = true;
        }

        if (modified)
        {
            _entityManager.SetComponentData(playerEntity, audioFeedback);
        }

        // 5. Cảnh báo Máu Yếu (Heartbeat Low HP < 25%)
        var health = _entityManager.GetComponentData<HealthData>(playerEntity);
        if (health.CurrentHP > 0f && health.CurrentHP <= health.MaxHP * 0.25f)
        {
            _heartbeatTimer -= Time.unscaledDeltaTime;
            if (_heartbeatTimer <= 0f)
            {
                AudioManager.Instance.PlaySFX(SFXType.Heartbeat, 0.8f);
                _heartbeatTimer = 0.85f; // Chu kỳ nhịp tim 0.85s
            }
        }
        else
        {
            _heartbeatTimer = 0f;
        }
    }

    private void UpdateWaveMusic()
    {
        if (_waveQuery.IsEmpty || _isGameOverBgmPlaying) return;

        var waveData = _waveQuery.GetSingleton<WaveManagerData>();

        if (waveData.State != _lastWaveState)
        {
            if (waveData.State == WaveState.MegaWaveActive)
            {
                // Bắt đầu đợt Mega Wave sinh tồn 60s
                AudioManager.Instance.PlaySFX(SFXType.MegaWaveHorn, 1.0f);
                AudioManager.Instance.PlayBGM(BGMTrack.MegaWave, 1.2f);
            }
            else if (waveData.State == WaveState.NormalTimeline && _lastWaveState == WaveState.MegaWaveActive)
            {
                // Kết thúc đợt Mega Wave, quay lại nhạc chiến đấu thường
                AudioManager.Instance.PlayBGM(BGMTrack.BattleNormal, 1.8f);
            }

            _lastWaveState = waveData.State;
        }
    }
}
