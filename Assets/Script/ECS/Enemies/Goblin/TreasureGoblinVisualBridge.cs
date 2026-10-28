using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hybrid Visual Bridge cho Treasure Goblin:
/// - Hiển thị Sprite Yêu Tinh Hoàng Kim lấp lánh mang bao tải báu vật.
/// - Đồng bộ thanh đếm ngược tẩu thoát ("⏳ 18s") hoặc trạng thái nhàn rỗi ("💰 IDLE") trên đầu quái.
/// - Phát thông báo biểu ngữ khi Goblin xuất hiện trên chiến trường.
/// - Mũi tên dẫn đường (Navigation Pointer): Tự động chỉ hướng và hiển thị cự ly (mét) đến Goblin.
/// </summary>
public class TreasureGoblinVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _goblinQuery;
    private EntityQuery _playerQuery;
    private bool _initialized;

    private class GoblinVisual
    {
        public GameObject Root;
        public SpriteRenderer BodyRenderer;
        public Text CountdownText;
        public GameObject CanvasObj;
    }

    private readonly Dictionary<Entity, GoblinVisual> _activeVisuals = new Dictionary<Entity, GoblinVisual>();
    private readonly List<GoblinVisual> _pool = new List<GoblinVisual>();

    private Sprite _goblinSprite;
    private Sprite _arrowSprite;
    private Font _defaultFont;
    private int _lastSeenCount = 0;

    // --- Navigation Pointer UI (Mũi tên chỉ hướng Treasure Goblin) ---
    private Canvas _pointerCanvas;
    private GameObject _pointerRoot;
    private RectTransform _pointerRect;
    private RectTransform _arrowRect;
    private Image _arrowImage;
    private Text _infoText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<TreasureGoblinVisualBridge>() == null)
        {
            var go = new GameObject("[TreasureGoblinVisualBridge]");
            go.AddComponent<TreasureGoblinVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        GenerateProceduralGoblinSprite();
        GenerateProceduralArrowSprite();
        InitPointerUI();
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

        _goblinQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<TreasureGoblinTag>(),
            ComponentType.ReadOnly<TreasureGoblinData>(),
            ComponentType.ReadOnly<LocalTransform>());

        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<LocalTransform>());

        _initialized = true;
        return true;
    }

    private void Update()
    {
        if (!_initialized && !InitECS()) return;

        SyncVisuals();
    }

    private void SyncVisuals()
    {
        if (_goblinQuery.IsEmpty)
        {
            if (_activeVisuals.Count > 0)
            {
                foreach (var kvp in _activeVisuals)
                {
                    kvp.Value.Root.SetActive(false);
                    _pool.Add(kvp.Value);
                }
                _activeVisuals.Clear();
            }

            // Ẩn mũi tên dẫn đường khi không có Goblin nào
            if (_pointerRoot != null && _pointerRoot.activeSelf)
            {
                _pointerRoot.SetActive(false);
            }

            _lastSeenCount = 0;
            return;
        }

        var entities = _goblinQuery.ToEntityArray(Allocator.Temp);
        var transforms = _goblinQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var datas = _goblinQuery.ToComponentDataArray<TreasureGoblinData>(Allocator.Temp);

        // Báo động banner khi Goblin vừa xuất hiện
        if (entities.Length > _lastSeenCount)
        {
            GameHUDBridge.ShowBannerNotification(
                GameLocalization.Get("⚡ A Treasure Goblin appeared! Follow the arrow!", "⚡ Yêu Tinh Trộm Bảo xuất hiện! Hãy đi theo mũi tên!"));
        }
        _lastSeenCount = entities.Length;

        var currentAlive = new HashSet<Entity>();

        // Lấy tọa độ Player để tính Goblin gần nhất
        Vector3 playerPos = Vector3.zero;
        if (!_playerQuery.IsEmpty)
        {
            playerPos = _playerQuery.GetSingleton<LocalTransform>().Position;
        }
        else if (Camera.main != null)
        {
            playerPos = Camera.main.transform.position;
        }

        int closestIdx = 0;
        float closestDistSq = float.MaxValue;

        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];
            currentAlive.Add(e);

            Vector3 pos = new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f);
            float dSq = (pos - playerPos).sqrMagnitude;
            if (dSq < closestDistSq)
            {
                closestDistSq = dSq;
                closestIdx = i;
            }

            if (!_activeVisuals.TryGetValue(e, out GoblinVisual vis))
            {
                vis = GetOrCreateVisual();
                vis.Root.SetActive(true);
                _activeVisuals[e] = vis;
            }

            // Đồng bộ vị trí
            vis.Root.transform.position = pos;

            // Hiệu ứng nhấp nhô & lắc lư:
            // - Nếu đã bị phát hiện: chạy hoảng loạn (nhấp nhô nhanh)
            // - Nếu chưa phát hiện: đứng yên thở nhẹ (nhấp nhô chậm)
            bool isAlerted = datas[i].HasDetectedPlayer;
            float wobbleSpeed = isAlerted ? 14f : 4f;
            float wobbleAmp = isAlerted ? 0.06f : 0.02f;
            float wobble = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmp;
            vis.BodyRenderer.transform.localPosition = new Vector3(0f, wobble, 0f);

            // Cập nhật text trạng thái trên đầu
            if (vis.CountdownText != null)
            {
                if (isAlerted)
                {
                    int remainSec = Mathf.Max(0, Mathf.CeilToInt(datas[i].EscapeTimer));
                    vis.CountdownText.text = $"⏳ {remainSec}s";
                    vis.CountdownText.color = (remainSec <= 5) ? Color.red : new Color(1f, 0.85f, 0.2f);
                }
                else
                {
                    vis.CountdownText.text = "💰 IDLE";
                    vis.CountdownText.color = new Color(0.4f, 1f, 0.5f);
                }
            }
        }

        // Cập nhật mũi tên dẫn đường trỏ về Goblin gần nhất
        if (transforms.Length > 0)
        {
            Vector3 targetPos = new Vector3(transforms[closestIdx].Position.x, transforms[closestIdx].Position.y, 0f);
            float distance = Mathf.Sqrt(closestDistSq);
            bool isAlerted = datas[closestIdx].HasDetectedPlayer;
            UpdatePointerPosition(playerPos, targetPos, distance, isAlerted);
        }

        // Dọn dẹp visual các entity đã chết hoặc tẩu thoát
        var toRemove = new List<Entity>();
        foreach (var kvp in _activeVisuals)
        {
            if (!currentAlive.Contains(kvp.Key))
            {
                kvp.Value.Root.SetActive(false);
                _pool.Add(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _activeVisuals.Remove(toRemove[i]);
        }

        entities.Dispose();
        transforms.Dispose();
        datas.Dispose();
    }

    private void UpdatePointerPosition(Vector3 playerPos, Vector3 targetPos, float distance, bool isAlerted)
    {
        if (_pointerRoot == null) return;
        if (!_pointerRoot.activeSelf) _pointerRoot.SetActive(true);

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(targetPos);
        float screenW = Screen.width;
        float screenH = Screen.height;
        Vector2 screenCenter = new Vector2(screenW * 0.5f, screenH * 0.5f);

        // Khoảng cách an toàn tính từ viền màn hình
        float marginX = 85f;
        float marginY = 55f;

        bool isOffScreen = screenPos.z <= 0 ||
                           screenPos.x < marginX || screenPos.x > screenW - marginX ||
                           screenPos.y < marginY || screenPos.y > screenH - marginY;

        Vector2 dir = (new Vector2(screenPos.x, screenPos.y) - screenCenter);
        if (screenPos.z < 0) dir = -dir;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        dir.Normalize();

        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (isOffScreen)
        {
            // Bám theo mép màn hình hướng về Goblin
            float halfW = screenW * 0.5f - marginX;
            float halfH = screenH * 0.5f - marginY;
            float tX = Mathf.Abs(dir.x) > 0.001f ? (halfW / Mathf.Abs(dir.x)) : 99999f;
            float tY = Mathf.Abs(dir.y) > 0.001f ? (halfH / Mathf.Abs(dir.y)) : 99999f;
            float t = Mathf.Min(tX, tY);

            _pointerRect.position = screenCenter + dir * t;
            _arrowRect.localRotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
        }
        else
        {
            // Khi Goblin đã vào trong màn hình: Mũi tên chỉ thẳng xuống đầu Goblin
            _pointerRect.position = new Vector2(screenPos.x, screenPos.y + 60f);
            _arrowRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }

        // Hiệu ứng nhịp đập Pulse
        float pulseSpeed = isAlerted ? 12f : 6f;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.08f;
        _pointerRect.localScale = Vector3.one * pulse;

        // Cập nhật text và màu sắc
        int distMeters = Mathf.Max(1, Mathf.RoundToInt(distance));
        if (isAlerted)
        {
            _infoText.text = $"🚨 {distMeters}m";
            _infoText.color = new Color(1f, 0.40f, 0.15f);
            _arrowImage.color = new Color(1f, 0.45f, 0.15f);
        }
        else
        {
            _infoText.text = $"💰 {distMeters}m";
            _infoText.color = new Color(1f, 0.90f, 0.25f);
            _arrowImage.color = new Color(1f, 0.85f, 0.2f);
        }
    }

    private void InitPointerUI()
    {
        var canvasObj = new GameObject("[GoblinPointerCanvas]");
        canvasObj.transform.SetParent(transform, false);
        _pointerCanvas = canvasObj.AddComponent<Canvas>();
        _pointerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _pointerCanvas.sortingOrder = 90;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Khung hiển thị tổng thể (Badge Container)
        _pointerRoot = new GameObject("PointerRoot");
        _pointerRoot.transform.SetParent(canvasObj.transform, false);
        _pointerRect = _pointerRoot.AddComponent<RectTransform>();
        _pointerRect.sizeDelta = new Vector2(125f, 38f);
        _pointerRect.pivot = new Vector2(0.5f, 0.5f);

        // Nền tối mờ sang trọng cho Badge
        var bgImage = _pointerRoot.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.06f, 0.12f, 0.88f);

        var outline = _pointerRoot.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.8f, 0.2f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Mũi tên dẫn đường (Arrow Pointer Graphic)
        var arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(_pointerRoot.transform, false);
        _arrowRect = arrowObj.AddComponent<RectTransform>();
        _arrowRect.sizeDelta = new Vector2(24f, 24f);
        _arrowRect.anchoredPosition = new Vector2(-38f, 0f);

        _arrowImage = arrowObj.AddComponent<Image>();
        _arrowImage.sprite = _arrowSprite;
        _arrowImage.color = new Color(1f, 0.85f, 0.2f);

        // Text khoảng cách & trạng thái
        var textObj = new GameObject("InfoText");
        textObj.transform.SetParent(_pointerRoot.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(75f, 32f);
        textRect.anchoredPosition = new Vector2(14f, 0f);

        _infoText = textObj.AddComponent<Text>();
        _infoText.font = _defaultFont;
        _infoText.fontSize = 17;
        _infoText.fontStyle = FontStyle.Bold;
        _infoText.alignment = TextAnchor.MiddleCenter;
        _infoText.text = "💰 15m";
        _infoText.color = new Color(1f, 0.90f, 0.25f);

        var textOutline = textObj.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1.2f, -1.2f);

        _pointerRoot.SetActive(false);
    }

    private void GenerateProceduralArrowSprite()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            tex.SetPixel(x, y, clear);
        }

        Vector2 top = new Vector2(15.5f, 29f);
        Vector2 left = new Vector2(4f, 4f);
        Vector2 right = new Vector2(27f, 4f);
        Vector2 innerNotch = new Vector2(15.5f, 11f);

        // Tô hình Chevron / Mũi tên nhọn
        for (int y = 4; y <= 29; y++)
        for (int x = 4; x <= 27; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            if (IsPointInTriangle(p, left, top, innerNotch) || IsPointInTriangle(p, innerNotch, top, right))
            {
                bool isBorder = (y <= 6 || Mathf.Abs(x - 15.5f) >= (29 - y) * 0.92f);
                Color c = isBorder ? new Color(1f, 0.60f, 0.05f, 1f) : new Color(1f, 0.95f, 0.45f, 1f);
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        _arrowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static bool IsPointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        float d1 = Sign(pt, v1, v2);
        float d2 = Sign(pt, v2, v3);
        float d3 = Sign(pt, v3, v1);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private GoblinVisual GetOrCreateVisual()
    {
        if (_pool.Count > 0)
        {
            var item = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            return item;
        }

        var root = new GameObject("[GoblinVisual]");
        root.transform.SetParent(transform);

        // 1. Sprite Renderer
        var bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(root.transform, false);
        var sr = bodyObj.AddComponent<SpriteRenderer>();
        sr.sprite = _goblinSprite;
        sr.sortingOrder = 25; // Nằm trên quái thường

        // 2. World Space Canvas cho Text trạng thái
        var canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(root.transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 35;

        var rectTransform = canvasObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100, 30);
        rectTransform.localScale = Vector3.one * 0.015f;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        var text = textObj.AddComponent<Text>();
        text.font = _defaultFont;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;
        text.text = "💰 IDLE";
        text.color = new Color(0.4f, 1f, 0.5f);

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(100, 30);
        textRect.localPosition = Vector3.zero;

        return new GoblinVisual
        {
            Root = root,
            BodyRenderer = sr,
            CountdownText = text,
            CanvasObj = canvasObj
        };
    }

    private void GenerateProceduralGoblinSprite()
    {
        int size = 28;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color empty = new Color(0, 0, 0, 0);

        Color goldSkin = new Color(1f, 0.82f, 0.15f, 1f);
        Color darkGold = new Color(0.75f, 0.55f, 0.05f, 1f);
        Color redEyes = new Color(0.95f, 0.15f, 0.2f, 1f);
        Color bagBrown = new Color(0.6f, 0.35f, 0.15f, 1f);
        Color bagGold = new Color(1f, 0.95f, 0.4f, 1f);
        Color outline = new Color(0.2f, 0.12f, 0.02f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, empty);
            }
        }

        // Vẽ bao tải tiền trên lưng (phía trái)
        for (int y = 8; y <= 21; y++)
        {
            for (int x = 4; x <= 13; x++)
            {
                float dx = x - 8.5f;
                float dy = y - 14.5f;
                if (dx * dx + dy * dy <= 22f)
                {
                    tex.SetPixel(x, y, (y > 17) ? bagGold : bagBrown);
                }
            }
        }

        // Vẽ thân và đầu Goblin (phía phải)
        for (int y = 5; y <= 22; y++)
        {
            for (int x = 11; x <= 22; x++)
            {
                float dx = x - 16.5f;
                float dy = y - 13.5f;
                if (dx * dx + dy * dy <= 30f)
                {
                    tex.SetPixel(x, y, goldSkin);
                }
            }
        }

        // Tai nhọn đặc trưng của Goblin
        tex.SetPixel(23, 17, darkGold);
        tex.SetPixel(24, 18, darkGold);
        tex.SetPixel(24, 19, goldSkin);

        // Mắt đỏ tinh ranh
        tex.SetPixel(19, 15, redEyes);
        tex.SetPixel(18, 15, redEyes);

        // Chân chạy nhanh
        tex.SetPixel(13, 4, darkGold);
        tex.SetPixel(14, 3, darkGold);
        tex.SetPixel(18, 4, darkGold);
        tex.SetPixel(19, 3, darkGold);

        // Viền bóng
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (tex.GetPixel(x, y).a > 0.5f)
                {
                    if (tex.GetPixel(x - 1, y).a == 0 || tex.GetPixel(x + 1, y).a == 0 ||
                        tex.GetPixel(x, y - 1).a == 0 || tex.GetPixel(x, y + 1).a == 0)
                    {
                        tex.SetPixel(x, y, outline);
                    }
                }
            }
        }

        tex.Apply();
        _goblinSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24);
    }
}
