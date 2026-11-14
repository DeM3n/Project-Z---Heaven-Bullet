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
    [Tooltip("Thời gian tồn tại của vệt đuôi (s)")]
    [SerializeField] private float trailTime = 0.15f;
    [Tooltip("Độ rộng của vệt trail ở chuôi kiếm (giữ thon gọn vừa khít với thân kiếm ~0.11m)")]
    [SerializeField] private float startWidth = 0.11f;
    [Tooltip("Độ rộng vệt trail ở đuôi nhọn")]
    [SerializeField] private float endWidth = 0.01f;
    [Tooltip("Khoảng cách dời điểm phát trail từ tâm kiếm về phía chuôi kiếm (m)")]
    [SerializeField] private float hiltOffset = 0.35f;
    [Tooltip("Đảo chiều offset chuôi kiếm nếu mũi kiếm quay theo chiều ngược lại")]
    [SerializeField] private bool invertHiltDirection = false;
    [Tooltip("Màu vệt trail bình thường (tông đỏ huyết kiếm rực rỡ)")]
    [SerializeField] private Color normalColor = new Color(1f, 0.15f, 0.2f, 0.95f);
    [Tooltip("Màu vệt trail khi Ultimate (đỏ cam rực lửa)")]
    [SerializeField] private Color ultimateColor = new Color(1f, 0.42f, 0.12f, 1f);
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

    private readonly Dictionary<Entity, SwordVisualItem> _activeVisuals = new Dictionary<Entity, SwordVisualItem>();
    private readonly List<SwordVisualItem> _visualPool = new List<SwordVisualItem>();
    private readonly List<SwordShockwaveEffect> _shockwavePool = new List<SwordShockwaveEffect>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<FlyingSwordTrailBridge>() == null)
        {
            var go = new GameObject("[FlyingSwordTrailBridge]");
            go.AddComponent<FlyingSwordTrailBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        PrewarmPools();
    }

    private void PrewarmPools()
    {
        // 1. Pre-warm Phi Kiếm Trails (6 kiếm)
        for (int i = 0; i < 6; i++)
        {
            var item = CreateNewSwordVisual();
            item.Trail.Clear();
            item.Root.SetActive(false);
            _visualPool.Add(item);
        }

        // 2. Pre-warm Shockwave Effects (8 hiệu ứng nổ)
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject($"SwordShockwave_{i}");
            go.transform.SetParent(transform);
            var shockwave = go.AddComponent<SwordShockwaveEffect>();
            go.SetActive(false);
            _shockwavePool.Add(shockwave);
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
            ComponentType.ReadOnly<SkillRuntimeData>(),
            ComponentType.Exclude<CloneSkillTag>());

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

        // 3. Đồng bộ Trail và Aura trạng thái của từng thanh kiếm (khớp theo Entity ID để độc lập tuyệt đối giữa Player và Clones)
        if (_swordQuery.IsEmpty)
        {
            if (_activeVisuals.Count > 0)
            {
                foreach (var kvp in _activeVisuals)
                {
                    if (kvp.Value != null && kvp.Value.Root.activeSelf)
                    {
                        kvp.Value.Trail.Clear();
                        kvp.Value.Trail.emitting = false;
                        kvp.Value.Root.SetActive(false);
                        _visualPool.Add(kvp.Value);
                    }
                }
                _activeVisuals.Clear();
            }
            return;
        }

        var swordEntities = _swordQuery.ToEntityArray(Allocator.Temp);
        var transforms = _swordQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var swordDataArray = _swordQuery.ToComponentDataArray<FlyingSwordData>(Allocator.Temp);
        int swordCount = swordEntities.Length;

        var currentActiveEntities = new HashSet<Entity>();

        for (int i = 0; i < swordCount; i++)
        {
            Entity entity = swordEntities[i];
            currentActiveEntities.Add(entity);

            Vector3 targetPos = new Vector3(
                transforms[i].Position.x,
                transforms[i].Position.y,
                transforms[i].Position.z);

            // Dời điểm phát Trail về đúng vị trí Chuôi Kiếm (Hilt) phía sau
            Vector3 hiltDirection = ((Quaternion)transforms[i].Rotation) * (invertHiltDirection ? Vector3.down : Vector3.up);
            Vector3 trailEmitPos = targetPos + hiltDirection * hiltOffset;

            if (!_activeVisuals.TryGetValue(entity, out var item))
            {
                if (_visualPool.Count > 0)
                {
                    int last = _visualPool.Count - 1;
                    item = _visualPool[last];
                    _visualPool.RemoveAt(last);
                }
                else
                {
                    item = CreateNewSwordVisual();
                }

                item.Root.transform.position = trailEmitPos;
                item.Root.transform.rotation = transforms[i].Rotation;
                item.Trail.Clear();
                item.Root.SetActive(true);
                _activeVisuals[entity] = item;
            }

            // Dịch chuyển tức thời quá xa (> 4m) -> Clear trail để tránh vạch cắt màn hình
            if (Vector3.Distance(item.Root.transform.position, trailEmitPos) > 4f)
            {
                item.Trail.Clear();
            }

            item.Root.transform.position = trailEmitPos;
            item.Root.transform.rotation = transforms[i].Rotation;
            item.Trail.emitting = true;

            // --- Hiệu ứng Ultimate Ready & Ultimate Active ---
            bool isUltimateStrike = swordDataArray[i].IsUltimate;
            bool isUltimateReady = (skillLevel >= 4 && swordDataArray[i].UltimateTimer <= 0f);

            if (isUltimateStrike)
            {
                // Đang trong cú lướt Ultimate: Vệt đuôi đỏ cam rực lửa cực đại (width 0.28f)
                if (!item.IsUltimateMode)
                {
                    item.IsUltimateMode = true;
                    ApplyTrailGradient(item.Trail, ultimateColor, new Color(0.85f, 0.05f, 0.05f, 0f), 0.28f, 0.02f);
                }
                item.AuraObj.SetActive(true);
                item.AuraObj.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                item.AuraRenderer.color = new Color(1f, 0.35f, 0.15f, 0.95f);
                item.AuraObj.transform.localScale = Vector3.one * (1.6f + Mathf.PingPong(Time.time * 6f, 0.4f));
            }
            else if (isUltimateReady)
            {
                // Ultimate đã tích tụ xong: Aura đỏ cam xoay quanh chuôi kiếm, báo hiệu sẵn sàng
                if (!item.IsUltimateMode)
                {
                    item.IsUltimateMode = true;
                    ApplyTrailGradient(item.Trail, new Color(1f, 0.45f, 0.15f, 0.95f), normalColor, 0.16f, 0.01f);
                }
                item.AuraObj.SetActive(true);
                item.AuraObj.transform.Rotate(0f, 0f, 360f * Time.deltaTime);
                float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.2f;
                item.AuraRenderer.color = new Color(1f, 0.25f, 0.15f, 0.85f + Mathf.Sin(Time.time * 8f) * 0.15f);
                item.AuraObj.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                // Trạng thái bình thường: Vệt đỏ thon gọn xuất phát từ chuôi kiếm, tắt Aura
                if (item.IsUltimateMode)
                {
                    item.IsUltimateMode = false;
                    ApplyTrailGradient(item.Trail, normalColor, new Color(0.7f, 0.02f, 0.05f, 0f), startWidth, endWidth);
                }
                item.AuraObj.SetActive(false);
            }
        }

        // Tắt và thu hồi visual của các entity kiếm đã bị hủy
        if (_activeVisuals.Count > swordCount)
        {
            var deadEntities = new List<Entity>();
            foreach (var kvp in _activeVisuals)
            {
                if (!currentActiveEntities.Contains(kvp.Key))
                {
                    deadEntities.Add(kvp.Key);
                }
            }

            for (int i = 0; i < deadEntities.Count; i++)
            {
                Entity dead = deadEntities[i];
                var deadItem = _activeVisuals[dead];
                deadItem.Trail.Clear();
                deadItem.Trail.emitting = false;
                deadItem.Root.SetActive(false);
                _visualPool.Add(deadItem);
                _activeVisuals.Remove(dead);
            }
        }

        swordEntities.Dispose();
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

        var mat = SharedVisualAssets.SpriteUnlitMaterial;
        if (mat != null)
        {
            trail.material = mat;
        }

        // 2. Aura GameObject (vòng hào quang xoay quanh chuôi kiếm khi Ultimate sẵn sàng)
        var auraObj = new GameObject("UltimateAura");
        auraObj.transform.SetParent(root.transform, false);
        var auraRenderer = auraObj.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = SharedVisualAssets.CircleSprite;
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
        Material sharedMat = SharedVisualAssets.SpriteUnlitMaterial;

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
        _innerFlash.sprite = SharedVisualAssets.CircleSprite;
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
                new Color(1f, 0.45f, 0.2f, alpha),
                new Color(0.95f, 0.1f, 0.15f, alpha * 0.8f),
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
            _innerFlash.color = new Color(1f, 0.18f, 0.12f, alpha * 0.35f);
        }
    }
}
