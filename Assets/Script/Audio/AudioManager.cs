using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Các bản nhạc nền trong game.
/// </summary>
public enum BGMTrack
{
    None = 0,
    MainMenu = 1,
    BattleNormal = 2,
    MegaWave = 3,
    GameOver = 4
}

/// <summary>
/// Các hiệu ứng âm thanh Gameplay (Pickups, Events, Player Feedback).
/// </summary>
public enum SFXType
{
    ExpOrb = 0,
    HealthPickup = 1,
    MagnetPickup = 2,
    ChestOpen = 3,
    MegaWaveHorn = 4,
    PlayerHurt = 5,
    Heartbeat = 6,
    Explosion = 7
}

/// <summary>
/// Các hiệu ứng âm thanh Giao diện & Hệ thống.
/// </summary>
public enum UIType
{
    Click = 0,
    Hover = 1,
    LevelUp = 2,
    PerkSelect = 3,
    Reroll = 4
}

/// <summary>
/// Hệ thống điều phối âm thanh trung tâm (Central Audio Manager).
/// Quản lý Pool AudioSource, BGM Crossfade, Volume Persistence, EXP Pitch Escalation,
/// và hỗ trợ fallback Procedural Audio DSP khi chưa có file âm thanh bên ngoài.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string PREF_MASTER_VOL = "HB_MasterVolume";
    private const string PREF_BGM_VOL = "HB_BGMVolume";
    private const string PREF_SFX_VOL = "HB_SFXVolume";
    private const string PREF_IS_MUTED = "HB_IsMuted";

    private const int SFX_POOL_SIZE = 20;

    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var existing = FindAnyObjectByType<AudioManager>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    var go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Custom BGM Clips (Tùy chọn gán từ Inspector)")]
    public AudioClip CustomBGM_MainMenu;
    public AudioClip CustomBGM_Battle;
    public AudioClip CustomBGM_MegaWave;
    public AudioClip CustomBGM_GameOver;

    [Header("Custom SFX Clips (Tùy chọn)")]
    public AudioClip CustomSFX_ExpOrb;
    public AudioClip CustomSFX_HealthPickup;
    public AudioClip CustomSFX_MagnetPickup;
    public AudioClip CustomSFX_ChestOpen;
    public AudioClip CustomSFX_MegaWaveHorn;
    public AudioClip CustomSFX_PlayerHurt;
    public AudioClip CustomSFX_Heartbeat;
    public AudioClip CustomSFX_Explosion;

    [Header("Custom UI Clips (Tùy chọn)")]
    public AudioClip CustomUI_Click;
    public AudioClip CustomUI_Hover;
    public AudioClip CustomUI_LevelUp;
    public AudioClip CustomUI_PerkSelect;
    public AudioClip CustomUI_Reroll;

    // Cache procedural clips
    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

    // AudioSources
    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private bool _isUsingSourceA = true;
    private Coroutine _crossfadeRoutine;

    private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
    private int _sfxPoolIndex = 0;

    // Volume settings (0.0f - 1.0f)
    public float MasterVolume { get; private set; } = 0.8f;
    public float BGMVolume { get; private set; } = 0.7f;
    public float SFXVolume { get; private set; } = 0.9f;
    public bool IsMuted { get; private set; } = false;

    public event Action OnVolumeChanged;

    // Current BGM State
    public BGMTrack CurrentBGM { get; private set; } = BGMTrack.None;

    // Debounce & Rate Limiting
    private readonly Dictionary<int, float> _cooldownTimers = new Dictionary<int, float>();

    // EXP Pitch Stacking
    private float _lastExpPickupTime = -10f;
    private int _expCombo = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        InitializeAudioSources();
    }

    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.8f);
        BGMVolume = PlayerPrefs.GetFloat(PREF_BGM_VOL, 0.7f);
        SFXVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.9f);
        IsMuted = PlayerPrefs.GetInt(PREF_IS_MUTED, 0) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, MasterVolume);
        PlayerPrefs.SetFloat(PREF_BGM_VOL, BGMVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOL, SFXVolume);
        PlayerPrefs.SetInt(PREF_IS_MUTED, IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void InitializeAudioSources()
    {
        // 2 BGM sources for crossfading
        var bgmRootA = new GameObject("BGM_Source_A");
        bgmRootA.transform.SetParent(transform);
        _bgmSourceA = bgmRootA.AddComponent<AudioSource>();
        _bgmSourceA.loop = true;
        _bgmSourceA.playOnAwake = false;

        var bgmRootB = new GameObject("BGM_Source_B");
        bgmRootB.transform.SetParent(transform);
        _bgmSourceB = bgmRootB.AddComponent<AudioSource>();
        _bgmSourceB.loop = true;
        _bgmSourceB.playOnAwake = false;

        // SFX Pool
        var sfxRoot = new GameObject("SFX_Pool");
        sfxRoot.transform.SetParent(transform);
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var sfxObj = new GameObject($"SFX_Source_{i}");
            sfxObj.transform.SetParent(sfxRoot.transform);
            var src = sfxObj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            _sfxPool.Add(src);
        }

        ApplyBGMVolume();
    }

    #region Volume API

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        SaveSettings();
        ApplyBGMVolume();
        OnVolumeChanged?.Invoke();
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        SaveSettings();
        ApplyBGMVolume();
        OnVolumeChanged?.Invoke();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        SaveSettings();
        OnVolumeChanged?.Invoke();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        SaveSettings();
        ApplyBGMVolume();
        OnVolumeChanged?.Invoke();
    }

    public void SetMute(bool muted)
    {
        IsMuted = muted;
        SaveSettings();
        ApplyBGMVolume();
        OnVolumeChanged?.Invoke();
    }

    private float GetEffectiveBGMVolume()
    {
        return IsMuted ? 0f : (MasterVolume * BGMVolume);
    }

    private float GetEffectiveSFXVolume()
    {
        return IsMuted ? 0f : (MasterVolume * SFXVolume);
    }

    private void ApplyBGMVolume()
    {
        float targetVol = GetEffectiveBGMVolume();
        if (_crossfadeRoutine == null)
        {
            var activeSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
            if (activeSource != null) activeSource.volume = targetVol;
            var inactiveSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;
            if (inactiveSource != null) inactiveSource.volume = 0f;
        }
    }

    #endregion

    #region BGM Management & Crossfading

    public void PlayBGM(BGMTrack track, float crossfadeDuration = 1.5f)
    {
        if (track == CurrentBGM && CurrentBGM != BGMTrack.None) return;

        CurrentBGM = track;
        AudioClip clip = GetBGMClip(track);
        if (clip == null)
        {
            StopBGM(crossfadeDuration);
            return;
        }

        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
        }

        _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(clip, crossfadeDuration));
    }

    public void StopBGM(float fadeOutDuration = 1.0f)
    {
        CurrentBGM = BGMTrack.None;
        if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
        _crossfadeRoutine = StartCoroutine(FadeOutRoutine(fadeOutDuration));
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip, float duration)
    {
        var activeSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
        var newSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;
        _isUsingSourceA = !_isUsingSourceA;

        newSource.clip = newClip;
        newSource.volume = 0f;
        newSource.Play();

        float targetVol = GetEffectiveBGMVolume();
        float startActiveVol = activeSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            targetVol = GetEffectiveBGMVolume();
            newSource.volume = Mathf.Lerp(0f, targetVol, t);
            activeSource.volume = Mathf.Lerp(startActiveVol, 0f, t);

            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        newSource.volume = GetEffectiveBGMVolume();
        _crossfadeRoutine = null;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        var activeSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
        float startVol = activeSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            activeSource.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        _crossfadeRoutine = null;
    }

    #endregion

    #region SFX & UI Playback

    /// <summary>
    /// Phát hiệu ứng âm thanh Gameplay (Pickups, Chest, Player Hurt, Horn).
    /// </summary>
    public void PlaySFX(SFXType type, float volumeMultiplier = 1.0f, float pitch = 1.0f)
    {
        // Rate limit: Không phát lặp cùng 1 SFX trong 30ms
        int key = (int)type + 1000;
        if (_cooldownTimers.TryGetValue(key, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < 0.035f) return;
        }
        _cooldownTimers[key] = Time.unscaledTime;

        AudioClip clip = GetSFXClip(type);
        PlayPooledSound(clip, volumeMultiplier, pitch);
    }

    /// <summary>
    /// Phát hiệu ứng âm thanh Giao diện & Hệ thống (Click, Hover, Level Up, Perk, Reroll).
    /// </summary>
    public void PlayUI(UIType type)
    {
        // Rate limit: Hover/Click có cooldown 25ms để tránh spam
        int key = (int)type + 2000;
        if (_cooldownTimers.TryGetValue(key, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < 0.025f) return;
        }
        _cooldownTimers[key] = Time.unscaledTime;

        AudioClip clip = GetUIClip(type);
        PlayPooledSound(clip, 1.0f, 1.0f);
    }

    /// <summary>
    /// Cơ chế Nhặt Ngọc EXP: Thăng hoa cao độ liên hoàn (Pitch Escalation Stacking).
    /// Mỗi viên nhặt liên tiếp trong 0.85s sẽ tăng nửa cung (từ 1.0 đến 1.7), mang lại cảm giác cực sướng tai.
    /// </summary>
    public void PlayExpPickup()
    {
        float now = Time.unscaledTime;
        if (now - _lastExpPickupTime < 0.85f)
        {
            _expCombo = Mathf.Min(14, _expCombo + 1);
        }
        else
        {
            _expCombo = 0;
        }
        _lastExpPickupTime = now;

        // Thang âm: Tăng dần từ 1.0f đến 1.7f theo từng nốt
        float pitch = 1.0f + _expCombo * 0.05f;

        // Rate limit: Tối thiểu 20ms giữa 2 tiếng ngọc
        int key = (int)SFXType.ExpOrb + 1000;
        if (_cooldownTimers.TryGetValue(key, out float lastTime))
        {
            if (now - lastTime < 0.02f) return;
        }
        _cooldownTimers[key] = now;

        AudioClip clip = GetSFXClip(SFXType.ExpOrb);
        PlayPooledSound(clip, 0.9f, pitch);
    }

    /// <summary>
    /// Phát âm thanh vụ nổ oanh tạc (Explosion SFX) với độ lệch pitch nhẹ tự nhiên.
    /// </summary>
    public void PlayExplosion(float volume = 0.85f)
    {
        float pitch = UnityEngine.Random.Range(0.92f, 1.08f);
        PlaySFX(SFXType.Explosion, volume, pitch);
    }

    private void PlayPooledSound(AudioClip clip, float volumeMultiplier, float pitch)
    {
        if (clip == null || IsMuted) return;

        float effectiveVol = GetEffectiveSFXVolume() * volumeMultiplier;
        if (effectiveVol <= 0.001f) return;

        AudioSource src = GetNextPooledSource();
        if (src == null) return;

        src.pitch = pitch;
        src.PlayOneShot(clip, effectiveVol);
    }

    private AudioSource GetNextPooledSource()
    {
        if (_sfxPool.Count == 0) return null;

        // Tìm source đang rảnh
        for (int i = 0; i < _sfxPool.Count; i++)
        {
            int idx = (_sfxPoolIndex + i) % _sfxPool.Count;
            if (!_sfxPool[idx].isPlaying)
            {
                _sfxPoolIndex = (idx + 1) % _sfxPool.Count;
                return _sfxPool[idx];
            }
        }

        // Nếu tất cả đang bận, dùng vòng lặp tròn (Round-Robin Voice Stealing)
        var stolen = _sfxPool[_sfxPoolIndex];
        _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Count;
        return stolen;
    }

    #endregion

    #region Clip Resolvers (Custom or Procedural Fallback)

    private AudioClip GetBGMClip(BGMTrack track)
    {
        switch (track)
        {
            case BGMTrack.MainMenu:
                if (CustomBGM_MainMenu != null) return CustomBGM_MainMenu;
                return GetOrCreateCachedClip("Proc_BGM_MainMenu", ProceduralAudioClips.CreateMainMenuBGM);

            case BGMTrack.BattleNormal:
                if (CustomBGM_Battle != null) return CustomBGM_Battle;
                return GetOrCreateCachedClip("Proc_BGM_Battle", ProceduralAudioClips.CreateBattleBGM);

            case BGMTrack.MegaWave:
                if (CustomBGM_MegaWave != null) return CustomBGM_MegaWave;
                return GetOrCreateCachedClip("Proc_BGM_MegaWave", ProceduralAudioClips.CreateMegaWaveBGM);

            case BGMTrack.GameOver:
                if (CustomBGM_GameOver != null) return CustomBGM_GameOver;
                return GetOrCreateCachedClip("Proc_BGM_GameOver", ProceduralAudioClips.CreateGameOverBGM);

            default:
                return null;
        }
    }

    private AudioClip GetSFXClip(SFXType type)
    {
        switch (type)
        {
            case SFXType.ExpOrb:
                if (CustomSFX_ExpOrb != null) return CustomSFX_ExpOrb;
                return GetOrCreateCachedClip("Proc_SFX_ExpOrb", ProceduralAudioClips.CreateExpOrb);

            case SFXType.HealthPickup:
                if (CustomSFX_HealthPickup != null) return CustomSFX_HealthPickup;
                return GetOrCreateCachedClip("Proc_SFX_HealthPickup", ProceduralAudioClips.CreateHealthPickup);

            case SFXType.MagnetPickup:
                if (CustomSFX_MagnetPickup != null) return CustomSFX_MagnetPickup;
                return GetOrCreateCachedClip("Proc_SFX_MagnetPickup", ProceduralAudioClips.CreateMagnetPickup);

            case SFXType.ChestOpen:
                if (CustomSFX_ChestOpen != null) return CustomSFX_ChestOpen;
                return GetOrCreateCachedClip("Proc_SFX_ChestOpen", ProceduralAudioClips.CreateChestOpen);

            case SFXType.MegaWaveHorn:
                if (CustomSFX_MegaWaveHorn != null) return CustomSFX_MegaWaveHorn;
                return GetOrCreateCachedClip("Proc_SFX_MegaWaveHorn", ProceduralAudioClips.CreateMegaWaveHorn);

            case SFXType.PlayerHurt:
                if (CustomSFX_PlayerHurt != null) return CustomSFX_PlayerHurt;
                return GetOrCreateCachedClip("Proc_SFX_PlayerHurt", ProceduralAudioClips.CreatePlayerHurt);

            case SFXType.Heartbeat:
                if (CustomSFX_Heartbeat != null) return CustomSFX_Heartbeat;
                return GetOrCreateCachedClip("Proc_SFX_Heartbeat", ProceduralAudioClips.CreateHeartbeat);

            case SFXType.Explosion:
                if (CustomSFX_Explosion != null) return CustomSFX_Explosion;
                return GetOrCreateCachedClip("Proc_SFX_Explosion", ProceduralAudioClips.CreateExplosion);

            default:
                return null;
        }
    }

    private AudioClip GetUIClip(UIType type)
    {
        switch (type)
        {
            case UIType.Click:
                if (CustomUI_Click != null) return CustomUI_Click;
                return GetOrCreateCachedClip("Proc_UI_Click", ProceduralAudioClips.CreateUIClick);

            case UIType.Hover:
                if (CustomUI_Hover != null) return CustomUI_Hover;
                return GetOrCreateCachedClip("Proc_UI_Hover", ProceduralAudioClips.CreateUIHover);

            case UIType.LevelUp:
                if (CustomUI_LevelUp != null) return CustomUI_LevelUp;
                return GetOrCreateCachedClip("Proc_UI_LevelUp", ProceduralAudioClips.CreateUILevelUp);

            case UIType.PerkSelect:
                if (CustomUI_PerkSelect != null) return CustomUI_PerkSelect;
                return GetOrCreateCachedClip("Proc_UI_PerkSelect", ProceduralAudioClips.CreateUIPerkSelect);

            case UIType.Reroll:
                if (CustomUI_Reroll != null) return CustomUI_Reroll;
                return GetOrCreateCachedClip("Proc_UI_Reroll", ProceduralAudioClips.CreateUIReroll);

            default:
                return null;
        }
    }

    private AudioClip GetOrCreateCachedClip(string key, Func<AudioClip> generator)
    {
        if (_clipCache.TryGetValue(key, out var clip) && clip != null)
        {
            return clip;
        }

        AudioClip newClip = generator();
        _clipCache[key] = newClip;
        return newClip;
    }

    #endregion
}
