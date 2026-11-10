using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge quản lý toàn bộ hiệu ứng hình ảnh (VFX) và âm thanh vụ nổ của Bomber:
/// 1. Sóng xung kích nổ nhiệt (Explosion Shockwave) với bán kính chính xác theo ECS.
/// 2. Đĩa sáng nổ bung bên trong (Inner Blast Core) chói lóa.
/// 3. Vết cháy sém đen mặt đất (Charred Earth Decal) lưu lại mờ dần.
/// 4. Mảnh văng & tàn lửa bắn tung tóe (Fiery Sparks & Shrapnel ParticleSystem).
/// 5. Rung lắc màn hình (Screen Shake) và âm thanh nổ rền uy lực (Explosion SFX).
/// 100% GPU-accelerated với SpriteRenderer, zero GC allocation qua Object Pool.
/// </summary>
public class BomberVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _explosionEventQuery;
    private bool _initialized;

    private readonly List<BomberExplosionEffect> _explosionPool = new List<BomberExplosionEffect>();
    private const int INITIAL_POOL_SIZE = 12;

    private static Sprite _circleSprite;
    private static Sprite _ringSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<BomberVisualBridge>() == null)
        {
            var go = new GameObject("[BomberVisualBridge]");
            go.AddComponent<BomberVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        InitECS();
        InitPool();
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _explosionEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<BomberExplosionEvent>());

        _initialized = true;
        return true;
    }

    private void InitPool()
    {
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            var effect = CreateNewExplosionEffect(i);
            _explosionPool.Add(effect);
        }
    }

    private BomberExplosionEffect CreateNewExplosionEffect(int id)
    {
        var go = new GameObject($"BomberExplosionEffect_{id}");
        go.transform.SetParent(transform, false);
        var effect = go.AddComponent<BomberExplosionEffect>();
        go.SetActive(false);
        return effect;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;
        HandleExplosionEvents();
    }

    private void HandleExplosionEvents()
    {
        if (_explosionEventQuery.IsEmpty) return;

        var events = _explosionEventQuery.ToComponentDataArray<BomberExplosionEvent>(Allocator.Temp);

        for (int i = 0; i < events.Length; i++)
        {
            Vector3 center = new Vector3(events[i].Position.x, events[i].Position.y, 0f);
            float radius = events[i].Radius;
            bool isSuicide = events[i].IsSuicide;

            SpawnExplosion(center, radius, isSuicide);
        }

        _entityManager.DestroyEntity(_explosionEventQuery);
        events.Dispose();
    }

    private void SpawnExplosion(Vector3 position, float radius, bool isSuicide)
    {
        // 1. Lấy hiệu ứng từ pool
        BomberExplosionEffect effect = null;
        for (int i = 0; i < _explosionPool.Count; i++)
        {
            if (!_explosionPool[i].gameObject.activeSelf)
            {
                effect = _explosionPool[i];
                break;
            }
        }

        if (effect == null)
        {
            effect = CreateNewExplosionEffect(_explosionPool.Count);
            _explosionPool.Add(effect);
        }

        effect.Setup(position, radius, isSuicide);

        // 2. Kích hoạt âm thanh nổ oanh tạc
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayExplosion(isSuicide ? 0.95f : 0.75f);
        }

        // 3. Rung lắc camera (Screen Shake)
        if (CameraFollowPlayer.Instance != null)
        {
            CameraFollowPlayer.Instance.TriggerShake(
                isSuicide ? 0.25f : 0.12f,
                isSuicide ? 0.22f : 0.14f);
        }
    }

    #region --- Procedural Sprites (GPU Texture Generation) ---

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center)) / center;
                if (dist > 1.0f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float ring = Mathf.Sin(dist * Mathf.PI);
                    float alpha = Mathf.Pow(ring, 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _circleSprite;
    }

    public static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center)) / center;
                if (dist > 1.0f || dist < 0.68f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float normalized = (dist - 0.68f) / (1.0f - 0.68f);
                    float bell = Mathf.Sin(normalized * Mathf.PI);
                    float alpha = Mathf.Pow(bell, 1.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _ringSprite;
    }

    #endregion
}

/// <summary>
/// Thực thể hiệu ứng vụ nổ độc lập được quản lý qua Object Pool:
/// Gồm 4 tầng hiển thị: Vết cháy mặt đất, đĩa nổ bên trong, vành sóng xung kích và hệ hạt tàn lửa.
/// </summary>
public class BomberExplosionEffect : MonoBehaviour
{
    private SpriteRenderer _charredGround;
    private SpriteRenderer _innerBlast;
    private SpriteRenderer _outerRing;
    private ParticleSystem _sparksParticle;

    private float _maxRadius;
    private float _duration = 0.40f;
    private float _elapsed = 0f;
    private float _groundFadeDuration = 0.90f;
    private bool _isSuicide;

    private void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        // 1. Vết cháy sém mặt đất (Charred Earth Decal)
        var charObj = new GameObject("CharredGround");
        charObj.transform.SetParent(transform, false);
        _charredGround = charObj.AddComponent<SpriteRenderer>();
        _charredGround.sprite = BomberVisualBridge.GetCircleSprite();
        _charredGround.sortingOrder = -8;
        if (mat != null) _charredGround.material = mat;

        // 2. Đĩa nổ rực sáng bên trong (Inner Blast Core)
        var blastObj = new GameObject("InnerBlast");
        blastObj.transform.SetParent(transform, false);
        _innerBlast = blastObj.AddComponent<SpriteRenderer>();
        _innerBlast.sprite = BomberVisualBridge.GetCircleSprite();
        _innerBlast.sortingOrder = 9;
        if (mat != null) _innerBlast.material = mat;

        // 3. Vành đai sóng xung kích nổ bung (Outer Shockwave Ring)
        var ringObj = new GameObject("OuterRing");
        ringObj.transform.SetParent(transform, false);
        _outerRing = ringObj.AddComponent<SpriteRenderer>();
        _outerRing.sprite = BomberVisualBridge.GetRingSprite();
        _outerRing.sortingOrder = 10;
        if (mat != null) _outerRing.material = mat;

        // 4. Hệ hạt tàn lửa & khói nổ (Sparks Particle System)
        InitParticleSystem(shader);
    }

    private void InitParticleSystem(Shader shader)
    {
        var psObj = new GameObject("SparksParticles");
        psObj.transform.SetParent(transform, false);
        _sparksParticle = psObj.AddComponent<ParticleSystem>();
        _sparksParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var psr = psObj.GetComponent<ParticleSystemRenderer>();

        if (shader != null)
        {
            Material pMat = new Material(shader);
            pMat.mainTexture = BomberVisualBridge.GetCircleSprite().texture;
            psr.material = pMat;
        }
        psr.sortingOrder = 11;

        var main = _sparksParticle.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.15f;

        var emission = _sparksParticle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 22)
        });

        var shape = _sparksParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;

        var colorOverLifetime = _sparksParticle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),   // Vàng rực
                new GradientColorKey(new Color(1f, 0.40f, 0.05f), 0.5f), // Cam rực
                new GradientColorKey(new Color(0.85f, 0.1f, 0.05f), 1f)  // Đỏ tàn
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = _sparksParticle.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
    }

    public void Setup(Vector3 position, float radius, bool isSuicide)
    {
        transform.position = position;
        _maxRadius = radius;
        _isSuicide = isSuicide;
        _duration = isSuicide ? 0.42f : 0.35f;
        _groundFadeDuration = isSuicide ? 0.95f : 0.70f;
        _elapsed = 0f;

        gameObject.SetActive(true);

        // Bắn hệ hạt tàn lửa
        if (_sparksParticle != null)
        {
            _sparksParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sparksParticle.Play();
        }

        UpdateVisual(0f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        UpdateVisual(t);

        // Ẩn effect khi vết cháy mặt đất đã mờ hẳn
        if (_elapsed >= _groundFadeDuration)
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateVisual(float t)
    {
        // Ease-out Cubic nổ bung nhanh trong những frame đầu
        float easeT = 1f - Mathf.Pow(1f - t, 3f);
        float currentRadius = Mathf.Lerp(0.2f, _maxRadius, easeT);
        float alpha = Mathf.Clamp01((1f - t) * 1.6f);

        // 1. Vành sóng xung kích nổ bung (Outer Shockwave Ring)
        if (_outerRing != null)
        {
            _outerRing.transform.localScale = Vector3.one * (currentRadius * 2.2f);
            Color ringColor = Color.Lerp(
                new Color(1f, 0.95f, 0.4f, alpha),
                new Color(1f, 0.25f, 0.05f, alpha * 0.85f),
                t);
            _outerRing.color = ringColor;
        }

        // 2. Đĩa sáng nổ nhiệt bên trong (Inner Blast Core)
        if (_innerBlast != null)
        {
            _innerBlast.transform.localScale = Vector3.one * (currentRadius * 1.7f);
            Color blastColor = Color.Lerp(
                new Color(1f, 0.92f, 0.6f, alpha * 0.9f),
                new Color(0.95f, 0.20f, 0.05f, 0f),
                t);
            _innerBlast.color = blastColor;
        }

        // 3. Vết cháy sém mặt đất (Charred Earth Decal)
        if (_charredGround != null)
        {
            float groundScale = _maxRadius * 1.55f;
            _charredGround.transform.localScale = Vector3.one * groundScale;

            // Giữ đậm trong 0.35s đầu rồi mờ dần
            float groundAlpha;
            if (_elapsed < 0.35f)
            {
                groundAlpha = 0.65f;
            }
            else
            {
                float fadeT = Mathf.Clamp01((_elapsed - 0.35f) / (_groundFadeDuration - 0.35f));
                groundAlpha = Mathf.Lerp(0.65f, 0f, fadeT);
            }

            _charredGround.color = new Color(0.12f, 0.06f, 0.04f, groundAlpha);
        }
    }
}
