using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Bridge đồng bộ TrailRenderer, hiệu ứng tích tụ chiêu Ultimate, 
/// và render hiệu ứng Hitbox Shockwave AOE của Phi Kiếm trong ECS.
/// Giúp hiển thị trực quan tầm ảnh hưởng sát thương và trạng thái Ultimate cho người chơi.
/// </summary>
public class FlyingSwordTrailBridge : MonoBehaviour
{
    [Header("Trail Settings")]
    [Tooltip("Thời gian tồn tại của vệt đuôi (giữ ngắn ~0.15s - 0.2s để không bị kéo quá dài)")]
    [SerializeField] private float trailTime = 0.18f;
    [SerializeField] private float startWidth = 0.22f;
    [SerializeField] private float endWidth = 0.02f;
    [SerializeField] private Color normalColor = new Color(0.2f, 0.88f, 1f, 0.85f);
    [SerializeField] private Color ultimateColor = new Color(1f, 0.85f, 0.25f, 0.95f);
    [SerializeField] private Material trailMaterial;

    private EntityManager _entityManager;
    private EntityQuery _swordQuery;
    private EntityQuery _skillQuery;
    private EntityQuery _blastQuery;
    private bool _initialized;

    // Quản lý Trail & Aura cho từng thanh kiếm
    private class SwordVisualItem
    {
        public GameObject Root;
        public TrailRenderer Trail;
        public GameObject AuraObj;
        public SpriteRenderer AuraRenderer;
        public bool IsUltimateMode;
    }

    private readonly List<SwordVisualItem> _visualPool = new List<SwordVisualItem>();
    private readonly List<SwordShockwaveEffect> _shockwavePool = new List<SwordShockwaveEffect>();

    private static Sprite _circleSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<FlyingSwordTrailBridge>() == null)
        {
            var go = new GameObject("[FlyingSwordTrailBridge]");
            go.AddComponent<FlyingSwordTrailBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        InitECS();
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _swordQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FlyingSwordTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<FlyingSwordData>());

        _skillQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FlyingSwordSkillTag>(),
            ComponentType.ReadOnly<SkillRuntimeData>());

        _blastQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FlyingSwordAoeBlastEvent>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        // 1. Xử lý các sự kiện nổ Hitbox AOE Ultimate từ ECS
        HandleAoeBlastEvents();

        // 2. Kiểm tra cấp độ kỹ năng Phi Kiếm
        int skillLevel = 1;
        if (!_skillQuery.IsEmpty)
        {
            skillLevel = _skillQuery.GetSingleton<SkillRuntimeData>().Level;
        }

        // 3. Đồng bộ Trail và Aura trạng thái của từng thanh kiếm
        if (_swordQuery.IsEmpty)
        {
            for (int i = 0; i < _visualPool.Count; i++)
            {
                if (_visualPool[i] != null && _visualPool[i].Root.activeSelf)
                {
                    _visualPool[i].Trail.emitting = false;
                    _visualPool[i].Root.SetActive(false);
                }
            }
            return;
        }

        var transforms = _swordQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var swordDataArray = _swordQuery.ToComponentDataArray<FlyingSwordData>(Allocator.Temp);
        int swordCount = transforms.Length;

        // Đảm bảo pool đủ số lượng item cho từng kiếm
        while (_visualPool.Count < swordCount)
        {
            _visualPool.Add(CreateNewSwordVisual());
        }

        for (int i = 0; i < _visualPool.Count; i++)
        {
            var item = _visualPool[i];
            if (i < swordCount)
            {
                if (!item.Root.activeSelf)
                {
                    item.Root.SetActive(true);
                    item.Trail.Clear();
                }

                Vector3 targetPos = new Vector3(
                    transforms[i].Position.x,
                    transforms[i].Position.y,
                    transforms[i].Position.z);

                // Dịch chuyển tức thời quá xa (> 4m) -> Clear trail để tránh vạch cắt màn hình
                if (Vector3.Distance(item.Root.transform.position, targetPos) > 4f)
                {
                    item.Trail.Clear();
                }

                item.Root.transform.position = targetPos;
                item.Root.transform.rotation = transforms[i].Rotation;
                item.Trail.emitting = true;

                // --- Hiệu ứng Ultimate Ready & Ultimate Active ---
                bool isUltimateStrike = swordDataArray[i].IsUltimate;
                bool isUltimateReady = (skillLevel >= 4 && swordDataArray[i].UltimateTimer <= 0f);

                if (isUltimateStrike)
                {
                    // Đang trong cú lướt Ultimate: Vệt đuôi vàng cam rực lửa cực đại (width 0.5f)
                    if (!item.IsUltimateMode)
                    {
                        item.IsUltimateMode = true;
                        ApplyTrailGradient(item.Trail, ultimateColor, new Color(1f, 0.4f, 0.1f, 0.9f), 0.5f, 0.05f);
                    }
                    item.AuraObj.SetActive(true);
                    item.AuraObj.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                    item.AuraRenderer.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                    item.AuraObj.transform.localScale = Vector3.one * (1.6f + Mathf.PingPong(Time.time * 6f, 0.4f));
                }
                else if (isUltimateReady)
                {
                    // Ultimate đã tích tụ xong: Aura vàng kim xoay quanh kiếm, báo hiệu sẵn sàng
                    if (!item.IsUltimateMode)
                    {
                        item.IsUltimateMode = true;
                        ApplyTrailGradient(item.Trail, new Color(1f, 0.9f, 0.35f, 0.9f), normalColor, 0.3f, 0.02f);
                    }
                    item.AuraObj.SetActive(true);
                    item.AuraObj.transform.Rotate(0f, 0f, 360f * Time.deltaTime);
                    float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.2f;
                    item.AuraRenderer.color = new Color(1f, 0.9f, 0.3f, 0.75f + Mathf.Sin(Time.time * 8f) * 0.2f);
                    item.AuraObj.transform.localScale = Vector3.one * pulse;
                }
                else
                {
                    // Trạng thái bình thường: Vệt Cyan thon gọn, tắt Aura
                    if (item.IsUltimateMode)
                    {
                        item.IsUltimateMode = false;
                        ApplyTrailGradient(item.Trail, normalColor, normalColor, startWidth, endWidth);
                    }
                    item.AuraObj.SetActive(false);
                }
            }
            else
            {
                if (item.Root.activeSelf)
                {
                    item.Trail.emitting = false;
                    item.Root.SetActive(false);
                }
            }
        }

        transforms.Dispose();
        swordDataArray.Dispose();
    }

    /// <summary>
    /// Thu thập các Event nổ AOE từ ECS và kích hoạt Shockwave Hitbox trên màn hình
    /// </summary>
    private void HandleAoeBlastEvents()
    {
        if (_blastQuery.IsEmpty) return;

        var blastEntities = _blastQuery.ToEntityArray(Allocator.Temp);
        var blastDataArray = _blastQuery.ToComponentDataArray<FlyingSwordAoeBlastEvent>(Allocator.Temp);

        for (int i = 0; i < blastDataArray.Length; i++)
        {
            Vector3 center = new Vector3(blastDataArray[i].Position.x, blastDataArray[i].Position.y, 0f);
            float radius = blastDataArray[i].Radius;
            SpawnShockwaveHitbox(center, radius);
        }

        // Hủy các entity event sau khi đã spawn VFX
        _entityManager.DestroyEntity(_blastQuery);

        blastEntities.Dispose();
        blastDataArray.Dispose();
    }

    private void SpawnShockwaveHitbox(Vector3 position, float radius)
    {
        SwordShockwaveEffect shockwave = null;
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
            var go = new GameObject($"SwordShockwave_{_shockwavePool.Count}");
            go.transform.SetParent(transform);
            shockwave = go.AddComponent<SwordShockwaveEffect>();
            _shockwavePool.Add(shockwave);
        }

        shockwave.Setup(position, radius, 0.45f);
    }

    private SwordVisualItem CreateNewSwordVisual()
    {
        var root = new GameObject($"SwordVisual_{_visualPool.Count}");
        root.transform.SetParent(transform);

        // 1. Trail Renderer
        var trail = root.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.minVertexDistance = 0.04f;
        trail.sortingOrder = 0;

        ApplyTrailGradient(trail, normalColor, normalColor, startWidth, endWidth);

        var defaultShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                           ?? Shader.Find("Sprites/Default");
        if (defaultShader != null)
        {
            trail.material = new Material(defaultShader);
        }

        // 2. Aura GameObject (vòng hào quang xoay quanh chuôi kiếm khi Ultimate sẵn sàng)
        var auraObj = new GameObject("UltimateAura");
        auraObj.transform.SetParent(root.transform, false);
        var auraRenderer = auraObj.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = GetCircleSprite();
        auraRenderer.sortingOrder = 2; // Hiển thị trên kiếm
        auraRenderer.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        auraObj.transform.localScale = Vector3.one * 0.9f;
        auraObj.SetActive(false);

        return new SwordVisualItem
        {
            Root = root,
            Trail = trail,
            AuraObj = auraObj,
            AuraRenderer = auraRenderer,
            IsUltimateMode = false
        };
    }

    private void ApplyTrailGradient(TrailRenderer trail, Color headColor, Color tailColor, float sWidth, float eWidth)
    {
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, sWidth);
        widthCurve.AddKey(1f, eWidth);
        trail.widthCurve = widthCurve;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(headColor, 0f),
                new GradientColorKey(tailColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(headColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center)) / center;
                if (dist > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // Vòng halo có lỗ ở tâm và viền sáng rõ
                    float ring = Mathf.Sin(dist * Mathf.PI);
                    float alpha = Mathf.Pow(ring, 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _circleSprite;
    }
}

/// <summary>
/// Hiệu ứng Shockwave Hitbox thể hiện chính xác bán kính sát thương và phạm vi hủy đạn (AOE Radius) của Phi Kiếm
/// </summary>
public class SwordShockwaveEffect : MonoBehaviour
{
    private LineRenderer _outerRing;
    private SpriteRenderer _innerFlash;
    private float _maxRadius;
    private float _duration = 0.45f;
    private float _elapsed = 0f;
    private const int SEGMENTS = 48;

    private void Awake()
    {
        var defaultShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                           ?? Shader.Find("Sprites/Default");
        Material sharedMat = defaultShader != null ? new Material(defaultShader) : null;

        // 1. LineRenderer vẽ vòng tròn viền ngoài (Outer Shockwave Ring)
        _outerRing = gameObject.AddComponent<LineRenderer>();
        _outerRing.useWorldSpace = false;
        _outerRing.positionCount = SEGMENTS + 1;
        _outerRing.loop = true;
        _outerRing.sortingOrder = 5;
        if (sharedMat != null) _outerRing.material = sharedMat;

        // 2. SpriteRenderer lấp đầy vùng nổ bên trong (Inner Energy Glow Disc)
        var flashObj = new GameObject("InnerFlash");
        flashObj.transform.SetParent(transform, false);
        _innerFlash = flashObj.AddComponent<SpriteRenderer>();
        _innerFlash.sprite = FlyingSwordTrailBridge.GetCircleSprite();
        _innerFlash.sortingOrder = 4;
        if (sharedMat != null) _innerFlash.material = sharedMat;
    }

    public void Setup(Vector3 position, float radius, float duration = 0.45f)
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
        // Ease-out Cubic: nổ bung rất nhanh ở frame đầu rồi hãm lại tại biên giới Hitbox
        float easeT = 1f - Mathf.Pow(1f - t, 3f);
        float currentRadius = Mathf.Lerp(0.3f, _maxRadius, easeT);
        float alpha = Mathf.Clamp01((1f - t) * 1.4f);

        // Cập nhật vòng tròn viền ngoài
        if (_outerRing != null)
        {
            _outerRing.widthMultiplier = Mathf.Lerp(0.18f, 0.02f, t);
            Color ringColor = Color.Lerp(
                new Color(1f, 0.9f, 0.35f, alpha),
                new Color(0.2f, 0.95f, 1f, alpha * 0.8f),
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

        // Cập nhật đĩa năng lượng phát sáng bên trong
        if (_innerFlash != null)
        {
            _innerFlash.transform.localScale = Vector3.one * (currentRadius * 2f);
            _innerFlash.color = new Color(0.3f, 0.95f, 1f, alpha * 0.35f);
        }
    }
}
