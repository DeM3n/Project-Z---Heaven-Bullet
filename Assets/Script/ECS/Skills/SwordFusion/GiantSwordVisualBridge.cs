using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge cho kỹ năng Hợp Thể (Sword Fusion).
/// Lắng nghe sự kiện cắm đất của Kiếm Khổng Lồ từ ECS để tạo hiệu ứng Sóng Xung Kích Hoàng Kim (Golden Shockwave)
/// và độ rung chấn động địa chấn cực kỳ mãn nhãn.
/// </summary>
public class GiantSwordVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _impactQuery;
    private bool _initialized;

    private readonly List<GiantShockwaveEffect> _shockwavePool = new List<GiantShockwaveEffect>();
    private static Sprite _circleSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<GiantSwordVisualBridge>() == null)
        {
            var go = new GameObject("[GiantSwordVisualBridge]");
            go.AddComponent<GiantSwordVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _impactQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GiantSwordImpactEvent>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;
        if (_impactQuery.IsEmpty) return;

        var impactEntities = _impactQuery.ToEntityArray(Allocator.Temp);
        var impactDataArray = _impactQuery.ToComponentDataArray<GiantSwordImpactEvent>(Allocator.Temp);

        for (int i = 0; i < impactDataArray.Length; i++)
        {
            Vector3 center = new Vector3(impactDataArray[i].Position.x, impactDataArray[i].Position.y, 0f);
            float radius = impactDataArray[i].Radius;
            SpawnShockwaveHitbox(center, radius);

            // Gỡ bỏ component event để không sinh lặp lại
            if (_entityManager.Exists(impactEntities[i]))
            {
                _entityManager.RemoveComponent<GiantSwordImpactEvent>(impactEntities[i]);
            }
        }

        impactEntities.Dispose();
        impactDataArray.Dispose();
    }

    private void SpawnShockwaveHitbox(Vector3 position, float radius)
    {
        GiantShockwaveEffect shockwave = null;
        for (int i = 0; i < _shockwavePool.Count; i++)
        {
            if (!_shockwavePool[i].gameObject.activeSelf)
            {
                shockwave = _shockwavePool[i];
                break;
            }
        }

        if (shockwave == null)
        {
            var go = new GameObject($"GiantShockwave_{_shockwavePool.Count}");
            go.transform.SetParent(transform);
            shockwave = go.AddComponent<GiantShockwaveEffect>();
            _shockwavePool.Add(shockwave);
        }

        shockwave.Setup(position, radius, 0.55f);
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }
        tex.Apply();

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64);
        return _circleSprite;
    }
}

/// <summary>
/// Hiệu ứng Sóng Xung Kích Hoàng Kim khi Kiếm Khổng Lồ cắm ngập đất
/// </summary>
public class GiantShockwaveEffect : MonoBehaviour
{
    private LineRenderer _outerRing;
    private SpriteRenderer _innerFlash;
    private float _maxRadius;
    private float _duration = 0.55f;
    private float _elapsed = 0f;
    private const int SEGMENTS = 64;

    private void Awake()
    {
        var defaultShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                           ?? Shader.Find("Sprites/Default");
        Material sharedMat = defaultShader != null ? new Material(defaultShader) : null;

        _outerRing = gameObject.AddComponent<LineRenderer>();
        _outerRing.useWorldSpace = false;
        _outerRing.positionCount = SEGMENTS + 1;
        _outerRing.loop = true;
        _outerRing.sortingOrder = 6;
        if (sharedMat != null) _outerRing.material = sharedMat;

        var flashObj = new GameObject("InnerFlash");
        flashObj.transform.SetParent(transform, false);
        _innerFlash = flashObj.AddComponent<SpriteRenderer>();
        _innerFlash.sprite = GiantSwordVisualBridge.GetCircleSprite();
        _innerFlash.sortingOrder = 5;
        if (sharedMat != null) _innerFlash.material = sharedMat;
    }

    public void Setup(Vector3 position, float radius, float duration = 0.55f)
    {
        transform.position = position;
        _maxRadius = radius;
        _duration = duration;
        _elapsed = 0f;
        gameObject.SetActive(true);
        UpdateVisual(0f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        UpdateVisual(t);

        if (t >= 1f)
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateVisual(float t)
    {
        float easeT = 1f - Mathf.Pow(1f - t, 3f);
        float currentRadius = Mathf.Lerp(0.5f, _maxRadius, easeT);
        float alpha = Mathf.Clamp01((1f - t) * 1.5f);

        if (_outerRing != null)
        {
            _outerRing.widthMultiplier = Mathf.Lerp(0.28f, 0.03f, t);
            Color ringColor = Color.Lerp(
                new Color(1f, 0.95f, 0.4f, alpha), // Vàng kim rực rỡ
                new Color(1f, 0.55f, 0.1f, alpha * 0.7f), // Cam rực lửa hoàng gia
                t);
            _outerRing.startColor = ringColor;
            _outerRing.endColor = ringColor;

            for (int i = 0; i <= SEGMENTS; i++)
            {
                float angle = (i / (float)SEGMENTS) * Mathf.PI * 2f;
                Vector3 pt = new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f);
                _outerRing.SetPosition(i, pt);
            }
        }

        if (_innerFlash != null)
        {
            _innerFlash.transform.localScale = Vector3.one * (currentRadius * 2f);
            _innerFlash.color = new Color(1f, 0.85f, 0.25f, alpha * 0.45f);
        }
    }
}
