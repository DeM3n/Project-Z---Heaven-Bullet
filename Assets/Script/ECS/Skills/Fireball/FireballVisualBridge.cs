using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge quản lý toàn bộ hiệu ứng hình ảnh (VFX) của Cầu Lửa (Fireball):
/// 1. Vệt đuôi lửa (Fiery Trail) và quả cầu phát sáng cho Cầu Lửa Mẹ & Cầu Lửa Con.
/// 2. Hiệu ứng sóng xung kích nổ nhiệt (Explosion Shockwave) với bán kính chính xác theo ECS.
/// 3. Vùng Lửa Thiêu Đốt Cấp 4 (Scorched Fire Zone): Vòng lửa bốc cháy bập bùng trên mặt đất.
/// </summary>
public class FireballVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _mainFireballQuery;
    private EntityQuery _subFireballQuery;
    private EntityQuery _fireZoneQuery;
    private EntityQuery _explosionEventQuery;
    private bool _initialized;

    // --- Object Pools & Entity Mapping ---
    private readonly Dictionary<Entity, FireballVisualItem> _activeMainVisuals = new Dictionary<Entity, FireballVisualItem>();
    private readonly List<FireballVisualItem> _freeMainPool = new List<FireballVisualItem>();

    private readonly Dictionary<Entity, FireballVisualItem> _activeSubVisuals = new Dictionary<Entity, FireballVisualItem>();
    private readonly List<FireballVisualItem> _freeSubPool = new List<FireballVisualItem>();

    private readonly Dictionary<Entity, FireZoneVisualEffect> _activeZones = new Dictionary<Entity, FireZoneVisualEffect>();
    private readonly List<FireZoneVisualEffect> _freeZonePool = new List<FireZoneVisualEffect>();

    private readonly List<FireballExplosionEffect> _explosionPool = new List<FireballExplosionEffect>();

    private readonly HashSet<Entity> _currentEntitySet = new HashSet<Entity>();
    private readonly List<Entity> _toRemoveList = new List<Entity>();

    private int _mainCounter;
    private int _subCounter;
    private int _zoneCounter;

    private static Sprite _circleSprite;

    private class FireballVisualItem
    {
        public GameObject Root;
        public TrailRenderer Trail;
        public SpriteRenderer CoreRenderer;
        public SpriteRenderer GlowRenderer;
        public GameObject ShadowObj;
        public SpriteRenderer ShadowRenderer;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<FireballVisualBridge>() == null)
        {
            var go = new GameObject("[FireballVisualBridge]");
            go.AddComponent<FireballVisualBridge>();
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

        _mainFireballQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FireballTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<FireballProjectileData>());

        _subFireballQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SubFireballTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<FireballProjectileData>());

        _fireZoneQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FireZoneTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<FireZoneData>());

        _explosionEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<FireballExplosionEvent>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        // 1. Xử lý các sự kiện nổ cầu lửa từ ECS
        HandleExplosionEvents();

        // 2. Đồng bộ vị trí & vệt đuôi Cầu Lửa Mẹ
        SyncProjectiles(_mainFireballQuery, _activeMainVisuals, _freeMainPool, isMain: true);

        // 3. Đồng bộ vị trí & vệt đuôi Cầu Lửa Con
        SyncProjectiles(_subFireballQuery, _activeSubVisuals, _freeSubPool, isMain: false);

        // 4. Đồng bộ Vùng Lửa Thiêu Đốt Cấp 4
        SyncFireZones();
    }

    private void HandleExplosionEvents()
    {
        if (_explosionEventQuery.IsEmpty) return;

        var events = _explosionEventQuery.ToComponentDataArray<FireballExplosionEvent>(Allocator.Temp);

        for (int i = 0; i < events.Length; i++)
        {
            Vector3 center = new Vector3(events[i].Position.x, events[i].Position.y, 0f);
            float radius = events[i].Radius;
            bool isMain = events[i].IsMainExplosion;

            SpawnExplosionShockwave(center, radius, isMain);
        }

        _entityManager.DestroyEntity(_explosionEventQuery);
        events.Dispose();
    }

    private void SpawnExplosionShockwave(Vector3 position, float radius, bool isMain)
    {
        FireballExplosionEffect effect = null;
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
            var go = new GameObject($"FireballExplosion_{_explosionPool.Count}");
            go.transform.SetParent(transform);
            effect = go.AddComponent<FireballExplosionEffect>();
            _explosionPool.Add(effect);
        }

        effect.Setup(position, radius, isMain ? 0.40f : 0.28f, isMain);
    }

    private void SyncProjectiles(
        EntityQuery query,
        Dictionary<Entity, FireballVisualItem> activeMap,
        List<FireballVisualItem> freePool,
        bool isMain)
    {
        if (query.IsEmpty)
        {
            if (activeMap.Count > 0)
            {
                foreach (var kvp in activeMap)
                {
                    var item = kvp.Value;
                    if (item != null && item.Root != null)
                    {
                        item.Trail.emitting = false;
                        item.Trail.Clear();
                        item.Root.SetActive(false);
                        if (item.ShadowObj != null) item.ShadowObj.SetActive(false);
                        freePool.Add(item);
                    }
                }
                activeMap.Clear();
            }
            return;
        }

        var entities = query.ToEntityArray(Allocator.Temp);
        var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var projs = query.ToComponentDataArray<FireballProjectileData>(Allocator.Temp);

        // 1. Quét dọn các Entity đã bị hủy trong ECS (ví dụ: chạm đất nổ)
        _currentEntitySet.Clear();
        for (int i = 0; i < entities.Length; i++)
        {
            _currentEntitySet.Add(entities[i]);
        }

        _toRemoveList.Clear();
        foreach (var kvp in activeMap)
        {
            if (!_currentEntitySet.Contains(kvp.Key))
            {
                var item = kvp.Value;
                if (item != null && item.Root != null)
                {
                    // Tắt emitting và xóa trail ngay lập tức, không để lại vạch nối sang entity khác
                    item.Trail.emitting = false;
                    item.Trail.Clear();
                    item.Root.SetActive(false);
                    if (item.ShadowObj != null) item.ShadowObj.SetActive(false);
                    freePool.Add(item);
                }
                _toRemoveList.Add(kvp.Key);
            }
        }

        for (int i = 0; i < _toRemoveList.Count; i++)
        {
            activeMap.Remove(_toRemoveList[i]);
        }

        // 2. Cập nhật vị trí & hiệu ứng cho từng Entity còn tồn tại
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            Vector3 pos = new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f);

            if (!activeMap.TryGetValue(entity, out var item))
            {
                // Entity mới sinh -> Lấy visual từ pool hoặc tạo mới
                if (freePool.Count > 0)
                {
                    int lastIdx = freePool.Count - 1;
                    item = freePool[lastIdx];
                    freePool.RemoveAt(lastIdx);
                }
                else
                {
                    item = CreateProjectileVisual(isMain);
                }

                // Cực kỳ quan trọng để ngăn vệt trail bị kéo từ vị trí cũ sang vị trí mới:
                // Tắt emitting -> Di chuyển GameObject -> Clear cache -> Bật Active -> Clear -> Bật emitting
                item.Trail.emitting = false;
                item.Root.transform.position = pos;
                item.Trail.Clear();
                item.Root.SetActive(true);
                item.Trail.Clear();
                item.Trail.emitting = true;
                if (item.ShadowObj != null) item.ShadowObj.SetActive(true);

                activeMap.Add(entity, item);
            }
            else
            {
                // Entity đang tiếp tục bay -> cập nhật vị trí bình thường
                item.Root.transform.position = pos;
            }

            if (!isMain)
            {
                // Cầu lửa con: Quỹ đạo Parabol bay bổng lên trời
                float t = Mathf.Clamp01(projs[i].ElapsedTime / Mathf.Max(0.01f, projs[i].TotalDuration));
                float height = 4f * projs[i].ArcHeight * t * (1f - t);

                // 1. Phóng to khi ở trên cao (gần camera / bầu trời) và thu nhỏ khi rơi chạm đất
                float scaleBoost = 1f + (height / Mathf.Max(0.1f, projs[i].ArcHeight)) * 0.45f;
                item.Root.transform.localScale = Vector3.one * scaleBoost;

                // 2. Bóng đổ trên mặt đất (Ground Shadow)
                if (item.ShadowObj != null)
                {
                    float2 groundPos = math.lerp(projs[i].StartPosition, projs[i].TargetPosition, t);
                    item.ShadowObj.transform.position = new Vector3(groundPos.x, groundPos.y, 0f);

                    float shadowSize = Mathf.Lerp(0.38f, 0.18f, height / 3f);
                    item.ShadowObj.transform.localScale = new Vector3(shadowSize * 1.3f, shadowSize * 0.7f, 1f);
                    float shadowAlpha = Mathf.Lerp(0.55f, 0.2f, height / 3f);
                    item.ShadowRenderer.color = new Color(0f, 0f, 0f, shadowAlpha);
                }

                // Xoay nhẹ theo góc bay
                item.Root.transform.rotation = transforms[i].Rotation;
            }
            else
            {
                // Cầu lửa mẹ: Xoay cuộn trào bình thường
                item.Root.transform.localScale = Vector3.one;
                item.Root.transform.Rotate(0f, 0f, 450f * Time.deltaTime);

                if (item.ShadowObj != null)
                {
                    item.ShadowObj.transform.position = pos + new Vector3(0f, -0.18f, 0f);
                    item.ShadowObj.transform.localScale = new Vector3(0.55f, 0.28f, 1f);
                    item.ShadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
                }
            }

            // Hiệu ứng nhấp nháy phát sáng nhẹ của lõi lửa
            float pulse = 1f + Mathf.Sin(Time.time * 16f) * 0.15f;
            item.GlowRenderer.transform.localScale = Vector3.one * pulse;
        }

        entities.Dispose();
        transforms.Dispose();
        projs.Dispose();
    }

    private void SyncFireZones()
    {
        if (_fireZoneQuery.IsEmpty)
        {
            if (_activeZones.Count > 0)
            {
                foreach (var kvp in _activeZones)
                {
                    if (kvp.Value != null && kvp.Value.gameObject.activeSelf)
                    {
                        kvp.Value.gameObject.SetActive(false);
                    }
                    _freeZonePool.Add(kvp.Value);
                }
                _activeZones.Clear();
            }
            return;
        }

        var entities = _fireZoneQuery.ToEntityArray(Allocator.Temp);
        var transforms = _fireZoneQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var zones = _fireZoneQuery.ToComponentDataArray<FireZoneData>(Allocator.Temp);

        _currentEntitySet.Clear();
        for (int i = 0; i < entities.Length; i++)
        {
            _currentEntitySet.Add(entities[i]);
        }

        _toRemoveList.Clear();
        foreach (var kvp in _activeZones)
        {
            if (!_currentEntitySet.Contains(kvp.Key))
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeSelf)
                {
                    kvp.Value.gameObject.SetActive(false);
                }
                _freeZonePool.Add(kvp.Value);
                _toRemoveList.Add(kvp.Key);
            }
        }

        for (int i = 0; i < _toRemoveList.Count; i++)
        {
            _activeZones.Remove(_toRemoveList[i]);
        }

        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            Vector3 pos = new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f);
            float radius = zones[i].Radius;
            float durationRem = zones[i].DurationRemaining;

            if (!_activeZones.TryGetValue(entity, out var effect))
            {
                if (_freeZonePool.Count > 0)
                {
                    int lastIdx = _freeZonePool.Count - 1;
                    effect = _freeZonePool[lastIdx];
                    _freeZonePool.RemoveAt(lastIdx);
                }
                else
                {
                    var go = new GameObject($"FireZoneVisual_{++_zoneCounter}");
                    go.transform.SetParent(transform);
                    effect = go.AddComponent<FireZoneVisualEffect>();
                }

                effect.Setup(pos, radius);
                _activeZones.Add(entity, effect);
            }
            else
            {
                effect.UpdatePositionAndRadius(pos, radius, durationRem);
            }
        }

        entities.Dispose();
        transforms.Dispose();
        zones.Dispose();
    }

    private FireballVisualItem CreateProjectileVisual(bool isMain)
    {
        int id = isMain ? (++_mainCounter) : (++_subCounter);
        var root = new GameObject(isMain ? $"MainFireballVisual_{id}" : $"SubFireballVisual_{id}");
        root.transform.SetParent(transform);

        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        // 1. Trail Renderer (Đuôi lửa)
        var trail = root.AddComponent<TrailRenderer>();
        trail.material = mat;
        trail.time = isMain ? 0.22f : 0.14f;
        trail.startWidth = isMain ? 0.35f : 0.20f;
        trail.endWidth = 0.02f;
        trail.sortingOrder = 6;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f),   // Lõi vàng chói
                new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.5f), // Cam rực lửa
                new GradientColorKey(new Color(0.9f, 0.1f, 0.05f), 1f)   // Đỏ rực
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        // 2. Glow Ngoài (Aura rực sáng)
        var glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(root.transform, false);
        var glowRend = glowObj.AddComponent<SpriteRenderer>();
        glowRend.sprite = GetCircleSprite();
        glowRend.color = isMain ? new Color(1f, 0.45f, 0.05f, 0.75f) : new Color(1f, 0.4f, 0.1f, 0.65f);
        glowRend.sortingOrder = 7;
        glowRend.transform.localScale = Vector3.one * (isMain ? 0.6f : 0.35f);
        if (mat != null) glowRend.material = mat;

        // 3. Core Lõi sáng (Trắng Vàng)
        var coreObj = new GameObject("Core");
        coreObj.transform.SetParent(root.transform, false);
        var coreRend = coreObj.AddComponent<SpriteRenderer>();
        coreRend.sprite = GetCircleSprite();
        coreRend.color = new Color(1f, 0.95f, 0.7f, 1f);
        coreRend.sortingOrder = 8;
        coreRend.transform.localScale = Vector3.one * (isMain ? 0.32f : 0.18f);
        if (mat != null) coreRend.material = mat;

        // 4. Bóng đổ trên mặt đất (Ground Shadow)
        var shadowObj = new GameObject(isMain ? $"MainFireballShadow_{id}" : $"SubFireballShadow_{id}");
        shadowObj.transform.SetParent(transform);
        var shadowRend = shadowObj.AddComponent<SpriteRenderer>();
        shadowRend.sprite = GetCircleSprite();
        shadowRend.color = new Color(0f, 0f, 0f, 0.45f);
        shadowRend.sortingOrder = 3;
        if (mat != null) shadowRend.material = mat;
        shadowObj.SetActive(false);

        root.SetActive(false);

        return new FireballVisualItem
        {
            Root = root,
            Trail = trail,
            CoreRenderer = coreRend,
            GlowRenderer = glowRend,
            ShadowObj = shadowObj,
            ShadowRenderer = shadowRend
        };
    }

    private void OnDestroy()
    {
        _activeMainVisuals.Clear();
        _freeMainPool.Clear();
        _activeSubVisuals.Clear();
        _freeSubPool.Clear();
        _activeZones.Clear();
        _freeZonePool.Clear();
        _explosionPool.Clear();
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
}

/// <summary>
/// Hiệu ứng sóng xung kích nổ nhiệt (Explosion Shockwave) của Cầu Lửa
/// </summary>
public class FireballExplosionEffect : MonoBehaviour
{
    private LineRenderer _outerRing;
    private SpriteRenderer _innerBlast;
    private float _maxRadius;
    private float _duration = 0.40f;
    private float _elapsed = 0f;
    private bool _isMain;
    private const int SEGMENTS = 36;

    private void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        _outerRing = gameObject.AddComponent<LineRenderer>();
        _outerRing.useWorldSpace = false;
        _outerRing.positionCount = SEGMENTS + 1;
        _outerRing.loop = true;
        _outerRing.sortingOrder = 9;
        if (mat != null) _outerRing.material = mat;

        var blastObj = new GameObject("InnerBlast");
        blastObj.transform.SetParent(transform, false);
        _innerBlast = blastObj.AddComponent<SpriteRenderer>();
        _innerBlast.sprite = FireballVisualBridge.GetCircleSprite();
        _innerBlast.sortingOrder = 8;
        if (mat != null) _innerBlast.material = mat;
    }

    public void Setup(Vector3 position, float radius, float duration, bool isMain)
    {
        transform.position = position;
        _maxRadius = radius;
        _duration = duration;
        _elapsed = 0f;
        _isMain = isMain;
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
        // Ease-out Cubic nổ bung nhanh
        float easeT = 1f - Mathf.Pow(1f - t, 3f);
        float currentRadius = Mathf.Lerp(0.2f, _maxRadius, easeT);
        float alpha = Mathf.Clamp01((1f - t) * 1.5f);

        // Vòng ngoài
        if (_outerRing != null)
        {
            _outerRing.widthMultiplier = Mathf.Lerp(_isMain ? 0.22f : 0.12f, 0.02f, t);
            Color ringColor = Color.Lerp(
                new Color(1f, 0.95f, 0.4f, alpha),
                new Color(1f, 0.25f, 0.05f, alpha * 0.8f),
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

        // Đĩa sáng nổ nhiệt bên trong
        if (_innerBlast != null)
        {
            _innerBlast.transform.localScale = Vector3.one * (currentRadius * 2f);
            Color blastColor = Color.Lerp(
                new Color(1f, 0.9f, 0.5f, alpha * 0.85f),
                new Color(0.9f, 0.2f, 0.05f, 0f),
                t);
            _innerBlast.color = blastColor;
        }
    }
}

/// <summary>
/// Hiệu ứng Vùng Lửa Thiêu Đốt (Scorched Fire Zone) trên mặt đất ở Cấp độ 4:
/// 1. Lớp đất cháy sém đen khói (Charred Earth Decal)
/// 2. Lòng hồ dung nham rực lửa (Magma Pool)
/// 3. Vành đai lửa sóng động (Turbulent Wave Flame Border)
/// 4. Các vết nứt dung nham phân nhánh ngẫu nhiên (Procedural Magma Fissures)
/// 5. Hệ hạt tàn lửa bay lượn bốc lên trời (Rising Sparks & Embers ParticleSystem)
/// 100% được sinh bằng code C# thuần túy, hoàn toàn KHÔNG cần bất kỳ asset/texture bên ngoài!
/// </summary>
public class FireZoneVisualEffect : MonoBehaviour
{
    private LineRenderer _borderRing;
    private SpriteRenderer _charredGround;
    private SpriteRenderer _groundFire;
    private SpriteRenderer _innerEmber;
    private ParticleSystem _emberParticles;
    private readonly List<LineRenderer> _crackLines = new List<LineRenderer>();
    private const int NUM_MAIN_CRACKS = 6;
    private const int NUM_FORK_CRACKS = 6;
    private const int NUM_CROSS_CRACKS = 4;
    private const int TOTAL_CRACKS = NUM_MAIN_CRACKS + NUM_FORK_CRACKS + NUM_CROSS_CRACKS; // 16 nhánh kết nối
    private const int SEGMENTS = 48;
    private float _radius;

    private void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        // 1. Lớp đất sém đen khói (Charred Earth Decal)
        var charObj = new GameObject("CharredGround");
        charObj.transform.SetParent(transform, false);
        _charredGround = charObj.AddComponent<SpriteRenderer>();
        _charredGround.sprite = FireballVisualBridge.GetCircleSprite();
        _charredGround.sortingOrder = 1;
        if (mat != null) _charredGround.material = mat;

        // 2. Vùng dung nham mặt đất (Magma Pool)
        var groundObj = new GameObject("GroundFire");
        groundObj.transform.SetParent(transform, false);
        _groundFire = groundObj.AddComponent<SpriteRenderer>();
        _groundFire.sprite = FireballVisualBridge.GetCircleSprite();
        _groundFire.sortingOrder = 2;
        if (mat != null) _groundFire.material = mat;

        // 3. Lõi nhiệt bốc cháy bên trong (Inner Ember)
        var emberObj = new GameObject("InnerEmber");
        emberObj.transform.SetParent(transform, false);
        _innerEmber = emberObj.AddComponent<SpriteRenderer>();
        _innerEmber.sprite = FireballVisualBridge.GetCircleSprite();
        _innerEmber.sortingOrder = 3;
        if (mat != null) _innerEmber.material = mat;

        // 4. Mạng lưới các vết nứt dung nham nứt toác trên mặt đất (Nhánh chính + Nhánh rẽ + Cầu nối ngang)
        for (int i = 0; i < TOTAL_CRACKS; i++)
        {
            var crackObj = new GameObject($"LavaCrack_{i}");
            crackObj.transform.SetParent(transform, false);
            var line = crackObj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sortingOrder = 3;
            if (mat != null) line.material = mat;
            _crackLines.Add(line);
        }

        // 5. Viền ngoài lửa bập bùng (Turbulent Flame Border)
        _borderRing = gameObject.AddComponent<LineRenderer>();
        _borderRing.useWorldSpace = false;
        _borderRing.positionCount = SEGMENTS + 1;
        _borderRing.loop = true;
        _borderRing.sortingOrder = 4;
        _borderRing.widthMultiplier = 0.08f;
        if (mat != null) _borderRing.material = mat;

        // 6. Hệ hạt tàn lửa bay bổng lên trời (Rising Embers Particle System)
        InitParticleSystem(shader);
    }

    private void InitParticleSystem(Shader shader)
    {
        var psObj = new GameObject("EmberParticles");
        psObj.transform.SetParent(transform, false);
        _emberParticles = psObj.AddComponent<ParticleSystem>();
        var psr = psObj.GetComponent<ParticleSystemRenderer>();

        if (shader != null)
        {
            Material pMat = new Material(shader);
            pMat.mainTexture = FireballVisualBridge.GetCircleSprite().texture;
            psr.material = pMat;
        }
        psr.sortingOrder = 5;

        var main = _emberParticles.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f; // Tự động bốc nổi lên trên

        var emission = _emberParticles.emission;
        emission.rateOverTime = 22f;

        var shape = _emberParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.4f;

        var colorOverLifetime = _emberParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.7f),
                new GradientColorKey(new Color(0.8f, 0.1f, 0.05f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = _emberParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1.0f);
        sizeCurve.AddKey(0.6f, 0.8f);
        sizeCurve.AddKey(1.0f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
    }

    public void Setup(Vector3 position, float radius)
    {
        transform.position = position;
        _radius = radius;
        gameObject.SetActive(true);

        GenerateCracks(radius);

        if (_emberParticles != null)
        {
            var shape = _emberParticles.shape;
            shape.radius = radius * 0.8f;
            _emberParticles.Clear();
            _emberParticles.Play();
        }

        UpdateFlameBorder(radius, 1f, 1f);
    }

    public void UpdatePositionAndRadius(Vector3 position, float radius, float durationRemaining)
    {
        transform.position = position;
        _radius = radius;

        float flicker = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
        float alpha = Mathf.Clamp01(durationRemaining * 2.2f); // Mờ dần khi sắp hết thời gian

        // 1. Viền lửa nhấp nhô lượn sóng động
        UpdateFlameBorder(radius, flicker, alpha);

        // 2. Lớp đất sém đen
        if (_charredGround != null)
        {
            _charredGround.transform.localScale = Vector3.one * (_radius * 2.3f);
            _charredGround.color = new Color(0.06f, 0.03f, 0.03f, 0.72f * alpha);
        }

        // 3. Vùng dung nham mặt đất
        if (_groundFire != null)
        {
            float poolPulse = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
            _groundFire.transform.localScale = Vector3.one * (_radius * 2.0f * poolPulse);
            _groundFire.color = new Color(0.95f, 0.32f, 0.05f, 0.52f * flicker * alpha);
        }

        // 4. Lõi nhiệt bốc cháy bên trong
        if (_innerEmber != null)
        {
            float emberPulse = 0.75f + Mathf.PingPong(Time.time * 4.5f, 0.25f);
            _innerEmber.transform.localScale = Vector3.one * (_radius * 1.35f * emberPulse);
            _innerEmber.color = new Color(1f, 0.88f, 0.25f, 0.7f * flicker * alpha);
            _innerEmber.transform.Rotate(0f, 0f, 35f * Time.deltaTime);
        }

        // 5. Cập nhật các vết nứt dung nham
        UpdateCracks(flicker, alpha);

        // 6. Ngừng phun hạt khi sắp tắt
        if (_emberParticles != null && durationRemaining <= 0.25f && _emberParticles.isPlaying)
        {
            _emberParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void GenerateCracks(float radius)
    {
        if (_crackLines.Count < TOTAL_CRACKS) return;

        Vector3[,] mainPoints = new Vector3[NUM_MAIN_CRACKS, 5];

        // 1. Nhánh chính: 6 nhánh bắt đầu từ đúng tâm nổ (0,0) -> Nối liền nhau tại tâm
        for (int i = 0; i < NUM_MAIN_CRACKS; i++)
        {
            var line = _crackLines[i];
            float baseAngle = (i / (float)NUM_MAIN_CRACKS) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.15f, 0.15f);
            line.positionCount = 5;

            // Điểm 0: Ngay tâm (0,0) -> Tất cả nhánh chính đều chạm nhau tại tâm nổ!
            mainPoints[i, 0] = Vector3.zero;
            line.SetPosition(0, Vector3.zero);

            // Điểm 1: Cự ly gần tâm
            float dist1 = radius * UnityEngine.Random.Range(0.20f, 0.28f);
            float angle1 = baseAngle + UnityEngine.Random.Range(-0.10f, 0.10f);
            mainPoints[i, 1] = new Vector3(Mathf.Cos(angle1) * dist1, Mathf.Sin(angle1) * dist1, 0f);
            line.SetPosition(1, mainPoints[i, 1]);

            // Điểm 2: Cự ly trung bình
            float dist2 = radius * UnityEngine.Random.Range(0.42f, 0.52f);
            float angle2 = angle1 + UnityEngine.Random.Range(-0.15f, 0.15f);
            mainPoints[i, 2] = new Vector3(Mathf.Cos(angle2) * dist2, Mathf.Sin(angle2) * dist2, 0f);
            line.SetPosition(2, mainPoints[i, 2]);

            // Điểm 3: Cự ly xa
            float dist3 = radius * UnityEngine.Random.Range(0.65f, 0.74f);
            float angle3 = angle2 + UnityEngine.Random.Range(-0.18f, 0.18f);
            mainPoints[i, 3] = new Vector3(Mathf.Cos(angle3) * dist3, Mathf.Sin(angle3) * dist3, 0f);
            line.SetPosition(3, mainPoints[i, 3]);

            // Điểm 4: Đầu ngọn vết nứt mép ngoài
            float dist4 = radius * UnityEngine.Random.Range(0.82f, 0.92f);
            float angle4 = angle3 + UnityEngine.Random.Range(-0.22f, 0.22f);
            mainPoints[i, 4] = new Vector3(Mathf.Cos(angle4) * dist4, Mathf.Sin(angle4) * dist4, 0f);
            line.SetPosition(4, mainPoints[i, 4]);
        }

        // 2. Nhánh rẽ phụ: 6 nhánh tách ra từ thân các nhánh chính (Y-shaped Forks)
        int crackIdx = NUM_MAIN_CRACKS;
        for (int i = 0; i < NUM_FORK_CRACKS; i++, crackIdx++)
        {
            var line = _crackLines[crackIdx];
            line.positionCount = 4;

            // Rẽ từ điểm 1 hoặc điểm 2 của nhánh chính tương ứng
            int attachIdx = (i % 2 == 0) ? 2 : 1;
            Vector3 startPt = mainPoints[i, attachIdx];
            line.SetPosition(0, startPt);

            float baseAngle = Mathf.Atan2(startPt.y, startPt.x);
            // Góc rẽ 35° đến 55° sang trái hoặc phải
            float forkSide = (i % 2 == 0) ? 1f : -1f;
            float forkAngle = baseAngle + forkSide * UnityEngine.Random.Range(0.6f, 0.95f);

            float branchStep = radius * UnityEngine.Random.Range(0.16f, 0.22f);
            Vector3 p1 = startPt + new Vector3(Mathf.Cos(forkAngle) * branchStep, Mathf.Sin(forkAngle) * branchStep, 0f);
            line.SetPosition(1, p1);

            float forkAngle2 = forkAngle + UnityEngine.Random.Range(-0.22f, 0.22f);
            Vector3 p2 = p1 + new Vector3(Mathf.Cos(forkAngle2) * branchStep, Mathf.Sin(forkAngle2) * branchStep, 0f);
            line.SetPosition(2, p2);

            float forkAngle3 = forkAngle2 + UnityEngine.Random.Range(-0.25f, 0.25f);
            Vector3 p3 = p2 + new Vector3(Mathf.Cos(forkAngle3) * (branchStep * 0.85f), Mathf.Sin(forkAngle3) * (branchStep * 0.85f), 0f);
            line.SetPosition(3, p3);
        }

        // 3. Cầu nứt ngang nối liền giữa 2 nhánh chính lân cận (Cross-connecting Bridges)
        for (int k = 0; k < NUM_CROSS_CRACKS; k++, crackIdx++)
        {
            var line = _crackLines[crackIdx];
            line.positionCount = 3;

            int branchA = k;
            int branchB = (k + 1) % NUM_MAIN_CRACKS;

            // Nối đốt 2 của nhánh A sang đốt 1 hoặc 2 của nhánh B
            Vector3 ptA = mainPoints[branchA, 2];
            Vector3 ptB = mainPoints[branchB, (k % 2 == 0) ? 2 : 1];

            Vector3 midPt = Vector3.Lerp(ptA, ptB, 0.5f);
            // Thêm độ lệch zíc zắc tự nhiên
            float midOffset = radius * UnityEngine.Random.Range(-0.07f, 0.07f);
            midPt += new Vector3(-Mathf.Sin(k), Mathf.Cos(k), 0f) * midOffset;

            line.SetPosition(0, ptA);
            line.SetPosition(1, midPt);
            line.SetPosition(2, ptB);
        }
    }

    private void UpdateCracks(float flicker, float alpha)
    {
        Color startCol = new Color(1f, 0.96f, 0.5f, 0.95f * flicker * alpha);
        Color midCol = new Color(1f, 0.45f, 0.08f, 0.85f * flicker * alpha);
        Color endCol = new Color(0.75f, 0.15f, 0.05f, 0.45f * flicker * alpha);

        for (int i = 0; i < _crackLines.Count; i++)
        {
            var line = _crackLines[i];
            if (line == null) continue;

            line.startColor = startCol;
            line.endColor = (i < NUM_MAIN_CRACKS) ? endCol : midCol;

            // Nhánh chính dày hơn, nhánh rẽ và cầu nối mỏng hơn
            float baseW = (i < NUM_MAIN_CRACKS) ? 0.085f : 0.045f;
            float breathe = (baseW + Mathf.Sin(Time.time * 8f + i * 0.7f) * 0.015f) * Mathf.Max(0.1f, alpha);
            line.startWidth = breathe;
            line.endWidth = (i < NUM_MAIN_CRACKS) ? 0.018f : 0.012f;
        }
    }

    private void UpdateFlameBorder(float radius, float flicker, float alpha)
    {
        if (_borderRing == null) return;

        Color borderColor = new Color(1f, 0.45f, 0.05f, 0.9f * flicker * alpha);
        _borderRing.startColor = borderColor;
        _borderRing.endColor = borderColor;
        _borderRing.widthMultiplier = 0.09f + Mathf.Sin(Time.time * 10f) * 0.02f;

        float time = Time.time;
        for (int i = 0; i <= SEGMENTS; i++)
        {
            float angle = (i / (float)SEGMENTS) * Mathf.PI * 2f;
            // Hiệu ứng sóng lửa bập bùng: kết hợp 2 tần số sóng sin/cos
            float wave = Mathf.Sin(angle * 7f + time * 9f) * 0.08f
                       + Mathf.Cos(angle * 13f - time * 13f) * 0.04f;
            float r = radius * (1f + wave);
            Vector3 pt = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            _borderRing.SetPosition(i, pt);
        }
    }
}
