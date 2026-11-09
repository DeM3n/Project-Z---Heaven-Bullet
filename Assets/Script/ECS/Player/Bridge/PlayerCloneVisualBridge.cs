using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Bridge quản lý hiển thị trực quan cho các Phân Thân Ảo Ảnh (Player Clones):
/// - Tự động đồng bộ vị trí, hướng quay mặt (Sprite Flip) theo dữ liệu ECS.
/// - Áp dụng hình dạng và kích thước tương thích với Player (0.8m x 0.8m) kèm texture fallback chuẩn.
/// - Áp dụng màu sắc bán trong suốt huyền ảo (Ethereal Cyan Ghost Tint kèm nhịp thở ma thuật nhẹ).
/// - Hiển thị vòng hào quang Khiên Hộ Thể xoay tròn quanh từng phân thân khi khiên kích hoạt.
/// </summary>
public class PlayerCloneVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _cloneQuery;
    private EntityQuery _playerQuery;
    private bool _initialized;

    // Visual GameObjects cho 2 phân thân
    private readonly GameObject[] _cloneObjects = new GameObject[2];
    private readonly SpriteRenderer[] _cloneRenderers = new SpriteRenderer[2];
    private readonly LineRenderer[] _cloneShieldLines = new LineRenderer[2];

    private SpriteRenderer _playerRenderer;
    private static Sprite _defaultGhostSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<PlayerCloneVisualBridge>() == null)
        {
            var go = new GameObject("[PlayerCloneVisualBridge]");
            go.AddComponent<PlayerCloneVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        InitECS();
        CreateCloneVisuals();
    }

    private bool InitECS()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        _entityManager = world.EntityManager;
        _cloneQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerCloneTag>(),
            ComponentType.ReadOnly<PlayerCloneData>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<PlayerShieldData>()
        );
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<LocalTransform>()
        );
        _initialized = true;
        return true;
    }

    /// <summary>
    /// Tạo texture khối vuông màu trắng sắc nét (32x32) để làm sprite đại diện cho Ghost Clone.
    /// Với pixelsPerUnit = 32, kích thước chuẩn là 1x1m (khi scale 0.8 sẽ khớp hoàn toàn kích thước của Player).
    /// </summary>
    private static Sprite GetDefaultGhostSprite()
    {
        if (_defaultGhostSprite != null) return _defaultGhostSprite;

        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var cols = new Color[size * size];
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i] = Color.white;
        }
        tex.SetPixels(cols);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _defaultGhostSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _defaultGhostSprite;
    }

    private void CreateCloneVisuals()
    {
        for (int i = 0; i < 2; i++)
        {
            var cloneObj = new GameObject($"[CloneVisual_{i}]");
            cloneObj.transform.SetParent(transform, false);
            cloneObj.transform.localScale = new Vector3(0.8f, 0.8f, 1f); // Chuẩn kích thước Player (0.8 x 0.8)

            var sr = cloneObj.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 5; // Hiển thị trên nền sân, hài hòa cùng lớp nhân vật
            sr.sprite = GetDefaultGhostSprite();
            sr.color = new Color(0.40f, 0.85f, 1f, 0.72f); // Ethereal Cyan Ghost Tint

            // LineRenderer cho Khiên Hộ Thể của phân thân (vòng lục giác)
            var shieldObj = new GameObject("ShieldHex");
            shieldObj.transform.SetParent(cloneObj.transform, false);

            var line = shieldObj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 6;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.35f, 0.85f, 1f, 0.8f);
            line.material = mat;
            line.startColor = new Color(0.35f, 0.85f, 1f, 0.8f);
            line.endColor = new Color(0.35f, 0.85f, 1f, 0.8f);

            // Tạo các điểm lục giác bán kính 0.85m
            float hexRadius = 0.85f;
            for (int h = 0; h < 6; h++)
            {
                float ang = h * 60f * Mathf.Deg2Rad;
                line.SetPosition(h, new Vector3(Mathf.Cos(ang) * hexRadius, Mathf.Sin(ang) * hexRadius, 0f));
            }

            shieldObj.SetActive(false);

            _cloneObjects[i] = cloneObj;
            _cloneRenderers[i] = sr;
            _cloneShieldLines[i] = line;
            cloneObj.SetActive(false);
        }
    }

    private SpriteRenderer FindPlayerRenderer()
    {
        if (_playerQuery.IsEmpty) return null;
        var playerEntity = _playerQuery.GetSingletonEntity();
        if (!_entityManager.Exists(playerEntity)) return null;

#pragma warning disable CS0618
        if (_entityManager.HasComponent<SpriteRenderer>(playerEntity))
        {
            var sr = _entityManager.GetComponentObject<SpriteRenderer>(playerEntity);
            if (sr != null) return sr;
        }
#pragma warning restore CS0618

        var playerGo = GameObject.Find("Player(Clone)") ?? GameObject.Find("Player");
        if (playerGo != null)
        {
            var sr = playerGo.GetComponent<SpriteRenderer>();
            if (sr != null) return sr;
        }

        var srs = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
        foreach (var s in srs)
        {
            if (s.name.StartsWith("[CloneVisual")) continue;
            if (s.name.ToLower().Contains("player"))
            {
                return s;
            }
        }

        return null;
    }

    private void LateUpdate()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (!_initialized || world == null || !world.IsCreated)
        {
            if (!InitECS()) return;
        }

        if (_cloneQuery.IsEmpty)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_cloneObjects[i] != null && _cloneObjects[i].activeSelf)
                    _cloneObjects[i].SetActive(false);
            }
            return;
        }

        if (_playerRenderer == null)
        {
            _playerRenderer = FindPlayerRenderer();
        }

        var transforms = _cloneQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var cloneDatas = _cloneQuery.ToComponentDataArray<PlayerCloneData>(Allocator.Temp);
        var shieldDatas = _cloneQuery.ToComponentDataArray<PlayerShieldData>(Allocator.Temp);

        // Đánh dấu các visual đã cập nhật trong frame
        bool[] updated = new bool[2];

        for (int i = 0; i < cloneDatas.Length; i++)
        {
            int idx = cloneDatas[i].CloneIndex;
            if (idx < 0 || idx >= 2) continue;

            updated[idx] = true;
            var obj = _cloneObjects[idx];
            var sr = _cloneRenderers[idx];
            var line = _cloneShieldLines[idx];

            if (!obj.activeSelf) obj.SetActive(true);

            // Cập nhật vị trí (khóa Z = 0 để đảm bảo hiển thị đúng phẳng 2D)
            Vector3 pos = transforms[i].Position;
            pos.z = 0f;
            obj.transform.position = pos;

            // Đồng bộ Sprite và Kích thước theo Player (hoặc fallback hình vuông 0.8x0.8)
            if (_playerRenderer != null && _playerRenderer.sprite != null)
            {
                sr.sprite = _playerRenderer.sprite;
                obj.transform.localScale = _playerRenderer.transform.localScale;
            }
            else
            {
                if (sr.sprite == null) sr.sprite = GetDefaultGhostSprite();
                obj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            }

            // Nhịp thở mờ ảo nhẹ nhàng (Ethereal Ghost Alpha Pulse: dao động 0.62 - 0.78)
            float breath = 0.70f + 0.08f * Mathf.Sin(Time.time * 3.5f + idx * Mathf.PI);
            sr.color = new Color(0.40f, 0.85f, 1f, breath);

            // Lật mặt theo hướng di chuyển của Clone
            sr.flipX = cloneDatas[i].FacingDirection < 0f;

            // Xoay nhẹ vòng khiên bảo vệ nếu có khiên
            bool hasShield = shieldDatas[i].SkillLevel > 0 && shieldDatas[i].CurrentCharges > 0 && shieldDatas[i].HitsRemainingInCurrentShield > 0;
            if (line != null)
            {
                line.gameObject.SetActive(hasShield);
                if (hasShield)
                {
                    line.transform.Rotate(0f, 0f, 45f * Time.unscaledDeltaTime);
                }
            }
        }

        for (int i = 0; i < 2; i++)
        {
            if (!updated[i] && _cloneObjects[i] != null && _cloneObjects[i].activeSelf)
            {
                _cloneObjects[i].SetActive(false);
            }
        }

        transforms.Dispose();
        cloneDatas.Dispose();
        shieldDatas.Dispose();
    }
}
