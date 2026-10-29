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
    private static Sprite _ringSprite;
    private static Sprite _lavaCrackSprite;

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
        shadowRend.sortingOrder = -4; // Nằm sát mặt đất dưới player và quái (order 0)
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

    public static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        // Vành đai tròn rỗng (Hollow Ring) với viền mịn màng Anti-Aliased
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center)) / center;
                if (dist > 1.0f || dist < 0.70f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // Độ dày từ 0.70 đến 1.0, đỉnh cao nhất ở giữa (0.85)
                    float normalized = (dist - 0.70f) / (1.0f - 0.70f); // 0 -> 1
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

    public static Sprite GetLavaCrackSprite()
    {
        if (_lavaCrackSprite != null) return _lavaCrackSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 0);
        }

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.46f;

        // Cố định seed để luôn sinh mạng nứt dung nham hoàn hảo giống nhau
        UnityEngine.Random.InitState(1337);

        int numBranches = 8;
        for (int b = 0; b < numBranches; b++)
        {
            float baseAngle = (b / (float)numBranches) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.15f, 0.15f);
            Vector2 p0 = center;

            int steps = 4;
            Vector2 prev = p0;
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                float r = maxR * (t * 0.85f + UnityEngine.Random.Range(0f, 0.15f));
                float angle = baseAngle + UnityEngine.Random.Range(-0.25f, 0.25f);
                Vector2 current = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;

                float width = Mathf.Lerp(4.0f, 1.2f, t);
                DrawFissureSegment(pixels, size, prev, current, width);

                // Nhánh phụ rẽ ra ở đốt giữa
                if (s == 2 && UnityEngine.Random.value > 0.3f)
                {
                    float forkSide = (b % 2 == 0) ? 1f : -1f;
                    float forkAngle = angle + forkSide * UnityEngine.Random.Range(0.6f, 0.9f);
                    float forkLen = maxR * UnityEngine.Random.Range(0.25f, 0.40f);
                    Vector2 forkEnd = current + new Vector2(Mathf.Cos(forkAngle), Mathf.Sin(forkAngle)) * forkLen;
                    DrawFissureSegment(pixels, size, current, forkEnd, width * 0.75f);
                }

                prev = current;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        _lavaCrackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _lavaCrackSprite;
    }

    private static void DrawFissureSegment(Color32[] pixels, int size, Vector2 p1, Vector2 p2, float width)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p1.x, p2.x) - width - 1));
        int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(p1.x, p2.x) + width + 1));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p1.y, p2.y) - width - 1));
        int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(p1.y, p2.y) + width + 1));

        Vector2 ab = p2 - p1;
        float ab2 = Vector2.Dot(ab, ab);

        Color32 coreCol = new Color32(255, 245, 150, 255); // Lõi vàng sáng
        Color32 glowCol = new Color32(255, 80, 10, 200);   // Viền đỏ cam magma

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = ab2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - p1, ab) / ab2) : 0f;
                Vector2 closest = p1 + t * ab;
                float dist = Vector2.Distance(p, closest);

                if (dist <= width)
                {
                    int idx = y * size + x;
                    float normDist = dist / width; // 0 (tâm dòng) -> 1 (mép ngoài)

                    Color32 c;
                    if (normDist <= 0.35f)
                    {
                        c = coreCol;
                    }
                    else
                    {
                        float blend = (normDist - 0.35f) / 0.65f;
                        c = new Color32(
                            (byte)Mathf.Lerp(coreCol.r, glowCol.r, blend),
                            (byte)Mathf.Lerp(coreCol.g, glowCol.g, blend),
                            (byte)Mathf.Lerp(coreCol.b, glowCol.b, blend),
                            (byte)(glowCol.a * (1f - blend))
                        );
                    }

                    Color32 old = pixels[idx];
                    pixels[idx] = new Color32(
                        (byte)Mathf.Max(old.r, c.r),
                        (byte)Mathf.Max(old.g, c.g),
                        (byte)Mathf.Max(old.b, c.b),
                        (byte)Mathf.Max(old.a, c.a)
                    );
                }
            }
        }
    }
}

/// <summary>
/// Hiệu ứng sóng xung kích nổ nhiệt (Explosion Shockwave) của Cầu Lửa.
/// 100% GPU-Accelerated với SpriteRenderer, không tốn bất kỳ CPU Mesh recalculation nào!
/// </summary>
public class FireballExplosionEffect : MonoBehaviour
{
    private SpriteRenderer _outerRing;
    private SpriteRenderer _innerBlast;
    private float _maxRadius;
    private float _duration = 0.40f;
    private float _elapsed = 0f;
    private bool _isMain;

    private void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        var blastObj = new GameObject("InnerBlast");
        blastObj.transform.SetParent(transform, false);
        _innerBlast = blastObj.AddComponent<SpriteRenderer>();
        _innerBlast.sprite = FireballVisualBridge.GetCircleSprite();
        _innerBlast.sortingOrder = 8;
        if (mat != null) _innerBlast.material = mat;

        var ringObj = new GameObject("OuterRing");
        ringObj.transform.SetParent(transform, false);
        _outerRing = ringObj.AddComponent<SpriteRenderer>();
        _outerRing.sprite = FireballVisualBridge.GetRingSprite();
        _outerRing.sortingOrder = 9;
        if (mat != null) _outerRing.material = mat;
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

        // Vòng shockwave nổ bung
        if (_outerRing != null)
        {
            _outerRing.transform.localScale = Vector3.one * (currentRadius * 2.1f);
            Color ringColor = Color.Lerp(
                new Color(1f, 0.95f, 0.4f, alpha),
                new Color(1f, 0.25f, 0.05f, alpha * 0.8f),
                t);
            _outerRing.color = ringColor;
        }

        // Đĩa sáng nổ nhiệt bên trong
        if (_innerBlast != null)
        {
            _innerBlast.transform.localScale = Vector3.one * (currentRadius * 1.8f);
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
/// 3. Lưới nứt toác dung nham rực sáng (Procedural Magma Fissures Sprite - GPU Accelerated)
/// 4. Lớp vân nứt phụ xoay góc tạo độ chi tiết cao (Detail Fissures)
/// 5. Lõi nhiệt bốc cháy bên trong (Inner Ember)
/// 6. Vành đai lửa xoay tròn lượn sóng động (Turbulent Border Aura)
/// 7. Hệ hạt tàn lửa bay lượn bốc lên trời (Rising Sparks & Embers ParticleSystem)
/// 100% GPU-accelerated với SpriteRenderer, loại bỏ triệt để LineRenderer CPU bottleneck!
/// </summary>
public class FireZoneVisualEffect : MonoBehaviour
{
    private SpriteRenderer _charredGround;
    private SpriteRenderer _groundFire;
    private SpriteRenderer _cracksRenderer;
    private SpriteRenderer _cracksDetail;
    private SpriteRenderer _innerEmber;
    private SpriteRenderer _borderAura;
    private ParticleSystem _emberParticles;
    private float _radius;

    private void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;

        // 1. Lớp đất sém đen khói (Charred Earth Decal) - Nằm sát mặt đất dưới Player và Quái (sortingOrder 0)
        var charObj = new GameObject("CharredGround");
        charObj.transform.SetParent(transform, false);
        _charredGround = charObj.AddComponent<SpriteRenderer>();
        _charredGround.sprite = FireballVisualBridge.GetCircleSprite();
        _charredGround.sortingOrder = -10;
        if (mat != null) _charredGround.material = mat;

        // 2. Vùng dung nham mặt đất (Magma Pool)
        var groundObj = new GameObject("GroundFire");
        groundObj.transform.SetParent(transform, false);
        _groundFire = groundObj.AddComponent<SpriteRenderer>();
        _groundFire.sprite = FireballVisualBridge.GetCircleSprite();
        _groundFire.sortingOrder = -9;
        if (mat != null) _groundFire.material = mat;

        // 3. Mạng lưới các vết nứt dung nham nứt toác (Lava Fissures - GPU Sprite)
        var crackObj = new GameObject("LavaCracks");
        crackObj.transform.SetParent(transform, false);
        _cracksRenderer = crackObj.AddComponent<SpriteRenderer>();
        _cracksRenderer.sprite = FireballVisualBridge.GetLavaCrackSprite();
        _cracksRenderer.sortingOrder = -8;
        if (mat != null) _cracksRenderer.material = mat;

        // 4. Lớp vết nứt phụ xoay lệch góc tăng độ dày đặc
        var crackDetailObj = new GameObject("LavaCracksDetail");
        crackDetailObj.transform.SetParent(transform, false);
        crackDetailObj.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _cracksDetail = crackDetailObj.AddComponent<SpriteRenderer>();
        _cracksDetail.sprite = FireballVisualBridge.GetLavaCrackSprite();
        _cracksDetail.sortingOrder = -8;
        if (mat != null) _cracksDetail.material = mat;

        // 5. Lõi nhiệt bốc cháy bên trong (Inner Ember)
        var emberObj = new GameObject("InnerEmber");
        emberObj.transform.SetParent(transform, false);
        _innerEmber = emberObj.AddComponent<SpriteRenderer>();
        _innerEmber.sprite = FireballVisualBridge.GetCircleSprite();
        _innerEmber.sortingOrder = -7;
        if (mat != null) _innerEmber.material = mat;

        // 6. Viền hào quang lửa ngoài (Border Aura Ring)
        var borderObj = new GameObject("BorderAura");
        borderObj.transform.SetParent(transform, false);
        _borderAura = borderObj.AddComponent<SpriteRenderer>();
        _borderAura.sprite = FireballVisualBridge.GetRingSprite();
        _borderAura.sortingOrder = -6;
        if (mat != null) _borderAura.material = mat;

        // 7. Hệ hạt tàn lửa bay bổng lên trời (Rising Embers Particle System)
        InitParticleSystem(shader);
    }

    private void InitParticleSystem(Shader shader)
    {
        var psObj = new GameObject("EmberParticles");
        psObj.transform.SetParent(transform, false);
        _emberParticles = psObj.AddComponent<ParticleSystem>();
        _emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var psr = psObj.GetComponent<ParticleSystemRenderer>();

        if (shader != null)
        {
            Material pMat = new Material(shader);
            pMat.mainTexture = FireballVisualBridge.GetCircleSprite().texture;
            psr.material = pMat;
        }
        psr.sortingOrder = -5;

        var main = _emberParticles.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f; // Tự động bốc nổi lên trên

        var emission = _emberParticles.emission;
        emission.rateOverTime = 10f; // Nhẹ nhàng, êm ái

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

        if (_emberParticles != null)
        {
            var shape = _emberParticles.shape;
            shape.radius = radius * 0.8f;
            _emberParticles.Clear();
            _emberParticles.Play();
        }

        UpdateVisuals(radius, 1f, 1f);
    }

    public void UpdatePositionAndRadius(Vector3 position, float radius, float durationRemaining)
    {
        transform.position = position;
        _radius = radius;

        float flicker = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
        float alpha = Mathf.Clamp01(durationRemaining * 2.2f); // Mờ dần khi sắp hết thời gian

        UpdateVisuals(radius, flicker, alpha);

        // Ngừng phun hạt khi sắp tắt
        if (_emberParticles != null && durationRemaining <= 0.25f && _emberParticles.isPlaying)
        {
            _emberParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateVisuals(float radius, float flicker, float alpha)
    {
        // 1. Lớp đất sém đen
        if (_charredGround != null)
        {
            _charredGround.transform.localScale = Vector3.one * (radius * 2.3f);
            _charredGround.color = new Color(0.06f, 0.03f, 0.03f, 0.72f * alpha);
        }

        // 2. Vùng dung nham mặt đất
        if (_groundFire != null)
        {
            float poolPulse = 1f + Mathf.Sin(Time.time * 6f) * 0.05f;
            _groundFire.transform.localScale = Vector3.one * (radius * 2.0f * poolPulse);
            _groundFire.color = new Color(0.95f, 0.32f, 0.05f, 0.52f * flicker * alpha);
        }

        // 3. Mạng vết nứt chính
        if (_cracksRenderer != null)
        {
            _cracksRenderer.transform.localScale = Vector3.one * (radius * 2.05f);
            _cracksRenderer.color = new Color(1f, 0.90f, 0.40f, 0.95f * flicker * alpha);
        }

        // 4. Mạng vết nứt phụ
        if (_cracksDetail != null)
        {
            _cracksDetail.transform.localScale = Vector3.one * (radius * 1.85f);
            _cracksDetail.color = new Color(1f, 0.45f, 0.08f, 0.70f * flicker * alpha);
        }

        // 5. Lõi nhiệt bốc cháy bên trong (quay chậm)
        if (_innerEmber != null)
        {
            float emberPulse = 0.75f + Mathf.PingPong(Time.time * 4.5f, 0.25f);
            _innerEmber.transform.localScale = Vector3.one * (radius * 1.35f * emberPulse);
            _innerEmber.color = new Color(1f, 0.88f, 0.25f, 0.70f * flicker * alpha);
            _innerEmber.transform.Rotate(0f, 0f, -30f * Time.deltaTime);
        }

        // 6. Viền hào quang lửa ngoài (xoay nhẹ)
        if (_borderAura != null)
        {
            float auraPulse = 1f + Mathf.Sin(Time.time * 8f) * 0.04f;
            _borderAura.transform.localScale = Vector3.one * (radius * 2.15f * auraPulse);
            _borderAura.color = new Color(1f, 0.45f, 0.05f, 0.85f * flicker * alpha);
            _borderAura.transform.Rotate(0f, 0f, 25f * Time.deltaTime);
        }
    }
}
