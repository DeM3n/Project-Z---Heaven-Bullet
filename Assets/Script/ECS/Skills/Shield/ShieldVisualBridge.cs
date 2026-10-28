using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Bridge quản lý hiệu ứng hình ảnh trực quan cho Khiên Hộ Thể (Aegis Shield):
/// 1. Vòng năng lượng hình lục giác (Hexagonal Energy Barrier) phát sáng bọc quanh Player khi có khiên.
/// 2. Hiệu ứng xoay đồng trục 2 lớp khiên khi ở Cấp 3 và Cấp 4.
/// 3. Cảnh báo rạn nứt phát sáng khi lớp khiên chỉ còn 1 hit cuối ở Cấp 4.
/// 4. Chớp sáng hấp thụ sát thương (Block Absorption Flash) khi đỡ đòn thành công.
/// 5. Sóng xung kích nổ vỡ (Shatter Nova Shockwave) kèm các mảnh tinh thể văng tung tóe khi vỡ khiên ở Cấp 4.
/// </summary>
public class ShieldVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private EntityQuery _breakEventQuery;
    private bool _initialized;

    // Đối tượng hiển thị vòng bảo vệ bọc quanh Player
    private GameObject _barrierRoot;
    private LineRenderer _outerHexLine;
    private LineRenderer _innerHexLine;
    private SpriteRenderer _glowDisc;
    private SpriteRenderer _orbitNode1;
    private SpriteRenderer _orbitNode2;

    private float _pulseTime;
    private float _blockFlashTimer = 0f;
    private static Sprite _circleSprite;

    // Pool quản lý các vụ nổ vỡ khiên (Shatter Effects)
    private readonly List<ShieldShatterEffect> _shatterPool = new List<ShieldShatterEffect>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<ShieldVisualBridge>() == null)
        {
            var go = new GameObject("[ShieldVisualBridge]");
            go.AddComponent<ShieldVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        InitECS();
        CreateBarrierHierarchy();
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadWrite<PlayerShieldData>());

        _breakEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ShieldBreakEvent>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        UpdateBarrierVisual();
        HandleBreakEvents();
    }

    /// <summary>
    /// Tạo cấu trúc GameObject và Renderer cho vòng khiên lục giác bao quanh Player
    /// </summary>
    private void CreateBarrierHierarchy()
    {
        _barrierRoot = new GameObject("AegisBarrierRoot");
        _barrierRoot.transform.SetParent(transform, false);

        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                     ?? Shader.Find("Sprites/Default");
        Material lineMat = (shader != null) ? new Material(shader) : null;
        Material spriteMat = (shader != null) ? new Material(shader) : null;

        // 1. LineRenderer lục giác ngoài (Outer Hexagon)
        var outerObj = new GameObject("OuterHex");
        outerObj.transform.SetParent(_barrierRoot.transform, false);
        _outerHexLine = outerObj.AddComponent<LineRenderer>();
        _outerHexLine.useWorldSpace = false;
        _outerHexLine.positionCount = 7; // 6 cạnh khép kín
        _outerHexLine.loop = true;
        _outerHexLine.startWidth = 0.08f;
        _outerHexLine.endWidth = 0.08f;
        _outerHexLine.sortingOrder = 8;
        if (lineMat != null) _outerHexLine.material = lineMat;

        // 2. LineRenderer lục giác trong (Inner Hexagon cho 2 Shield)
        var innerObj = new GameObject("InnerHex");
        innerObj.transform.SetParent(_barrierRoot.transform, false);
        _innerHexLine = innerObj.AddComponent<LineRenderer>();
        _innerHexLine.useWorldSpace = false;
        _innerHexLine.positionCount = 7;
        _innerHexLine.loop = true;
        _innerHexLine.startWidth = 0.06f;
        _innerHexLine.endWidth = 0.06f;
        _innerHexLine.sortingOrder = 7;
        if (lineMat != null) _innerHexLine.material = lineMat;

        // 3. Đĩa sáng năng lượng ở giữa (Glow Disc)
        var discObj = new GameObject("GlowDisc");
        discObj.transform.SetParent(_barrierRoot.transform, false);
        _glowDisc = discObj.AddComponent<SpriteRenderer>();
        _glowDisc.sprite = GetCircleSprite();
        _glowDisc.sortingOrder = 6;
        _glowDisc.color = new Color(0.1f, 0.9f, 1f, 0.18f);
        if (spriteMat != null) _glowDisc.material = spriteMat;

        // 4. Các điểm ngọc năng lượng quay quanh khiên (Orbital Nodes)
        _orbitNode1 = CreateOrbitalNode("OrbitNode1", spriteMat);
        _orbitNode2 = CreateOrbitalNode("OrbitNode2", spriteMat);

        SetupHexagonPoints(_outerHexLine, 1.25f);
        SetupHexagonPoints(_innerHexLine, 0.95f);

        _barrierRoot.SetActive(false);
    }

    private SpriteRenderer CreateOrbitalNode(string name, Material mat)
    {
        var nodeObj = new GameObject(name);
        nodeObj.transform.SetParent(_barrierRoot.transform, false);
        var sr = nodeObj.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.sortingOrder = 9;
        sr.color = new Color(0.7f, 1f, 1f, 0.9f);
        nodeObj.transform.localScale = Vector3.one * 0.22f;
        if (mat != null) sr.material = mat;
        return sr;
    }

    private static void SetupHexagonPoints(LineRenderer line, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        line.SetPosition(6, line.GetPosition(0)); // Khép kín vòng
    }

    /// <summary>
    /// Cập nhật vị trí, góc xoay và hiệu ứng phát sáng của vòng khiên quanh người chơi
    /// </summary>
    private void UpdateBarrierVisual()
    {
        if (_playerQuery.IsEmpty)
        {
            if (_barrierRoot != null) _barrierRoot.SetActive(false);
            return;
        }

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        LocalTransform playerTransform = _entityManager.GetComponentData<LocalTransform>(playerEntity);
        PlayerShieldData shield = _entityManager.GetComponentData<PlayerShieldData>(playerEntity);

        // Nếu chưa học kỹ năng hoặc không còn lớp khiên nào, ẩn vòng bảo vệ
        if (shield.SkillLevel <= 0 || shield.CurrentCharges <= 0 || shield.HitsRemainingInCurrentShield <= 0)
        {
            if (_barrierRoot != null && _barrierRoot.activeSelf)
            {
                _barrierRoot.SetActive(false);
            }
            return;
        }

        if (!_barrierRoot.activeSelf)
        {
            _barrierRoot.SetActive(true);
        }

        // Bám sát vị trí của Player
        _barrierRoot.transform.position = new Vector3(playerTransform.Position.x, playerTransform.Position.y, 0f);

        _pulseTime += Time.deltaTime;

        // Xử lý hiệu ứng chớp sáng khi vừa chặn thành công 1 đòn (Block Flash)
        if (shield.HasJustBlocked)
        {
            _blockFlashTimer = 0.2f;
            shield.HasJustBlocked = false;
            _entityManager.SetComponentData(playerEntity, shield);
        }

        if (_blockFlashTimer > 0f)
        {
            _blockFlashTimer -= Time.deltaTime;
        }

        bool isBlockFlashing = _blockFlashTimer > 0f;

        // Độ co giãn thở nhẹ (Pulse) của khiên
        float pulseScale = 1.0f + Mathf.Sin(_pulseTime * 3.5f) * 0.05f;
        if (isBlockFlashing) pulseScale *= 1.18f;

        _barrierRoot.transform.localScale = Vector3.one * pulseScale;

        // Xoay 2 vòng lục giác ngược chiều nhau
        _outerHexLine.transform.Rotate(0f, 0f, 35f * Time.deltaTime);
        _innerHexLine.transform.Rotate(0f, 0f, -50f * Time.deltaTime);

        // Xác định màu sắc theo trạng thái của khiên
        Color baseHexColor;
        Color glowDiscColor;

        if (isBlockFlashing)
        {
            // Flash trắng sáng chói khi đỡ đòn thành công
            baseHexColor = Color.white;
            glowDiscColor = new Color(0.8f, 1f, 1f, 0.6f);
        }
        else if (shield.SkillLevel >= 4 && shield.HitsRemainingInCurrentShield == 1)
        {
            // Cảnh báo rạn nứt ở Cấp 4 (chỉ còn 1 hit): màu hổ phách / vàng cam rực cháy
            baseHexColor = new Color(1f, 0.75f, 0.2f, 0.95f);
            glowDiscColor = new Color(1f, 0.6f, 0.1f, 0.28f);
        }
        else
        {
            // Trạng thái bình thường: Lam ngọc phát sáng huyền ảo (Cyan / Diamond Blue)
            baseHexColor = new Color(0f, 0.9f, 1f, 0.85f);
            glowDiscColor = new Color(0.05f, 0.85f, 1f, 0.20f);
        }

        _outerHexLine.startColor = baseHexColor;
        _outerHexLine.endColor = baseHexColor;
        _glowDisc.color = glowDiscColor;
        _glowDisc.transform.localScale = Vector3.one * (2.1f + Mathf.Sin(_pulseTime * 2.5f) * 0.1f);

        // Nếu có 2 lớp khiên: hiển thị thêm vòng lục giác phụ và các orbital node
        bool hasDualShield = (shield.CurrentCharges >= 2);
        _innerHexLine.gameObject.SetActive(hasDualShield);
        _orbitNode1.gameObject.SetActive(hasDualShield);
        _orbitNode2.gameObject.SetActive(hasDualShield);

        if (hasDualShield)
        {
            Color innerColor = new Color(0.4f, 1f, 0.85f, 0.75f);
            _innerHexLine.startColor = innerColor;
            _innerHexLine.endColor = innerColor;

            // Xoay các node quỹ đạo đối xứng quanh người chơi
            float orbitAngle = _pulseTime * 2.8f;
            float orbitRadius = 1.35f;
            _orbitNode1.transform.localPosition = new Vector3(Mathf.Cos(orbitAngle) * orbitRadius, Mathf.Sin(orbitAngle) * orbitRadius, 0f);
            _orbitNode2.transform.localPosition = new Vector3(Mathf.Cos(orbitAngle + Mathf.PI) * orbitRadius, Mathf.Sin(orbitAngle + Mathf.PI) * orbitRadius, 0f);
        }
    }

    /// <summary>
    /// Xử lý các sự kiện vỡ khiên (ShieldBreakEvent) để sinh hiệu ứng nổ và mảnh vỡ
    /// </summary>
    private void HandleBreakEvents()
    {
        if (_breakEventQuery.IsEmpty) return;

        var events = _breakEventQuery.ToComponentDataArray<ShieldBreakEvent>(Allocator.Temp);

        for (int i = 0; i < events.Length; i++)
        {
            Vector3 center = new Vector3(events[i].Position.x, events[i].Position.y, 0f);
            bool isExplosion = events[i].IsExplosion;
            float radius = isExplosion ? events[i].Radius : 0.8f;

            SpawnShatterEffect(center, radius, isExplosion);
        }

        // Hủy các entity event sau khi đã kích hoạt VFX
        _entityManager.DestroyEntity(_breakEventQuery);
        events.Dispose();
    }

    private void SpawnShatterEffect(Vector3 position, float radius, bool isExplosion)
    {
        ShieldShatterEffect effect = null;
        for (int i = 0; i < _shatterPool.Count; i++)
        {
            if (!_shatterPool[i].gameObject.activeSelf)
            {
                effect = _shatterPool[i];
                break;
            }
        }

        if (effect == null)
        {
            var go = new GameObject($"ShieldShatter_{_shatterPool.Count}");
            go.transform.SetParent(transform);
            effect = go.AddComponent<ShieldShatterEffect>();
            _shatterPool.Add(effect);
        }

        effect.Setup(position, radius, isExplosion ? 0.50f : 0.30f, isExplosion);
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float norm = dist / radius;
                if (norm > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float alpha = Mathf.SmoothStep(1f, 0f, norm);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return _circleSprite;
    }
}

/// <summary>
/// Hiệu ứng sóng chấn động nổ vỡ (Shatter Nova Shockwave) kèm các mảnh vỡ pha lê bay tỏa ra
/// </summary>
public class ShieldShatterEffect : MonoBehaviour
{
    private LineRenderer _outerRing;
    private SpriteRenderer _innerFlash;
    private readonly List<GameObject> _shards = new List<GameObject>();
    private readonly List<Vector2> _shardDirs = new List<Vector2>();

    private float _maxRadius;
    private float _duration;
    private float _elapsed;
    private bool _isExplosion;
    private const int SEGMENTS = 36;
    private const int SHARD_COUNT = 10;

    private void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                     ?? Shader.Find("Sprites/Default");
        Material sharedMat = (shader != null) ? new Material(shader) : null;

        // 1. LineRenderer sóng xung kích hình tròn quét rộng
        _outerRing = gameObject.AddComponent<LineRenderer>();
        _outerRing.useWorldSpace = false;
        _outerRing.positionCount = SEGMENTS + 1;
        _outerRing.loop = true;
        _outerRing.sortingOrder = 10;
        if (sharedMat != null) _outerRing.material = sharedMat;

        // 2. SpriteRenderer lấp đầy ánh sáng chớp nổ ở tâm
        var flashObj = new GameObject("ShatterFlash");
        flashObj.transform.SetParent(transform, false);
        _innerFlash = flashObj.AddComponent<SpriteRenderer>();
        _innerFlash.sprite = ShieldVisualBridge.GetCircleSprite();
        _innerFlash.sortingOrder = 9;
        if (sharedMat != null) _innerFlash.material = sharedMat;

        // 3. Mảnh vỡ pha lê (Crystal Shards)
        for (int i = 0; i < SHARD_COUNT; i++)
        {
            var shardObj = new GameObject($"Shard_{i}");
            shardObj.transform.SetParent(transform, false);
            var sr = shardObj.AddComponent<SpriteRenderer>();
            sr.sprite = ShieldVisualBridge.GetCircleSprite();
            sr.sortingOrder = 11;
            shardObj.transform.localScale = new Vector3(0.12f, 0.28f, 1f); // Dạng mảnh nhọn
            if (sharedMat != null) sr.material = sharedMat;
            _shards.Add(shardObj);
            _shardDirs.Add(Vector2.zero);
        }
    }

    public void Setup(Vector3 position, float radius, float duration, bool isExplosion)
    {
        transform.position = position;
        _maxRadius = radius;
        _duration = duration;
        _elapsed = 0f;
        _isExplosion = isExplosion;
        gameObject.SetActive(true);

        // Chỉ bật vòng tròn sóng xung kích quét rộng và các mảnh vỡ khi là VỤ NỔ CẤP 4!
        _outerRing.gameObject.SetActive(isExplosion);

        for (int i = 0; i < SHARD_COUNT; i++)
        {
            float angle = (i * (360f / SHARD_COUNT) + UnityEngine.Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            _shardDirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(3.5f, 6.0f);
            _shards[i].transform.localPosition = Vector3.zero;
            _shards[i].transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90f);
            _shards[i].SetActive(isExplosion);
        }

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
        float alpha = Mathf.Clamp01((1f - t) * 1.5f);

        // CHỈ cập nhật LineRenderer vòng tròn sóng xung kích khi là VỤ NỔ CẤP 4 (isExplosion == true)
        if (_isExplosion)
        {
            Color shockColor = new Color(0.1f, 0.95f, 1f, alpha);
            float lineWidth = Mathf.Lerp(0.14f, 0.02f, t);
            _outerRing.startWidth = lineWidth;
            _outerRing.endWidth = lineWidth;
            _outerRing.startColor = shockColor;
            _outerRing.endColor = shockColor;

            for (int i = 0; i <= SEGMENTS; i++)
            {
                float rad = (i * 360f / SEGMENTS) * Mathf.Deg2Rad;
                _outerRing.SetPosition(i, new Vector3(Mathf.Cos(rad) * currentRadius, Mathf.Sin(rad) * currentRadius, 0f));
            }
        }

        // Cập nhật ánh sáng ở tâm:
        // Cấp 4: Chớp nổ rực rỡ lan rộng theo bán kính nổ
        // Cấp 1-3: Chỉ là một ánh chớp xanh nhỏ tan biến tại chỗ (~0.8m)
        float flashScale = _isExplosion
            ? Mathf.Lerp(0.5f, _maxRadius * 0.75f, easeT)
            : Mathf.Lerp(1.0f, 0.2f, t);

        _innerFlash.transform.localScale = Vector3.one * flashScale;
        _innerFlash.color = new Color(0.2f, 0.95f, 1f, alpha * (_isExplosion ? 0.45f : 0.22f));

        // Cập nhật vị trí các mảnh vỡ bay tỏa ra
        if (_isExplosion)
        {
            for (int i = 0; i < SHARD_COUNT; i++)
            {
                _shards[i].transform.localPosition = (Vector3)(_shardDirs[i] * easeT * (_maxRadius * 0.28f));
                var sr = _shards[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.7f, 1f, 1f, alpha);
                }
            }
        }
    }
}
