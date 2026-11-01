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
    private EntityManager _entityManager;
    private EntityQuery _boomerangQuery;
    private EntityQuery _catchEventQuery;
    private bool _initialized;

    private static Sprite _boomerangSprite;
    private static Sprite _glowSprite;
    private static Sprite _ringSprite;

    private class BoomerangVisualItem
    {
        public GameObject Root;
        public SpriteRenderer CoreRenderer;
        public SpriteRenderer GlowRenderer;
        public TrailRenderer Trail;
        public LineRenderer TrajectoryLine;
        public bool IsEmpowered;
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
        EnsureSprites();
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

            // Thay đổi màu sắc nếu là đòn Cường Hóa (Empowered Lv4)
            if (visual.IsEmpowered != data.IsEmpowered)
            {
                visual.IsEmpowered = data.IsEmpowered;
                ApplyVisualTheme(visual, data.IsEmpowered);
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
        if (_freePool.Count > 0)
        {
            var item = _freePool[_freePool.Count - 1];
            _freePool.RemoveAt(_freePool.Count - 1);
            return item;
        }

        EnsureSprites();

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
        glowRend.sprite = _glowSprite;
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

        var mat = new Material(Shader.Find("Sprites/Default"));
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
        line.material = new Material(Shader.Find("Sprites/Default"));
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

        ApplyVisualTheme(visual, false);
        return visual;
    }

    private void ApplyVisualTheme(BoomerangVisualItem visual, bool isEmpowered)
    {
        if (isEmpowered)
        {
            // Sắc vàng cam rực lửa (Fiery Sun / Empowered)
            visual.CoreRenderer.color = new Color(1f, 0.95f, 0.7f, 1f);
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
            // Sắc xanh lục lam ngọc bích (Jade Cyan Wind)
            visual.CoreRenderer.color = new Color(0.85f, 1f, 0.95f, 1f);
            visual.GlowRenderer.color = new Color(0.1f, 0.85f, 0.9f, 0.6f);
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

        EnsureSprites();

        var root = new GameObject("Boomerang_CatchRing");
        root.transform.SetParent(transform);
        var rend = root.AddComponent<SpriteRenderer>();
        rend.sprite = _ringSprite;
        rend.sortingOrder = 55;

        return new CatchRingEffect
        {
            Root = root,
            Renderer = rend
        };
    }

    private static void EnsureSprites()
    {
        if (_boomerangSprite != null && _glowSprite != null && _ringSprite != null) return;

        // 1. Tạo Sprite Boomerang 4 cánh khí động học sắc nhọn (64x64)
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
                // Thân phi tiêu và lưỡi viền sáng
                float edgeFactor = Mathf.Clamp01((bladeRadius - dist) * 10f);
                float coreDist = dist / 0.35f;
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
        _boomerangSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        // 2. Tạo Sprite Radial Glow (32x32)
        int glowSize = 32;
        var glowTex = new Texture2D(glowSize, glowSize, TextureFormat.RGBA32, false);
        glowTex.filterMode = FilterMode.Bilinear;
        float gCenter = glowSize * 0.5f;

        for (int y = 0; y < glowSize; y++)
        for (int x = 0; x < glowSize; x++)
        {
            float dx = (x - gCenter) / gCenter;
            float dy = (y - gCenter) / gCenter;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(1f - d);
            alpha = Mathf.Pow(alpha, 2f);
            glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        glowTex.Apply();
        _glowSprite = Sprite.Create(glowTex, new Rect(0, 0, glowSize, glowSize), new Vector2(0.5f, 0.5f), glowSize);

        // 3. Tạo Sprite Ring (64x64) cho hiệu ứng sóng Shockwave
        var ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ringTex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - center) / center;
            float dy = (y - center) / center;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float ringWidth = 0.12f;
            float inner = 0.78f;
            float outer = inner + ringWidth;

            if (d >= inner && d <= outer)
            {
                float mid = (inner + outer) * 0.5f;
                float edge = 1f - Mathf.Abs(d - mid) / (ringWidth * 0.5f);
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, edge));
            }
            else
            {
                ringTex.SetPixel(x, y, Color.clear);
            }
        }
        ringTex.Apply();
        _ringSprite = Sprite.Create(ringTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

