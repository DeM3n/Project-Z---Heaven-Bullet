using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge quản lý toàn bộ hiệu ứng hình ảnh (VFX) của Kỹ năng Boomerang:
/// 1. Tự sinh Sprite Phi Tiêu 4 cánh sắc nhọn khí động học (Procedural Sprite) có lưỡi phát sáng.
/// 2. Tự động scale kích thước visual khớp 100% với bán kính Hitbox (Cấp 1: 0.55m -> Cấp 2+: 0.85m).
/// 3. Vệt đuôi TrailRenderer uốn lượn: Màu Lục Ngọc Lam (Jade-Cyan) cho đạn thường, Vàng Cam Huyết Kiếm (Fiery Gold) cho đạn Cường Hóa Lv4.
/// 4. Hiệu ứng Sóng Năng Lượng Chụp Dính (Catch Shockwave Ring) bùng nổ khi Player hứng dính Boomerang.
/// </summary>
public class BoomerangVisualBridge : MonoBehaviour
{
    [Header("Custom Visual Settings")]
    [Tooltip("Sprite Boomerang tùy chỉnh. Nếu để trống, hệ thống sẽ tự động lấy từ Boomerang.prefab hoặc Assets/Sprite/Boomerang.png.")]
    public Sprite CustomBoomerangSprite;

    private EntityManager _entityManager;
    private EntityQuery _boomerangQuery;
    private EntityQuery _catchEventQuery;
    private bool _initialized;

    private static Sprite _boomerangSprite;
    private static bool _isCustomSprite;

    private class BoomerangVisualItem
    {
        public GameObject Root;
        public SpriteRenderer CoreRenderer;
        public SpriteRenderer GlowRenderer;
        public TrailRenderer Trail;
        public LineRenderer TrajectoryLine;
        public bool IsEmpowered;
        public bool IsThunder;
    }

    private class CatchRingEffect
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public float Lifetime;
        public float MaxLifetime;
        public float StartRadius;
        public float EndRadius;
        public Color Color;
    }

    private readonly Dictionary<Entity, BoomerangVisualItem> _activeVisuals = new Dictionary<Entity, BoomerangVisualItem>();
    private readonly List<BoomerangVisualItem> _freePool = new List<BoomerangVisualItem>();
    private readonly List<CatchRingEffect> _activeRings = new List<CatchRingEffect>();
    private readonly List<CatchRingEffect> _freeRingPool = new List<CatchRingEffect>();

    private readonly HashSet<Entity> _currentEntitySet = new HashSet<Entity>();
    private readonly List<Entity> _toRemoveList = new List<Entity>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<BoomerangVisualBridge>() == null)
        {
            var go = new GameObject("[BoomerangVisualBridge]");
            go.AddComponent<BoomerangVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        _boomerangSprite = null; // Luôn làm mới để bắt kịp thay đổi từ Prefab hoặc Sprite folder
        EnsureSprites(CustomBoomerangSprite);
        PrewarmPools();
    }

    private void PrewarmPools()
    {
        for (int i = 0; i < 8; i++)
        {
            var item = CreateVisualItem();
            item.Root.SetActive(false);
            _freePool.Add(item);
        }

        for (int i = 0; i < 8; i++)
        {
            var ring = CreateRing();
            ring.Root.SetActive(false);
            _freeRingPool.Add(ring);
        }
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _boomerangQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<BoomerangTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<BoomerangProjectileData>()
        );

        _catchEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<BoomerangCatchEvent>()
        );

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        UpdateBoomerangVisuals();
        ProcessCatchEvents();
        UpdateCatchRings();
    }

    private void UpdateBoomerangVisuals()
    {
        if (_boomerangQuery.IsEmpty)
        {
            // Trả toàn bộ về pool nếu không có boomerang nào đang bay
            if (_activeVisuals.Count > 0)
            {
                foreach (var kvp in _activeVisuals)
                {
                    if (kvp.Value.TrajectoryLine != null) kvp.Value.TrajectoryLine.enabled = false;
                    if (kvp.Value.Root != null) kvp.Value.Root.SetActive(false);
                    _freePool.Add(kvp.Value);
                }
                _activeVisuals.Clear();
            }
            return;
        }

        using var entities = _boomerangQuery.ToEntityArray(Allocator.Temp);
        using var transforms = _boomerangQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var projDataArray = _boomerangQuery.ToComponentDataArray<BoomerangProjectileData>(Allocator.Temp);

        _currentEntitySet.Clear();

        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            _currentEntitySet.Add(entity);

            LocalTransform trans = transforms[i];
            BoomerangProjectileData data = projDataArray[i];

            if (!_activeVisuals.TryGetValue(entity, out var visual))
            {
                visual = GetOrCreateVisualItem();
                _activeVisuals[entity] = visual;

                // Reset Trail khi vừa xuất hiện
                visual.Trail.Clear();
                visual.Root.transform.position = trans.Position;
            }

            visual.Root.SetActive(true);
            visual.Root.transform.position = trans.Position;
            visual.Root.transform.rotation = trans.Rotation;

            // Đồng bộ kích thước visual theo đúng bán kính Hitbox (HitRadius)
            float visualScale = data.HitRadius * 2.3f;
            visual.Root.transform.localScale = new Vector3(visualScale, visualScale, 1f);

            // Thay đổi màu sắc nếu là đòn Cường Hóa (Empowered Lv4) hoặc Hợp Thể Lôi Tiêu (Thunder Fusion)
            bool isThunder = _entityManager.HasComponent<BoomerangLightningData>(entity);
            if (visual.IsEmpowered != data.IsEmpowered || visual.IsThunder != isThunder)
            {
                visual.IsEmpowered = data.IsEmpowered;
                visual.IsThunder = isThunder;
                ApplyVisualTheme(visual, data.IsEmpowered, isThunder);
            }

            // Hiển thị đường thẳng dẫn đường (Trajectory Aim Line) khi Boomerang đang quay về
            if (visual.TrajectoryLine != null)
            {
                if (data.State == BoomerangFlightState.Returning)
                {
                    visual.TrajectoryLine.enabled = true;
                    Vector3 startPos = trans.Position;
                    Vector3 endPos = trans.Position + new float3(data.Direction.x, data.Direction.y, 0f) * 22f;
                    visual.TrajectoryLine.SetPosition(0, startPos);
                    visual.TrajectoryLine.SetPosition(1, endPos);
                }
                else
                {
                    visual.TrajectoryLine.enabled = false;
                }
            }
        }

        // Dọn dẹp các entity đã bị hủy trong ECS
        _toRemoveList.Clear();
        foreach (var kvp in _activeVisuals)
        {
            if (!_currentEntitySet.Contains(kvp.Key))
            {
                _toRemoveList.Add(kvp.Key);
            }
        }

        for (int i = 0; i < _toRemoveList.Count; i++)
        {
            Entity e = _toRemoveList[i];
            var visual = _activeVisuals[e];
            if (visual.TrajectoryLine != null) visual.TrajectoryLine.enabled = false;
            if (visual.Root != null) visual.Root.SetActive(false);
            _freePool.Add(visual);
            _activeVisuals.Remove(e);
        }
    }

    private void ProcessCatchEvents()
    {
        if (_catchEventQuery.IsEmpty) return;

        using var events = _catchEventQuery.ToComponentDataArray<BoomerangCatchEvent>(Allocator.Temp);
        using var entities = _catchEventQuery.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < events.Length; i++)
        {
            var evt = events[i];
            SpawnCatchShockwave(evt.Position, evt.IsEmpowered);
        }

        // Dọn dẹp các entity event đã xử lý
        _entityManager.DestroyEntity(_catchEventQuery);
    }

    private void SpawnCatchShockwave(float3 position, bool isEmpowered)
    {
        CatchRingEffect ring = GetOrCreateRing();
        ring.Root.transform.position = position;
        ring.Root.SetActive(true);
        ring.Lifetime = 0f;
        ring.MaxLifetime = 0.35f;
        ring.StartRadius = 0.4f;
        ring.EndRadius = isEmpowered ? 2.8f : 1.9f;

        if (isEmpowered)
        {
            ring.Color = new Color(1f, 0.85f, 0.2f, 0.95f); // Vàng kim chói lọi
        }
        else
        {
            ring.Color = new Color(0.2f, 1f, 0.7f, 0.9f); // Xanh lục bảo ngọc bích
        }

        _activeRings.Add(ring);
    }

    private void UpdateCatchRings()
    {
        float dt = Time.deltaTime;
        for (int i = _activeRings.Count - 1; i >= 0; i--)
        {
            var ring = _activeRings[i];
            ring.Lifetime += dt;

            if (ring.Lifetime >= ring.MaxLifetime)
            {
                ring.Root.SetActive(false);
                _freeRingPool.Add(ring);
                _activeRings.RemoveAt(i);
                continue;
            }

            float progress = ring.Lifetime / ring.MaxLifetime;
            float currentRadius = Mathf.Lerp(ring.StartRadius, ring.EndRadius, Mathf.Sin(progress * Mathf.PI * 0.5f));
            ring.Root.transform.localScale = new Vector3(currentRadius * 2f, currentRadius * 2f, 1f);

            Color c = ring.Color;
            c.a = Mathf.Lerp(ring.Color.a, 0f, progress);
            ring.Renderer.color = c;
        }
    }

    private BoomerangVisualItem GetOrCreateVisualItem()
    {
        EnsureSprites(CustomBoomerangSprite);

        if (_freePool.Count > 0)
        {
            var item = _freePool[_freePool.Count - 1];
            _freePool.RemoveAt(_freePool.Count - 1);

            // Đồng bộ lại Sprite nếu người dùng vừa đổi Sprite mới trong Prefab/Editor
            if (item.CoreRenderer != null && item.CoreRenderer.sprite != _boomerangSprite)
            {
                item.CoreRenderer.sprite = _boomerangSprite;
            }
            return item;
        }

        return CreateVisualItem();
    }

    private BoomerangVisualItem CreateVisualItem()
    {
        EnsureSprites(CustomBoomerangSprite);

        var root = new GameObject("Boomerang_Visual");
        root.transform.SetParent(transform);

        // 1. Sprite Lõi Boomerang
        var coreObj = new GameObject("Core");
        coreObj.transform.SetParent(root.transform, false);
        var coreRend = coreObj.AddComponent<SpriteRenderer>();
        coreRend.sprite = _boomerangSprite;
        coreRend.sortingOrder = 45;

        // 2. Sprite Hào Quang Phát Sáng (Glow)
        var glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(root.transform, false);
        var glowRend = glowObj.AddComponent<SpriteRenderer>();
        glowRend.sprite = SharedVisualAssets.RadialGlowSprite;
        glowRend.sortingOrder = 44;
        glowObj.transform.localScale = Vector3.one * 1.45f;

        // 3. Trail Renderer uốn lượn
        var trailObj = new GameObject("Trail");
        trailObj.transform.SetParent(root.transform, false);
        var trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = 0.45f;
        trail.endWidth = 0.02f;
        trail.sortingOrder = 43;

        var mat = SharedVisualAssets.SpriteUnlitMaterial;
        trail.material = mat;

        // 4. Line Renderer đường dẫn quỹ đạo bay về (Return Trajectory Aim Line)
        var lineObj = new GameObject("TrajectoryLine");
        lineObj.transform.SetParent(root.transform, false);
        var line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.16f;
        line.endWidth = 0.04f;
        line.sortingOrder = 41; // Nằm sau Trail và Core
        line.material = mat;
        line.enabled = false;

        var visual = new BoomerangVisualItem
        {
            Root = root,
            CoreRenderer = coreRend,
            GlowRenderer = glowRend,
            Trail = trail,
            TrajectoryLine = line,
            IsEmpowered = false
        };

        ApplyVisualTheme(visual, false, false);
        return visual;
    }

    private void ApplyVisualTheme(BoomerangVisualItem visual, bool isEmpowered, bool isThunder = false)
    {
        if (isThunder)
        {
            // Sắc Lôi Điện Thiên Kiếp (Cyan Neon kết hợp Vàng Kim Hợp Thể)
            visual.CoreRenderer.color = new Color(0.9f, 1f, 1f, 1f);
            visual.GlowRenderer.enabled = true;
            visual.GlowRenderer.color = isEmpowered ? new Color(1f, 0.85f, 0.2f, 0.85f) : new Color(0f, 0.95f, 1f, 0.75f);
            visual.Trail.startColor = isEmpowered ? new Color(1f, 0.9f, 0.3f, 0.95f) : new Color(0.1f, 0.95f, 1f, 0.95f);
            visual.Trail.endColor = new Color(0f, 0.4f, 1f, 0f);
            if (visual.TrajectoryLine != null)
            {
                visual.TrajectoryLine.startColor = new Color(0.2f, 0.95f, 1f, 0.9f);
                visual.TrajectoryLine.endColor = new Color(0f, 0.3f, 0.8f, 0f);
            }
        }
        else if (isEmpowered)
        {
            // Sắc vàng cam rực lửa (Fiery Sun / Empowered)
            visual.CoreRenderer.color = _isCustomSprite ? new Color(1f, 0.9f, 0.7f, 1f) : new Color(1f, 0.95f, 0.7f, 1f);
            visual.GlowRenderer.enabled = true;
            visual.GlowRenderer.color = new Color(1f, 0.55f, 0.1f, 0.65f);
            visual.Trail.startColor = new Color(1f, 0.85f, 0.2f, 0.95f);
            visual.Trail.endColor = new Color(1f, 0.2f, 0.05f, 0f);
            if (visual.TrajectoryLine != null)
            {
                visual.TrajectoryLine.startColor = new Color(1f, 0.85f, 0.2f, 0.85f);
                visual.TrajectoryLine.endColor = new Color(1f, 0.4f, 0.1f, 0f);
            }
        }
        else
        {
            // Đạn thường: Giữ nguyên màu gốc của ảnh custom để pixel art hiển thị chính xác 100%
            visual.CoreRenderer.color = _isCustomSprite ? Color.white : new Color(0.85f, 1f, 0.95f, 1f);
            visual.GlowRenderer.enabled = !_isCustomSprite; // Ẩn glow hình tròn thô khi dùng ảnh vẽ riêng
            visual.GlowRenderer.color = new Color(0.1f, 0.85f, 0.9f, 0.4f);
            visual.Trail.startColor = new Color(0.2f, 1f, 0.75f, 0.9f);
            visual.Trail.endColor = new Color(0.05f, 0.4f, 0.9f, 0f);
            if (visual.TrajectoryLine != null)
            {
                visual.TrajectoryLine.startColor = new Color(0.2f, 1f, 0.75f, 0.85f);
                visual.TrajectoryLine.endColor = new Color(0.05f, 0.6f, 0.9f, 0f);
            }
        }
    }

    private CatchRingEffect GetOrCreateRing()
    {
        if (_freeRingPool.Count > 0)
        {
            var item = _freeRingPool[_freeRingPool.Count - 1];
            _freeRingPool.RemoveAt(_freeRingPool.Count - 1);
            return item;
        }

        return CreateRing();
    }

    private CatchRingEffect CreateRing()
    {
        EnsureSprites(CustomBoomerangSprite);

        var root = new GameObject("Boomerang_CatchRing");
        root.transform.SetParent(transform);
        var rend = root.AddComponent<SpriteRenderer>();
        rend.sprite = SharedVisualAssets.RingSprite;
        rend.sortingOrder = 55;

        return new CatchRingEffect
        {
            Root = root,
            Renderer = rend
        };
    }

    private static void EnsureSprites(Sprite customSprite = null)
    {
        if (_boomerangSprite == null)
        {
            _boomerangSprite = LoadBoomerangSprite(customSprite);
        }
    }

    /// <summary>
    /// Tự động tìm và nạp Sprite Boomerang theo thứ tự ưu tiên:
    /// 1. Sprite chỉ định trong Inspector.
    /// 2. SpriteRenderer gắn trên Prefab (Assets/Prefabs/Skills/Boomerang.prefab).
    /// 3. File ảnh import trong Assets/Sprite/Boomerang.png.
    /// 4. Procedural Sprite 4 cánh dự phòng (Fallback).
    /// </summary>
    public static Sprite LoadBoomerangSprite(Sprite customSprite = null)
    {
        if (customSprite != null)
        {
            _isCustomSprite = true;
            return customSprite;
        }

#if UNITY_EDITOR
        // 1. Kiểm tra SpriteRenderer trong Boomerang.prefab
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Skills/Boomerang.prefab");
        if (prefab != null)
        {
            var sr = prefab.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                _isCustomSprite = true;
                return sr.sprite;
            }
        }

        // 2. Kiểm tra trực tiếp file ảnh tại Assets/Sprite/Boomerang.png
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprite/Boomerang.png");
        if (assets != null && assets.Length > 0)
        {
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    _isCustomSprite = true;
                    return s;
                }
            }
        }
#endif

        // 3. Nạp từ Resources (cho bản build runtime độc lập nếu có)
        var fromRes = Resources.Load<Sprite>("Boomerang");
        if (fromRes != null)
        {
            _isCustomSprite = true;
            return fromRes;
        }

        // 4. Fallback: Sinh Procedural Sprite 4 cánh khí động học
        _isCustomSprite = false;
        return CreateProceduralBoomerangSprite();
    }

    private static Sprite CreateProceduralBoomerangSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - center) / center;
            float dy = (y - center) / center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            // Tạo hình dạng phi tiêu chữ thập 4 cánh uốn cong
            float angle = Mathf.Atan2(dy, dx);
            float petal = Mathf.Abs(Mathf.Cos(angle * 2f)); // 4 cánh
            float bladeRadius = 0.3f + 0.65f * Mathf.Pow(petal, 3f);

            if (dist <= bladeRadius && dist <= 0.95f)
            {
                float edgeFactor = Mathf.Clamp01((bladeRadius - dist) * 10f);
                Color col = Color.Lerp(new Color(0.1f, 0.25f, 0.35f, 1f), new Color(0.3f, 1f, 0.85f, 1f), edgeFactor);
                if (dist < 0.22f) col = new Color(0.9f, 1f, 1f, 1f); // Tâm ngọc phát sáng

                tex.SetPixel(x, y, col);
            }
            else
            {
                tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

