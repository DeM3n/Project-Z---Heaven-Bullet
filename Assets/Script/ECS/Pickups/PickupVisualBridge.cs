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
    }

    private readonly Dictionary<Entity, VisualItem> _activeVisuals = new Dictionary<Entity, VisualItem>();
    private readonly List<VisualItem> _pool = new List<VisualItem>();

    private Sprite _heartSprite;
    private Sprite _magnetSprite;
    private Sprite _chestSprite;

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
    }

    private void SyncVisuals()
    {
        if (_pickupQuery.IsEmpty)
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

            // Animation nhấp nhô lơ lửng nhẹ nhàng (Floating / Bobbing)
            float floatOffset = Mathf.Sin(Time.time * 4f + item.PhaseOffset) * 0.08f;
            item.Root.transform.position = basePos + new Vector3(0f, floatOffset, 0f);

            // Rương Cổ Vật to hơn và có hiệu ứng scale thở (breathing) nhẹ
            if (type == PickupType.TreasureChest)
            {
                float breathe = 1.0f + Mathf.Sin(Time.time * 3f + item.PhaseOffset) * 0.06f;
                item.Root.transform.localScale = new Vector3(1.4f * breathe, 1.4f * breathe, 1f);
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
    // PROCEDURAL PIXEL-ART SPRITE GENERATOR (32x32)
    // =========================================================================
    private void GenerateProceduralSprites()
    {
        _heartSprite = CreateHeartSprite();
        _magnetSprite = CreateMagnetSprite();
        _chestSprite = CreateChestSprite();
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
