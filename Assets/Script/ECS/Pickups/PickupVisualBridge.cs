using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge hiển thị hình ảnh 2D cho các vật phẩm rơi (Heart, Magnet, Treasure Chest).
/// Tự động sinh Procedural Sprite HD-2D đẹp mắt, hỗ trợ animation nhấp nhô lơ lửng (Floating Bobbing)
/// và hiệu ứng hào quang lấp lánh (Sparkle/Glow).
/// </summary>
public class PickupVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _pickupQuery;
    private bool _initialized;

    private class VisualItem
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public PickupType Type;
        public float BaseY;
        public float PhaseOffset;
        public Vector3 LastPos;
    }

    private class ChestOpeningItem
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public Vector3 BasePos;
        public float ElapsedTime;
        public float CurrentScale;
        public bool Sparkled;
    }

    private readonly Dictionary<Entity, VisualItem> _activeVisuals = new Dictionary<Entity, VisualItem>();
    private readonly List<VisualItem> _pool = new List<VisualItem>();
    private readonly List<ChestOpeningItem> _chestOpenings = new List<ChestOpeningItem>();

    [Header("Custom Sprites")]
    [Tooltip("Gán sprite trái tim hồi máu tùy chỉnh (nếu để trống, hệ thống tự nạp từ Assets/Sprite/Heart.png)")]
    public Sprite CustomHeartSprite;

    [Tooltip("Gán sprite rương đóng RPG Chests_2 (nếu để trống, tự nạp từ Assets/Sprite/RPG Chests.png)")]
    public Sprite CustomChestClosed;

    [Tooltip("Gán sprite rương hé mở RPG Chests_11")]
    public Sprite CustomChestOpen1;

    [Tooltip("Gán sprite rương mở nửa RPG Chests_20")]
    public Sprite CustomChestOpen2;

    [Tooltip("Gán sprite rương mở hoàn toàn RPG Chests_29")]
    public Sprite CustomChestOpen3;

    // Static properties để các hệ thống khác (UI, GameHUDBridge) có thể truy cập sprite rương
    public static Sprite ChestClosedSprite { get; private set; }
    public static Sprite ChestOpenFrame1 { get; private set; }
    public static Sprite ChestOpenFrame2 { get; private set; }
    public static Sprite ChestOpenFrame3 { get; private set; }

    private Sprite _heartSprite;
    private Sprite _magnetSprite;
    private Sprite _chestSprite;
    private Sprite _chestSpriteClosed;
    private Sprite _chestSpriteOpen1;
    private Sprite _chestSpriteOpen2;
    private Sprite _chestSpriteOpen3;
    private Sprite _glowCircleSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<PickupVisualBridge>() == null)
        {
            var go = new GameObject("[PickupVisualBridge]");
            go.AddComponent<PickupVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        GenerateProceduralSprites();
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
        _pickupQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PickupItemTag>(),
            ComponentType.ReadOnly<PickupData>(),
            ComponentType.ReadOnly<LocalTransform>());

        _initialized = true;
        return true;
    }

    private void Update()
    {
        if (!_initialized && !InitECS()) return;

        SyncVisuals();
        UpdateChestOpenings();
    }

    private void SyncVisuals()
    {
        if (_pickupQuery.IsEmpty)
        {
            if (_activeVisuals.Count > 0)
            {
                foreach (var kvp in _activeVisuals)
                {
                    if (kvp.Value.Type == PickupType.TreasureChest)
                    {
                        PlayChestOpenAnimation(kvp.Value.LastPos, 1.2f);
                    }
                    kvp.Value.Root.SetActive(false);
                    _pool.Add(kvp.Value);
                }
                _activeVisuals.Clear();
            }
            return;
        }

        var entities = _pickupQuery.ToEntityArray(Allocator.Temp);
        var transforms = _pickupQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var datas = _pickupQuery.ToComponentDataArray<PickupData>(Allocator.Temp);

        var currentAlive = new HashSet<Entity>();

        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];
            currentAlive.Add(e);

            Vector3 basePos = new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f);
            PickupType type = datas[i].Type;

            if (!_activeVisuals.TryGetValue(e, out VisualItem item))
            {
                item = GetOrCreateVisual(type);
                item.Type = type;
                item.PhaseOffset = (e.Index % 10) * 0.6f;
                item.Root.SetActive(true);
                _activeVisuals[e] = item;
            }

            // Cập nhật Sprite nếu loại thay đổi
            if (item.Type != type)
            {
                item.Type = type;
                ApplySprite(item.Renderer, type);
            }

            item.LastPos = basePos;

            // Animation nhấp nhô lơ lửng nhẹ nhàng (Floating / Bobbing)
            float floatOffset = Mathf.Sin(Time.time * 4f + item.PhaseOffset) * 0.08f;
            item.Root.transform.position = basePos + new Vector3(0f, floatOffset, 0f);

            // Scale và animation thở/nhịp đập tùy theo từng loại vật phẩm
            if (type == PickupType.TreasureChest)
            {
                float breathe = 1.0f + Mathf.Sin(Time.time * 3f + item.PhaseOffset) * 0.05f;
                item.Root.transform.localScale = new Vector3(1.2f * breathe, 1.2f * breathe, 1f);
            }
            else if (type == PickupType.HealthHeart)
            {
                // Trái tim đập nhẹ nhàng (Heartbeat pulse), giữ đúng tỉ lệ gốc do người dùng đã chỉnh trong Sprite Editor
                float pulse = 1.0f + Mathf.Sin(Time.time * 4.5f + item.PhaseOffset) * 0.05f;
                item.Root.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
            else
            {
                item.Root.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            }
        }

        // Dọn các visual của entity đã bị nhặt / hủy
        var toRemove = new List<Entity>();
        foreach (var kvp in _activeVisuals)
        {
            if (!currentAlive.Contains(kvp.Key))
            {
                if (kvp.Value.Type == PickupType.TreasureChest)
                {
                    PlayChestOpenAnimation(kvp.Value.LastPos, 1.2f);
                }
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

    private VisualItem GetOrCreateVisual(PickupType type)
    {
        VisualItem item;
        if (_pool.Count > 0)
        {
            item = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
        }
        else
        {
            var go = new GameObject("[PickupVisual]");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 30; // Nằm trên quái và ngọc EXP
            item = new VisualItem { Root = go, Renderer = sr };
        }

        ApplySprite(item.Renderer, type);
        return item;
    }

    private void ApplySprite(SpriteRenderer sr, PickupType type)
    {
        switch (type)
        {
            case PickupType.HealthHeart:
                sr.sprite = _heartSprite;
                sr.color = Color.white;
                break;
            case PickupType.ExpMagnet:
                sr.sprite = _magnetSprite;
                sr.color = Color.white;
                break;
            case PickupType.TreasureChest:
                sr.sprite = _chestSprite;
                sr.color = Color.white;
                break;
        }
    }

    // =========================================================================
    // SPRITE LOADER & PROCEDURAL GENERATOR
    // =========================================================================
    private void GenerateProceduralSprites()
    {
        _heartSprite = LoadOrGenerateHeartSprite();
        _magnetSprite = CreateMagnetSprite();
        LoadOrGenerateChestSprites();
    }

    /// <summary>
    /// Đảm bảo Sprite Rương được tải sẵn để các hệ thống UI (GameHUDBridge) có thể truy cập ngay lập tức.
    /// </summary>
    public static void EnsureSpritesLoaded()
    {
        if (ChestClosedSprite != null && ChestOpenFrame1 != null && ChestOpenFrame2 != null && ChestOpenFrame3 != null) return;

#if UNITY_EDITOR
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprite/RPG Chests.png");
        if (assets != null)
        {
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    string nameClean = s.name.Trim();
                    if (nameClean == "RPG Chests_2" || nameClean == "RPGChests_2")
                        ChestClosedSprite = s;
                    else if (nameClean == "RPG Chests_11" || nameClean == "RPGChests_11")
                        ChestOpenFrame1 = s;
                    else if (nameClean == "RPG Chests_20" || nameClean == "RPGChests_20")
                        ChestOpenFrame2 = s;
                    else if (nameClean == "RPG Chests_29" || nameClean == "RPGChests_29")
                        ChestOpenFrame3 = s;
                }
            }
        }
#endif
    }

    private void LoadOrGenerateChestSprites()
    {
        if (CustomChestClosed != null) ChestClosedSprite = CustomChestClosed;
        if (CustomChestOpen1 != null) ChestOpenFrame1 = CustomChestOpen1;
        if (CustomChestOpen2 != null) ChestOpenFrame2 = CustomChestOpen2;
        if (CustomChestOpen3 != null) ChestOpenFrame3 = CustomChestOpen3;

        EnsureSpritesLoaded();

        if (ChestClosedSprite == null) ChestClosedSprite = CreateChestSprite();
        if (ChestOpenFrame1 == null) ChestOpenFrame1 = ChestClosedSprite;
        if (ChestOpenFrame2 == null) ChestOpenFrame2 = ChestClosedSprite;
        if (ChestOpenFrame3 == null) ChestOpenFrame3 = ChestClosedSprite;

        _chestSpriteClosed = ChestClosedSprite;
        _chestSpriteOpen1 = ChestOpenFrame1;
        _chestSpriteOpen2 = ChestOpenFrame2;
        _chestSpriteOpen3 = ChestOpenFrame3;
        _chestSprite = _chestSpriteClosed;
    }

    /// <summary>
    /// Kích hoạt hoạt ảnh mở rương RPG Chests: RPG Chests_2 -> RPG Chests_11 -> RPG Chests_20 -> RPG Chests_29
    /// </summary>
    public void PlayChestOpenAnimation(Vector3 pos, float scale = 1.2f)
    {
        var go = new GameObject("[ChestOpeningAnim]");
        go.transform.SetParent(transform);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 35; // Nằm trên quái & ngọc EXP
        sr.sprite = _chestSpriteClosed != null ? _chestSpriteClosed : _chestSprite;
        sr.color = Color.white;

        var openingItem = new ChestOpeningItem
        {
            Root = go,
            Renderer = sr,
            BasePos = pos,
            CurrentScale = scale,
            ElapsedTime = 0f,
            Sparkled = false
        };

        _chestOpenings.Add(openingItem);
    }

    private void UpdateChestOpenings()
    {
        if (_chestOpenings.Count == 0) return;

        float dt = Time.unscaledDeltaTime; // Chạy theo unscaled time để mượt mà khi nhận rương gây Pause game

        for (int i = _chestOpenings.Count - 1; i >= 0; i--)
        {
            var item = _chestOpenings[i];
            if (item.Root == null)
            {
                _chestOpenings.RemoveAt(i);
                continue;
            }

            item.ElapsedTime += dt;
            float t = item.ElapsedTime;

            // Thứ tự diễn hoạt:
            // 0.00s - 0.09s: RPG Chests_2 (Đóng)
            // 0.09s - 0.18s: RPG Chests_11 (Hé mở)
            // 0.18s - 0.28s: RPG Chests_20 (Mở nửa)
            // 0.28s - 1.80s: RPG Chests_29 (Mở hoàn toàn)
            // 1.80s - 2.20s: Mờ dần (Fade out)
            // > 2.20s: Kết thúc

            if (t < 0.09f)
            {
                item.Renderer.sprite = _chestSpriteClosed != null ? _chestSpriteClosed : _chestSprite;
                item.Root.transform.position = item.BasePos;
            }
            else if (t < 0.18f)
            {
                item.Renderer.sprite = _chestSpriteOpen1 != null ? _chestSpriteOpen1 : _chestSprite;
                item.Root.transform.position = item.BasePos;
            }
            else if (t < 0.28f)
            {
                item.Renderer.sprite = _chestSpriteOpen2 != null ? _chestSpriteOpen2 : _chestSprite;
                // Bù trừ Pivot Y: Chests_20 (cao 21px, pivot Y 10.5) so với Chests_2 (cao 18px, pivot Y 9)
                // (21 - 18) / (2 * 16 PPU) = +0.09375m
                float yOffset = (21f - 18f) / (2f * 16f) * item.CurrentScale;
                item.Root.transform.position = item.BasePos + new Vector3(0f, yOffset, 0f);
            }
            else
            {
                item.Renderer.sprite = _chestSpriteOpen3 != null ? _chestSpriteOpen3 : _chestSprite;
                // Bù trừ Pivot Y: Chests_29 (cao 22px, pivot Y 11) so với Chests_2 (cao 18px, pivot Y 9)
                // (22 - 18) / (2 * 16 PPU) = +0.125m
                float yOffset = (22f - 18f) / (2f * 16f) * item.CurrentScale;
                item.Root.transform.position = item.BasePos + new Vector3(0f, yOffset, 0f);

                if (!item.Sparkled)
                {
                    item.Sparkled = true;
                    item.Root.transform.localScale = new Vector3(item.CurrentScale * 1.22f, item.CurrentScale * 1.22f, 1f);
                    SpawnGoldenChestBurst(item.BasePos);
                }
                else
                {
                    float currentS = item.Root.transform.localScale.x;
                    float targetS = item.CurrentScale;
                    float newS = Mathf.MoveTowards(currentS, targetS, dt * 2.5f);
                    item.Root.transform.localScale = new Vector3(newS, newS, 1f);
                }

                if (t >= 1.8f)
                {
                    float fadeT = (t - 1.8f) / 0.4f;
                    float alpha = Mathf.Clamp01(1f - fadeT);
                    item.Renderer.color = new Color(1f, 1f, 1f, alpha);
                }
            }

            if (t >= 2.2f)
            {
                Destroy(item.Root);
                _chestOpenings.RemoveAt(i);
            }
        }
    }

    private void SpawnGoldenChestBurst(Vector3 pos)
    {
        var go = new GameObject("[ChestGlowBurst]");
        go.transform.SetParent(transform);
        go.transform.position = pos + new Vector3(0f, 0.15f, 0f);
        go.transform.localScale = Vector3.one * 0.4f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 36;
        sr.sprite = GetOrCreateGlowCircleSprite();
        sr.color = new Color(1f, 0.9f, 0.35f, 0.85f);

        var fader = go.AddComponent<ChestBurstFader>();
        fader.Init(0.55f);
    }

    private Sprite GetOrCreateGlowCircleSprite()
    {
        if (_glowCircleSprite != null) return _glowCircleSprite;

        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        float maxR = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(r / maxR);
                float alpha = Mathf.SmoothStep(1f, 0f, t);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        _glowCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
        return _glowCircleSprite;
    }

    private Sprite LoadOrGenerateHeartSprite()
    {
        // 1. Kiểm tra Sprite tùy chỉnh gán trong Inspector (nếu có)
        if (CustomHeartSprite != null) return CustomHeartSprite;

        // 2. Nạp file ảnh Sprite do người dùng import tại Assets/Sprite/Heart.png
#if UNITY_EDITOR
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprite/Heart.png");
        if (assets != null)
        {
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    return s;
                }
            }
        }
#endif

        // 3. Nạp từ Resources nếu có
        var fromResources = Resources.Load<Sprite>("Heart");
        if (fromResources != null) return fromResources;

        var allFromResources = Resources.LoadAll<Sprite>("Heart");
        if (allFromResources != null && allFromResources.Length > 0) return allFromResources[0];

        // 4. Fallback: Tự động vẽ procedural nếu không tìm thấy file ảnh
        return CreateHeartSprite();
    }

    private Sprite CreateHeartSprite()
    {
        int size = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color empty = new Color(0, 0, 0, 0);
        Color red = new Color(0.95f, 0.15f, 0.25f, 1f);
        Color darkRed = new Color(0.6f, 0.05f, 0.1f, 1f);
        Color highlight = new Color(1f, 0.65f, 0.75f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, empty);
            }
        }

        // Vẽ hình trái tim đối xứng
        for (int y = 3; y < 21; y++)
        {
            for (int x = 2; x < 22; x++)
            {
                float nx = (x - 11.5f) / 9.5f;
                float ny = (y - 12.0f) / 9.5f;
                // Công thức đường viền trái tim
                float f = (nx * nx + ny * ny - 1f);
                if (f * f * f - nx * nx * ny * ny * ny <= 0.05f)
                {
                    Color col = (ny > 0.3f && nx < -0.2f) ? highlight : red;
                    tex.SetPixel(x, y, col);
                }
            }
        }

        // Vẽ viền tối
        for (int y = 2; y < 22; y++)
        {
            for (int x = 1; x < 23; x++)
            {
                if (tex.GetPixel(x, y).a > 0.5f)
                {
                    if (tex.GetPixel(x - 1, y).a == 0 || tex.GetPixel(x + 1, y).a == 0 ||
                        tex.GetPixel(x, y - 1).a == 0 || tex.GetPixel(x, y + 1).a == 0)
                    {
                        tex.SetPixel(x, y, darkRed);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24);
    }

    private Sprite CreateMagnetSprite()
    {
        int size = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color empty = new Color(0, 0, 0, 0);
        Color cyan = new Color(0.1f, 0.85f, 1f, 1f);
        Color silver = new Color(0.85f, 0.88f, 0.95f, 1f);
        Color red = new Color(0.95f, 0.2f, 0.25f, 1f);
        Color darkBorder = new Color(0.1f, 0.15f, 0.25f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, empty);
            }
        }

        // Vẽ hình móng ngựa (U-shape)
        for (int y = 3; y < 21; y++)
        {
            for (int x = 3; x < 21; x++)
            {
                float dx = x - 11.5f;
                float dy = y - 11.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                // Phần vòng tròn cung đáy (y <= 12)
                if (y <= 12 && r >= 4.5f && r <= 8.5f)
                {
                    tex.SetPixel(x, y, silver);
                }
                // Hai nhánh đứng của nam châm (y > 12)
                else if (y > 12)
                {
                    if ((x >= 3 && x <= 7) || (x >= 16 && x <= 20))
                    {
                        // 2 đầu cực màu đỏ và cyan
                        Color tipColor = (x < 10) ? red : cyan;
                        tex.SetPixel(x, y, (y >= 16) ? tipColor : silver);
                    }
                }
            }
        }

        // Viền bóng
        for (int y = 1; y < 23; y++)
        {
            for (int x = 1; x < 23; x++)
            {
                if (tex.GetPixel(x, y).a > 0.5f)
                {
                    if (tex.GetPixel(x - 1, y).a == 0 || tex.GetPixel(x + 1, y).a == 0 ||
                        tex.GetPixel(x, y - 1).a == 0 || tex.GetPixel(x, y + 1).a == 0)
                    {
                        tex.SetPixel(x, y, darkBorder);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24);
    }

    private Sprite CreateChestSprite()
    {
        int size = 26;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color empty = new Color(0, 0, 0, 0);
        Color gold = new Color(1f, 0.82f, 0.15f, 1f);
        Color goldLight = new Color(1f, 0.95f, 0.5f, 1f);
        Color brown = new Color(0.6f, 0.35f, 0.15f, 1f);
        Color darkBrown = new Color(0.3f, 0.16f, 0.05f, 1f);
        Color iron = new Color(0.2f, 0.22f, 0.28f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, empty);
            }
        }

        // Thân rương (y: 4 to 18, x: 3 to 22)
        for (int y = 4; y <= 18; y++)
        {
            for (int x = 3; x <= 22; x++)
            {
                Color c = brown;
                // Nẹp vàng viền ngoài
                if (x == 3 || x == 22 || x == 4 || x == 21 || y == 4 || y == 18)
                {
                    c = (x == 4 || y == 18) ? goldLight : gold;
                }
                // Khóa vàng ở giữa
                if (x >= 11 && x <= 14 && y >= 9 && y <= 13)
                {
                    c = (x == 12 && y == 11) ? darkBrown : goldLight;
                }
                // Đường rãnh mở nắp rương
                if (y == 13)
                {
                    c = iron;
                }
                tex.SetPixel(x, y, c);
            }
        }

        // Viền ngoài đậm
        for (int y = 3; y <= 19; y++)
        {
            for (int x = 2; x <= 23; x++)
            {
                if (tex.GetPixel(x, y).a > 0.5f)
                {
                    if (tex.GetPixel(x - 1, y).a == 0 || tex.GetPixel(x + 1, y).a == 0 ||
                        tex.GetPixel(x, y - 1).a == 0 || tex.GetPixel(x, y + 1).a == 0)
                    {
                        tex.SetPixel(x, y, darkBrown);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24);
    }
}

/// <summary>
/// Hiệu ứng hào quang vàng nở rộng và mờ dần khi mở Rương Cổ Vật.
/// </summary>
public class ChestBurstFader : MonoBehaviour
{
    private float _duration = 0.55f;
    private float _elapsed = 0f;
    private SpriteRenderer _sr;
    private Vector3 _startScale;

    public void Init(float duration)
    {
        _duration = duration;
        _sr = GetComponent<SpriteRenderer>();
        _startScale = transform.localScale;
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(_elapsed / _duration);

        float scaleMul = Mathf.Lerp(1f, 3.5f, progress);
        transform.localScale = _startScale * scaleMul;

        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = Mathf.Lerp(0.85f, 0f, progress);
            _sr.color = c;
        }

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
