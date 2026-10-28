using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Bridge đồng bộ Trail cho Trảm Kiếm (Slash Sword),
/// render vệt chém hình vòng cung (Slash Arc VFX) khi kiếm tung đòn,
/// và đồng bộ luồng Kiếm Khí (Sword Wave) xuyên thấu ở Cấp 4.
/// Quản lý chặt chẽ vòng đời TrailRenderer để tránh hiện tượng vệt đuôi bị kéo dài xuyên màn hình.
/// </summary>
public class SlashSwordVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _swordQuery;
    private EntityQuery _slashEventQuery;
    private EntityQuery _waveQuery;
    private bool _initialized;

    // Visual item cho từng thanh kiếm
    private class SwordVisual
    {
        public GameObject Root;
        public TrailRenderer Trail;
    }

    // Visual item cho từng đòn Kiếm Khí (Sword Wave) bay xuyên thấu
    private class WaveVisual
    {
        public GameObject Root;
        public LineRenderer CrescentLine;
        public TrailRenderer Trail;
    }

    private readonly List<SwordVisual> _swordVisuals = new List<SwordVisual>();
    private readonly List<SlashArcEffect> _slashArcPool = new List<SlashArcEffect>();
    private readonly List<WaveVisual> _waveVisualPool = new List<WaveVisual>();
    private readonly Dictionary<Entity, WaveVisual> _activeWaveVisuals = new Dictionary<Entity, WaveVisual>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<SlashSwordVisualBridge>() == null)
        {
            var go = new GameObject("[SlashSwordVisualBridge]");
            go.AddComponent<SlashSwordVisualBridge>();
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
            ComponentType.ReadOnly<SlashSwordTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<SlashSwordData>());

        _slashEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SlashVisualEvent>());

        _waveQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SwordWaveTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<SwordWaveData>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        // 1. Xử lý các sự kiện chém quét (Slash Visual Events)
        HandleSlashEvents();

        // 2. Đồng bộ Trail của từng thanh Trảm Kiếm (khớp theo SwordIndex)
        SyncSwordVisuals();

        // 3. Đồng bộ hiệu ứng Kiếm Khí (Sword Wave) ở Cấp 4 (khớp theo Entity ID)
        SyncSwordWaveVisuals();
    }

    private void HandleSlashEvents()
    {
        if (_slashEventQuery.IsEmpty) return;

        var events = _slashEventQuery.ToComponentDataArray<SlashVisualEvent>(Allocator.Temp);
        for (int i = 0; i < events.Length; i++)
        {
            Vector3 center = new Vector3(events[i].Position.x, events[i].Position.y, 0f);
            Vector3 dir = new Vector3(events[i].Direction.x, events[i].Direction.y, 0f);
            float radius = events[i].Radius;

            SpawnSlashArc(center, dir, radius);
        }

        events.Dispose();
        _entityManager.DestroyEntity(_slashEventQuery);
    }

    private void SpawnSlashArc(Vector3 position, Vector3 direction, float radius)
    {
        SlashArcEffect effect = null;
        for (int i = 0; i < _slashArcPool.Count; i++)
        {
            if (!_slashArcPool[i].gameObject.activeSelf)
            {
                effect = _slashArcPool[i];
                break;
            }
        }

        if (effect == null)
        {
            var go = new GameObject($"SlashArc_{_slashArcPool.Count}");
            go.transform.SetParent(transform);
            effect = go.AddComponent<SlashArcEffect>();
            _slashArcPool.Add(effect);
        }

        effect.Setup(position, direction, radius, 0.22f);
    }

    private void SyncSwordVisuals()
    {
        if (_swordQuery.IsEmpty)
        {
            for (int i = 0; i < _swordVisuals.Count; i++)
            {
                if (_swordVisuals[i].Root.activeSelf)
                {
                    _swordVisuals[i].Trail.Clear();
                    _swordVisuals[i].Root.SetActive(false);
                }
            }
            return;
        }

        var transforms = _swordQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var swords = _swordQuery.ToComponentDataArray<SlashSwordData>(Allocator.Temp);

        while (_swordVisuals.Count < Mathf.Max(6, swords.Length))
        {
            _swordVisuals.Add(CreateSwordVisual());
        }

        bool[] used = new bool[_swordVisuals.Count];

        for (int i = 0; i < swords.Length; i++)
        {
            int idx = Mathf.Clamp(swords[i].SwordIndex, 0, _swordVisuals.Count - 1);
            used[idx] = true;
            var vis = _swordVisuals[idx];

            Vector3 targetPos = new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f);

            if (!vis.Root.activeSelf)
            {
                vis.Root.transform.position = targetPos;
                vis.Trail.Clear();
                vis.Root.SetActive(true);
            }
            else if (Vector3.Distance(vis.Root.transform.position, targetPos) > 3.0f)
            {
                // Nhảy cự ly lớn -> Clear trail ngay lập tức để không tạo vạch cắt ngang màn hình
                vis.Trail.Clear();
            }

            vis.Root.transform.position = targetPos;

            // Khi đang lướt áp sát hoặc chém, vệt trail sáng rực và kéo dài hơn
            bool inCombat = (swords[i].State == 1 || swords[i].State == 2);
            vis.Trail.time = inCombat ? 0.18f : 0.08f;
            vis.Trail.widthMultiplier = inCombat ? 0.22f : 0.12f;
        }

        for (int i = 0; i < _swordVisuals.Count; i++)
        {
            if (!used[i] && _swordVisuals[i].Root.activeSelf)
            {
                _swordVisuals[i].Trail.Clear();
                _swordVisuals[i].Root.SetActive(false);
            }
        }

        transforms.Dispose();
        swords.Dispose();
    }

    private void SyncSwordWaveVisuals()
    {
        if (_waveQuery.IsEmpty)
        {
            if (_activeWaveVisuals.Count > 0)
            {
                foreach (var kvp in _activeWaveVisuals)
                {
                    kvp.Value.Trail.Clear();
                    kvp.Value.Root.SetActive(false);
                }
                _activeWaveVisuals.Clear();
            }
            return;
        }

        var waveEntities = _waveQuery.ToEntityArray(Allocator.Temp);
        var transforms = _waveQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var waves = _waveQuery.ToComponentDataArray<SwordWaveData>(Allocator.Temp);

        var currentActiveEntities = new HashSet<Entity>();

        for (int i = 0; i < waveEntities.Length; i++)
        {
            Entity entity = waveEntities[i];
            currentActiveEntities.Add(entity);

            Vector3 pos = new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f);
            float angle = Mathf.Atan2(waves[i].Direction.y, waves[i].Direction.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle - 90f);

            float scale = (transforms[i].Scale > 0.01f) ? transforms[i].Scale : 1f;

            if (_activeWaveVisuals.TryGetValue(entity, out WaveVisual vis))
            {
                // Nếu khoảng cách nhảy bất thường (> 3m) -> Clear trail
                if (Vector3.Distance(vis.Root.transform.position, pos) > 3.0f)
                {
                    vis.Trail.Clear();
                }
                vis.Root.transform.position = pos;
                vis.Root.transform.rotation = rot;
                vis.Root.transform.localScale = Vector3.one * scale;
            }
            else
            {
                // Entity MỚI xuất hiện: Đặt vị trí, scale trước, Clear trail rồi mới Active
                vis = GetOrCreateWaveVisual();
                vis.Root.transform.position = pos;
                vis.Root.transform.rotation = rot;
                vis.Root.transform.localScale = Vector3.one * scale;

                // Tăng độ dày nét vẽ cho đòn Kiếm Khí Siêu Đại của Fusion
                bool isGiantWave = scale > 1.5f;
                vis.Trail.startWidth = isGiantWave ? 0.52f : 0.32f;
                vis.Trail.endWidth = isGiantWave ? 0.04f : 0.02f;
                vis.CrescentLine.startWidth = isGiantWave ? 0.36f : 0.22f;
                vis.CrescentLine.endWidth = isGiantWave ? 0.08f : 0.04f;

                vis.Trail.Clear();
                vis.Root.SetActive(true);
                vis.Trail.emitting = true;

                _activeWaveVisuals[entity] = vis;
            }
        }

        // Tắt và thu hồi visual của các entity kiếm khí đã kết thúc
        if (_activeWaveVisuals.Count > waveEntities.Length)
        {
            var deadEntities = new List<Entity>();
            foreach (var kvp in _activeWaveVisuals)
            {
                if (!currentActiveEntities.Contains(kvp.Key))
                {
                    kvp.Value.Trail.Clear();
                    kvp.Value.Root.SetActive(false);
                    deadEntities.Add(kvp.Key);
                }
            }

            for (int i = 0; i < deadEntities.Count; i++)
            {
                _activeWaveVisuals.Remove(deadEntities[i]);
            }
        }

        waveEntities.Dispose();
        transforms.Dispose();
        waves.Dispose();
    }

    private WaveVisual GetOrCreateWaveVisual()
    {
        for (int i = 0; i < _waveVisualPool.Count; i++)
        {
            if (!_waveVisualPool[i].Root.activeSelf && !_activeWaveVisuals.ContainsValue(_waveVisualPool[i]))
            {
                return _waveVisualPool[i];
            }
        }

        var newVis = CreateWaveVisual();
        _waveVisualPool.Add(newVis);
        return newVis;
    }

    private SwordVisual CreateSwordVisual()
    {
        var root = new GameObject($"SlashSwordVisual_{_swordVisuals.Count}");
        root.transform.SetParent(transform);

        var trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = 0.2f;
        trail.endWidth = 0.01f;
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.material = GetDefaultTrailMaterial();

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 0.45f, 1f), 0f),    // Tím ánh bạc
                new GradientColorKey(new Color(0.35f, 0.85f, 1f), 1f)     // Lam ngọc
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;

        root.SetActive(false);
        return new SwordVisual { Root = root, Trail = trail };
    }

    private WaveVisual CreateWaveVisual()
    {
        var root = new GameObject($"SwordWaveVisual_{_waveVisualPool.Count}");
        root.transform.SetParent(transform);

        // Vòng bán nguyệt hình kiếm khí
        var line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = 0.22f;
        line.endWidth = 0.04f;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.material = GetDefaultTrailMaterial();

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),   // Lõi vàng kim
                new GradientColorKey(new Color(0.8f, 0.4f, 1f), 1f)     // Rìa tím kiếm khí
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.8f, 1f)
            }
        );
        line.colorGradient = gradient;

        // Vẽ đường cong lưỡi liềm (Crescent Arc)
        int segments = 16;
        line.positionCount = segments + 1;
        float arcDeg = 120f;
        float radius = 0.85f;
        for (int i = 0; i <= segments; i++)
        {
            float t = (i / (float)segments) - 0.5f; // -0.5 -> 0.5
            float rad = t * (arcDeg * Mathf.Deg2Rad);
            // Hướng mặt trước là +Y (trong local space)
            float x = Mathf.Sin(rad) * radius;
            float y = Mathf.Cos(rad) * radius * 0.7f;
            line.SetPosition(i, new Vector3(x, y, 0f));
        }

        // Vệt đuôi cho Kiếm Khí (giữ 0.16s thon gọn, sắc nét)
        var trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.16f;
        trail.startWidth = 0.32f;
        trail.endWidth = 0.02f;
        trail.material = GetDefaultTrailMaterial();
        trail.colorGradient = gradient;

        root.SetActive(false);
        return new WaveVisual { Root = root, CrescentLine = line, Trail = trail };
    }

    public static Material GetDefaultTrailMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                        Shader.Find("Sprites/Default") ??
                        Shader.Find("Unlit/Color");
        var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return mat;
    }
}

/// <summary>
/// Hiệu ứng vệt chém bán nguyệt mở rộng cực nhanh rồi tan biến trong 0.22s
/// </summary>
public class SlashArcEffect : MonoBehaviour
{
    private LineRenderer _line;
    private float _duration;
    private float _elapsed;
    private float _maxRadius;
    private Vector3 _direction;
    private const int SEGMENTS = 20;

    private void Awake()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = SEGMENTS + 1;
        _line.material = SlashSwordVisualBridge.GetDefaultTrailMaterial();
        _line.numCornerVertices = 4;
        _line.numCapVertices = 4;
    }

    public void Setup(Vector3 position, Vector3 direction, float radius, float duration = 0.22f)
    {
        transform.position = position;
        _direction = (direction.sqrMagnitude > 0.001f) ? direction.normalized : Vector3.right;
        _maxRadius = radius;
        _duration = duration;
        _elapsed = 0f;
        gameObject.SetActive(true);
        UpdateArc(0f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        UpdateArc(t);

        if (t >= 1f)
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateArc(float t)
    {
        // Ease-out Cubic: vung chém vọt ra rất nhanh frame đầu rồi dừng tan biến
        float easeT = 1f - Mathf.Pow(1f - t, 3f);
        float currentRadius = Mathf.Lerp(0.5f, _maxRadius, easeT);
        float alpha = Mathf.Clamp01((1f - t) * 1.5f);

        _line.startWidth = Mathf.Lerp(0.36f, 0.05f, t);
        _line.endWidth = Mathf.Lerp(0.16f, 0.02f, t);

        Color mainColor = Color.Lerp(
            new Color(1f, 0.95f, 0.7f, alpha),         // Ánh vàng kiếm quang
            new Color(0.85f, 0.45f, 1f, alpha * 0.7f), // Tím bán nguyệt
            t);

        _line.startColor = mainColor;
        _line.endColor = mainColor;

        float baseAngle = Mathf.Atan2(_direction.y, _direction.x);
        float arcSpan = 155f * Mathf.Deg2Rad; // Mở rộng góc chém lên 155 độ tương ứng AOE mới
        float startAngle = baseAngle - arcSpan * 0.5f;

        for (int i = 0; i <= SEGMENTS; i++)
        {
            float prog = (float)i / SEGMENTS;
            float angle = startAngle + prog * arcSpan;

            // Hình bán nguyệt hơi cong vát về 2 cánh
            float rFactor = 1f - Mathf.Abs(prog - 0.5f) * 0.35f;
            float r = currentRadius * rFactor;

            Vector3 pt = transform.position + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            _line.SetPosition(i, pt);
        }
    }
}
