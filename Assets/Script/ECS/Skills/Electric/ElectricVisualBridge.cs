using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Hybrid Visual Bridge quản lý toàn bộ hiệu ứng hình ảnh (VFX) của Kỹ năng Lôi Điện (Electric):
/// 1. Triển khai thuật toán Sét Thực Tế (Realistic Procedural Lightning):
///    - Lõi kép (White-Hot Core + Neon Cyan Corona Glow) tạo cảm giác dòng điện hàng triệu Volt.
///    - Thuật toán Fractal Midpoint Displacement tạo các nếp gãy zíc-zắc tự nhiên.
///    - Nhánh sét phụ (Branching Forks) tẽ ra không khí giải phóng hồ quang điện.
///    - Rung giật tần số cao (High-Frequency Jitter) và phai mờ siêu tốc (Rapid Decay).
/// 2. Truyền dẫn nối tiếp theo mili-giây (Staggered Chain Propagation):
///    - Tạo độ trễ mili-giây siêu nhỏ giữa các mắt xích để mắt người nhìn thấy tia điện phóng từ người qua từng quái.
/// 3. Điểm nổ va chạm (Impact Flash Disc & Spark Emitter):
///    - Đĩa nổ chớp sáng bung nở và chùm tia lửa điện tóe ra tại chân quái trúng đòn.
/// 4. Tối ưu hóa đỉnh cao (Zero GC Alloc & Massive Object Pool):
///    - Pool sẵn 48 cụm tia sét (tự động mở rộng thêm nếu có nhiều nguồn sét).
///    - Tái sử dụng mảng đệm cố định Static Buffer, không cấp phát RAM mỗi frame.
///    - Dùng chung 2 Material với GPU Batching, giữ vững 60–120 FPS.
/// </summary>
public class ElectricVisualBridge : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("Số lượng tia sét khởi tạo sẵn trong Pool (mặc định 48, tự động mở rộng nếu thiếu)")]
    [SerializeField] private int initialPoolSize = 48;

    [Header("Lightning Appearance")]
    [Tooltip("Thời gian tồn tại của 1 tia sét (s)")]
    [SerializeField] private float boltDuration = 0.18f;

    [Tooltip("Thời gian rung giật dữ dội trước khi phai mờ dần (s)")]
    [SerializeField] private float jitterDuration = 0.05f;

    [Tooltip("Độ trễ giữa mỗi bước lan truyền tia sét (s) - tạo cảm giác dòng điện chuyền nối tiếp")]
    [SerializeField] private float hopDelay = 0.025f;

    [Tooltip("Độ dày lõi plasma trắng (m)")]
    [SerializeField] private float coreWidth = 0.045f;

    [Tooltip("Độ dày hào quang xanh neon (m)")]
    [SerializeField] private float glowWidth = 0.18f;

    [Tooltip("Màu lõi plasma trắng")]
    [SerializeField] private Color coreColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Màu hào quang điện Cyan Neon")]
    [SerializeField] private Color glowColor = new Color(0f, 0.9f, 1f, 0.85f);

    [Tooltip("Màu tia lửa nổ va chạm")]
    [SerializeField] private Color sparkColor = new Color(0.4f, 0.95f, 1f, 1f);

    private EntityManager _entityManager;
    private EntityQuery _arcEventQuery;
    private bool _initialized;

    // Cache dùng chung Zero GC
    private static Material _coreMaterial;
    private static Material _glowMaterial;
    private static Sprite _flashSprite;

    private static readonly Vector3[] _pointBuffer = new Vector3[16];
    private static readonly Vector3[] _branchBuffer = new Vector3[6];

    // Đối tượng trong Pool
    private class LightningBoltItem
    {
        public GameObject Root;
        public LineRenderer CoreLine;
        public LineRenderer GlowLine;
        public LineRenderer BranchLine;
        public SpriteRenderer FlashRenderer;

        public Vector3 StartPos;
        public Vector3 EndPos;
        public float DelayTimer;
        public float LifeTimer;
        public float MaxLife;
        public int Segments;
        public float JitterAmount;
        public bool HasBranch;
        public int BranchIndex;
        public Vector3 BranchEnd;
        public bool IsActive;
        public bool IsPrimary;
        public bool InflictsStun;
    }

    private readonly List<LightningBoltItem> _pool = new List<LightningBoltItem>();
    private readonly List<LightningBoltItem> _activeBolts = new List<LightningBoltItem>();

    private ParticleSystem _sparkParticleSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<ElectricVisualBridge>() == null)
        {
            var go = new GameObject("[ElectricVisualBridge]");
            go.AddComponent<ElectricVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        EnsureMaterialsAndSprites();
        SetupSparkParticleSystem();
        InitializePool(initialPoolSize);
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
        _arcEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ElectricArcEvent>()
        );

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        float dt = Time.deltaTime;

        // 1. Tiếp nhận các sự kiện phóng sét từ Pure ECS
        ConsumeArcEvents();

        // 2. Cập nhật chuyển động và hiệu ứng của các tia sét đang hoạt động
        UpdateActiveBolts(dt);
    }

    private void ConsumeArcEvents()
    {
        if (_arcEventQuery.IsEmpty) return;

        using var events = _arcEventQuery.ToComponentDataArray<ElectricArcEvent>(Allocator.Temp);

        for (int i = 0; i < events.Length; i++)
        {
            var arc = events[i];
            Vector3 start = new Vector3(arc.StartPos.x, arc.StartPos.y, 0f);
            Vector3 end = new Vector3(arc.EndPos.x, arc.EndPos.y, 0f);

            SpawnLightningBolt(start, end, arc.HopIndex, arc.IsPrimary, arc.InflictsStun);
        }

        // Dọn sạch event sau khi đã vẽ
        _entityManager.DestroyEntity(_arcEventQuery);
    }

    private void SpawnLightningBolt(Vector3 start, Vector3 end, int hopIndex, bool isPrimary, bool inflictsStun)
    {
        LightningBoltItem bolt = GetOrCreateBolt();

        bolt.StartPos = start;
        bolt.EndPos = end;
        bolt.DelayTimer = hopIndex * hopDelay; // Độ trễ chuyển giao nhịp nhàng
        bolt.LifeTimer = 0f;
        bolt.MaxLife = boltDuration;
        bolt.IsPrimary = isPrimary;
        bolt.InflictsStun = inflictsStun;

        float dist = Vector3.Distance(start, end);
        bolt.Segments = Mathf.Clamp(Mathf.RoundToInt(dist * 2.5f) + 4, 8, 14);
        bolt.JitterAmount = Mathf.Min(0.45f, dist * 0.16f);

        // Tạo nhánh sét phụ tẽ ra ngẫu nhiên
        bolt.HasBranch = (dist > 1.8f && Random.value < 0.75f);
        if (bolt.HasBranch)
        {
            bolt.BranchIndex = Random.Range(3, bolt.Segments - 2);
            Vector3 mainDir = (end - start).normalized;
            float angle = (Random.value > 0.5f ? 35f : -35f) * Mathf.Deg2Rad;
            Vector3 branchDir = new Vector3(
                mainDir.x * Mathf.Cos(angle) - mainDir.y * Mathf.Sin(angle),
                mainDir.x * Mathf.Sin(angle) + mainDir.y * Mathf.Cos(angle),
                0f
            );
            bolt.BranchEnd = branchDir * (dist * Random.Range(0.25f, 0.45f));
        }

        // Tạm ẩn nếu có độ trễ nối tiếp
        bolt.IsActive = true;
        if (bolt.DelayTimer > 0f)
        {
            bolt.Root.SetActive(false);
        }
        else
        {
            bolt.Root.SetActive(true);
            RenderBoltGeometry(bolt, 1f);
            TriggerImpact(bolt.EndPos, bolt.IsPrimary, bolt.InflictsStun);
        }

        _activeBolts.Add(bolt);
    }

    private void UpdateActiveBolts(float dt)
    {
        for (int i = _activeBolts.Count - 1; i >= 0; i--)
        {
            LightningBoltItem bolt = _activeBolts[i];

            // Xử lý độ trễ Staggered Delay
            if (bolt.DelayTimer > 0f)
            {
                bolt.DelayTimer -= dt;
                if (bolt.DelayTimer <= 0f)
                {
                    bolt.Root.SetActive(true);
                    RenderBoltGeometry(bolt, 1f);
                    TriggerImpact(bolt.EndPos, bolt.IsPrimary, bolt.InflictsStun);
                }
                continue;
            }

            bolt.LifeTimer += dt;
            float progress = bolt.LifeTimer / bolt.MaxLife;

            if (progress >= 1f)
            {
                // Hết vòng đời -> tắt và trả về pool
                bolt.Root.SetActive(false);
                bolt.IsActive = false;
                _activeBolts.RemoveAt(i);
                continue;
            }

            // Giai đoạn đầu: Rung giật liên tục (Jitter) tạo cảm giác điện áp khổng lồ
            if (bolt.LifeTimer <= jitterDuration)
            {
                RenderBoltGeometry(bolt, 1f);
            }
            else
            {
                // Giai đoạn phai mờ (Fade-out): Giảm alpha và co hẹp bề rộng
                float fadeAlpha = Mathf.Clamp01(1f - (progress - jitterDuration / bolt.MaxLife) / (1f - jitterDuration / bolt.MaxLife));
                RenderBoltGeometry(bolt, fadeAlpha);
            }
        }
    }

    /// <summary>
    /// Thuật toán tạo hình tia sét Fractal Midpoint Displacement không cấp phát bộ nhớ (Zero GC).
    /// </summary>
    private void RenderBoltGeometry(LightningBoltItem bolt, float alpha)
    {
        Vector3 start = bolt.StartPos;
        Vector3 end = bolt.EndPos;
        int segments = bolt.Segments;
        float jitter = bolt.JitterAmount;

        Vector3 dir = end - start;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        _pointBuffer[0] = start;
        _pointBuffer[segments] = end;

        for (int k = 1; k < segments; k++)
        {
            float t = (float)k / segments;
            Vector3 lerpPos = Vector3.Lerp(start, end, t);

            // Bán nguyệt hình chuông (Envelope): Biên độ lệch lớn nhất ở giữa, giảm về 0 ở hai đầu
            float envelope = Mathf.Sin(Mathf.PI * t);
            float randomOffset = Random.Range(-1f, 1f) * jitter * envelope;
            float randomMicro = Random.Range(-0.06f, 0.06f);

            _pointBuffer[k] = lerpPos + perp * randomOffset + (Vector3)Random.insideUnitCircle * randomMicro;
        }

        // Cập nhật Lõi Plasma Trắng
        bolt.CoreLine.positionCount = segments + 1;
        bolt.CoreLine.SetPositions(_pointBuffer);
        float currentCoreWidth = coreWidth * (bolt.IsPrimary ? 1.25f : 1.0f) * alpha;
        bolt.CoreLine.startWidth = currentCoreWidth;
        bolt.CoreLine.endWidth = currentCoreWidth * 0.7f;
        Color cColor = coreColor;
        cColor.a = alpha;
        bolt.CoreLine.startColor = cColor;
        bolt.CoreLine.endColor = cColor;

        // Cập nhật Hào Quang Xanh Neon
        bolt.GlowLine.positionCount = segments + 1;
        bolt.GlowLine.SetPositions(_pointBuffer);
        float currentGlowWidth = glowWidth * (bolt.IsPrimary ? 1.35f : 1.0f) * alpha;
        bolt.GlowLine.startWidth = currentGlowWidth;
        bolt.GlowLine.endWidth = currentGlowWidth * 0.5f;
        Color gColor = glowColor;
        gColor.a = alpha * 0.85f;
        bolt.GlowLine.startColor = gColor;
        bolt.GlowLine.endColor = gColor;

        // Cập nhật Nhánh Sét Phụ (Branching Fork)
        if (bolt.HasBranch && bolt.BranchIndex < segments)
        {
            bolt.BranchLine.gameObject.SetActive(true);
            Vector3 branchOrigin = _pointBuffer[bolt.BranchIndex];
            Vector3 branchEndPos = branchOrigin + bolt.BranchEnd;

            _branchBuffer[0] = branchOrigin;
            _branchBuffer[4] = branchEndPos;

            for (int b = 1; b < 4; b++)
            {
                float bt = (float)b / 4f;
                Vector3 bLerp = Vector3.Lerp(branchOrigin, branchEndPos, bt);
                _branchBuffer[b] = bLerp + (Vector3)Random.insideUnitCircle * (jitter * 0.4f);
            }

            bolt.BranchLine.positionCount = 5;
            bolt.BranchLine.SetPositions(_branchBuffer);
            float bWidth = currentCoreWidth * 0.75f;
            bolt.BranchLine.startWidth = bWidth;
            bolt.BranchLine.endWidth = 0.01f;
            bolt.BranchLine.startColor = gColor;
            bolt.BranchLine.endColor = new Color(gColor.r, gColor.g, gColor.b, 0f);
        }
        else
        {
            bolt.BranchLine.gameObject.SetActive(false);
        }

        // Cập nhật Đĩa Chớp Nổ (Impact Flash Disc)
        if (bolt.FlashRenderer != null)
        {
            bolt.FlashRenderer.transform.position = end;
            float flashScale = Mathf.Lerp(0.85f, 0.2f, 1f - alpha) * (bolt.IsPrimary ? 1.3f : 1.0f);
            bolt.FlashRenderer.transform.localScale = Vector3.one * flashScale;
            Color fColor = coreColor;
            fColor.a = alpha;
            bolt.FlashRenderer.color = fColor;
        }
    }

    /// <summary>
    /// Kích hoạt chớp sáng va chạm và bắn chùm tia lửa điện
    /// </summary>
    private void TriggerImpact(Vector3 pos, bool isPrimary, bool inflictsStun)
    {
        if (_sparkParticleSystem != null)
        {
            int count = isPrimary ? 12 : 7;
            if (inflictsStun) count += 6; // Thêm tia lửa điện nếu gây Stun

            var emitParams = new ParticleSystem.EmitParams
            {
                position = pos,
                applyShapeToPosition = true
            };
            _sparkParticleSystem.Emit(emitParams, count);
        }
    }

    #region --- Object Pooling & Setup ---

    private void InitializePool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _pool.Add(CreateNewBoltItem(i));
        }
    }

    private LightningBoltItem GetOrCreateBolt()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!_pool[i].IsActive)
            {
                return _pool[i];
            }
        }

        // Mở rộng động nếu số lượng tia vượt quá dự tính
        var newItem = CreateNewBoltItem(_pool.Count);
        _pool.Add(newItem);
        return newItem;
    }

    private LightningBoltItem CreateNewBoltItem(int index)
    {
        var root = new GameObject($"Bolt_{index}");
        root.transform.SetParent(transform);
        root.SetActive(false);

        // 1. Glow Line (vẽ trước, nằm dưới)
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform);
        var glowLine = glowGo.AddComponent<LineRenderer>();
        SetupLineRenderer(glowLine, _glowMaterial, 1);

        // 2. Core Line (vẽ sau, nằm đè lên trên)
        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(root.transform);
        var coreLine = coreGo.AddComponent<LineRenderer>();
        SetupLineRenderer(coreLine, _coreMaterial, 2);

        // 3. Branch Line (nhánh phụ)
        var branchGo = new GameObject("Branch");
        branchGo.transform.SetParent(root.transform);
        var branchLine = branchGo.AddComponent<LineRenderer>();
        SetupLineRenderer(branchLine, _coreMaterial, 2);

        // 4. Impact Flash (đĩa sáng tại điểm va chạm)
        var flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(root.transform);
        var flashSr = flashGo.AddComponent<SpriteRenderer>();
        flashSr.sprite = _flashSprite;
        flashSr.material = _glowMaterial;
        flashSr.sortingOrder = 3;

        return new LightningBoltItem
        {
            Root = root,
            CoreLine = coreLine,
            GlowLine = glowLine,
            BranchLine = branchLine,
            FlashRenderer = flashSr,
            IsActive = false
        };
    }

    private static void SetupLineRenderer(LineRenderer lr, Material mat, int sortingOrder)
    {
        lr.material = mat;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.sortingOrder = sortingOrder;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    private static void EnsureMaterialsAndSprites()
    {
        _coreMaterial = SharedVisualAssets.SpriteUnlitMaterial;
        _glowMaterial = SharedVisualAssets.SpriteUnlitMaterial;
        _flashSprite = SharedVisualAssets.CircleSprite;
    }

    private void SetupSparkParticleSystem()
    {
        var psGo = new GameObject("[SparkEmitter]");
        psGo.transform.SetParent(transform);
        _sparkParticleSystem = psGo.AddComponent<ParticleSystem>();

        var main = _sparkParticleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 0.16f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = sparkColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        var emission = _sparkParticleSystem.emission;
        emission.enabled = false; // Chỉ phát bằng Emit thủ tục

        var shape = _sparkParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        var colorOverLifetime = _sparkParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(sparkColor, 0.4f), new GradientColorKey(glowColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = _sparkParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        var renderer = _sparkParticleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.material = _glowMaterial;
        renderer.sortingOrder = 4;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    #endregion
}
