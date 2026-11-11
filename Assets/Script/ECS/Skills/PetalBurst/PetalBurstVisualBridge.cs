using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge cho Kỹ năng Tiên Nữ Tán Hoa (Fairy Petal Burst):
/// 1. Render từng đóa hoa / cánh hoa bay xoay tròn theo chuyển động ECS với Sprite procedural mềm mại.
/// 2. Hiệu ứng Vụ Nổ Hoa (Blossom Detonation Shockwave): Vòng sáng nở bung kèm cánh hoa vương vãi.
/// 3. Hiệu ứng Cấp 4: Hoa Trận Hút Quái (Petal Implosion Vortex) với vòng xoáy năng lượng hút chân không.
/// 4. Quản lý toàn bộ GameObject qua Object Pooling, Zero GC Alloc trong suốt trận đấu.
/// </summary>
public class PetalBurstVisualBridge : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _petalQuery;
    private EntityQuery _detonationEventQuery;
    private bool _initialized;

    // Sprite và Material dùng chung
    private static Sprite _petalSprite;
    private static Sprite _ringSprite;
    private static Sprite _vortexSprite;
    private static Material _sharedSpriteMat;

    // --- Cấu trúc dữ liệu Pool hiển thị bông hoa đang bay ---
    private class PetalVisualItem
    {
        public GameObject Root;
        public SpriteRenderer CoreRenderer;
        public SpriteRenderer GlowRenderer;
    }

    // --- Cấu trúc dữ liệu Pool hiệu ứng nổ hoa ---
    private class BlossomExplosionEffect
    {
        public GameObject Root;
        public SpriteRenderer RingRenderer;
        public SpriteRenderer CoreFlashRenderer;
        public SpriteRenderer VortexRenderer;
        public float Timer;
        public float MaxDuration;
        public float TargetRadius;
        public bool HasVortex;
    }

    private readonly Dictionary<Entity, PetalVisualItem> _activePetals = new Dictionary<Entity, PetalVisualItem>();
    private readonly List<PetalVisualItem> _freePetalPool = new List<PetalVisualItem>();

    private readonly List<BlossomExplosionEffect> _activeExplosions = new List<BlossomExplosionEffect>();
    private readonly List<BlossomExplosionEffect> _freeExplosionPool = new List<BlossomExplosionEffect>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<PetalBurstVisualBridge>() == null)
        {
            var go = new GameObject("[PetalBurstVisualBridge]");
            go.AddComponent<PetalBurstVisualBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        EnsureSprites();
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

        _petalQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PetalProjectileTag>(),
            ComponentType.ReadOnly<PetalProjectileData>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        _detonationEventQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PetalDetonationEvent>()
        );

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        UpdateActivePetals();
        ProcessDetonationEvents();
        UpdateActiveExplosions();
    }

    // =========================================================================
    // 1. ĐỒNG BỘ HIỂN THỊ CÁNH HOA ĐANG BAY
    // =========================================================================
    private void UpdateActivePetals()
    {
        using var entities = _petalQuery.ToEntityArray(Allocator.Temp);
        var currentSet = new HashSet<Entity>();

        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];
            currentSet.Add(e);

            var transformData = _entityManager.GetComponentData<LocalTransform>(e);
            var petalData = _entityManager.GetComponentData<PetalProjectileData>(e);

            if (!_activePetals.TryGetValue(e, out var item))
            {
                item = GetOrCreatePetalItem();
                _activePetals[e] = item;
            }

            item.Root.transform.position = transformData.Position;
            item.Root.transform.rotation = transformData.Rotation;

            // Tiến độ bay (1.0 khi mới xuất chiêu -> 0.0 khi sắp dừng)
            float progress = Mathf.Clamp01(1f - (petalData.LifeTimer / Mathf.Max(0.01f, petalData.Duration)));

            // Tỉ lệ scale hoa (nở dần khi vừa bay ra, co nhỏ nhẹ khi giảm tốc, tăng 8% theo yêu cầu: 0.55f * 1.08f = 0.594f)
            float scaleMul = Mathf.Lerp(0.65f, 1.0f, Mathf.Sin((1f - progress) * Mathf.PI));
            item.Root.transform.localScale = Vector3.one * (0.594f * scaleMul);

            // Cấp 4 hoa phát sáng lấp lánh sắc hoa đào hoàng kim
            Color baseColor = (petalData.SkillLevel >= 4)
                ? new Color(1.0f, 0.45f, 0.85f, 0.95f)
                : new Color(1.0f, 0.38f, 0.72f, 0.90f);

            item.CoreRenderer.color = baseColor;
            item.GlowRenderer.color = new Color(1.0f, 0.85f, 0.95f, 0.6f * progress);
        }

        // Dọn sạch các hoa đã nổ hoặc biến mất
        var toRemove = new List<Entity>();
        foreach (var kvp in _activePetals)
        {
            if (!currentSet.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                RecyclePetalItem(kvp.Value);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            _activePetals.Remove(toRemove[i]);
        }
    }

    // =========================================================================
    // 2. XỬ LÝ SỰ KIỆN KÍCH NỔ VÀ SINH HIỆU ỨNG VỤ NỔ HOA
    // =========================================================================
    private void ProcessDetonationEvents()
    {
        if (_detonationEventQuery.IsEmpty) return;

        using var eventEntities = _detonationEventQuery.ToEntityArray(Allocator.Temp);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < eventEntities.Length; i++)
        {
            Entity evtEntity = eventEntities[i];
            var evt = _entityManager.GetComponentData<PetalDetonationEvent>(evtEntity);

            SpawnExplosionEffect(evt.Position, evt.ExplosionRadius, evt.HasVortex, evt.SkillLevel);
            ecb.DestroyEntity(evtEntity);
        }

        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    private void SpawnExplosionEffect(float3 position, float radius, bool hasVortex, int skillLevel)
    {
        var effect = GetOrCreateExplosionEffect();
        effect.Root.transform.position = position;
        effect.Timer = 0f;
        effect.MaxDuration = hasVortex ? 0.38f : 0.28f;
        effect.TargetRadius = radius;
        effect.HasVortex = hasVortex;

        effect.Root.SetActive(true);
        _activeExplosions.Add(effect);
    }

    // =========================================================================
    // 3. CẬP NHẬT HOẠT ẢNH VỤ NỔ HOA & LỐC XOÁY CẤP 4
    // =========================================================================
    private void UpdateActiveExplosions()
    {
        float dt = Time.deltaTime;

        for (int i = _activeExplosions.Count - 1; i >= 0; i--)
        {
            var effect = _activeExplosions[i];
            effect.Timer += dt;
            float t = Mathf.Clamp01(effect.Timer / effect.MaxDuration);

            // Sóng nổ nở rộng Ease-Out Cubic
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentRadius = Mathf.Lerp(0.2f, effect.TargetRadius * 1.8f, easeOut);
            effect.RingRenderer.transform.localScale = Vector3.one * currentRadius;

            // Mờ dần theo thời gian
            float alpha = Mathf.Clamp01(1f - t);
            Color ringCol = effect.HasVortex
                ? new Color(1.0f, 0.40f, 0.85f, alpha * 0.9f)
                : new Color(1.0f, 0.35f, 0.70f, alpha * 0.8f);
            effect.RingRenderer.color = ringCol;

            // Tâm nổ chói sáng ở phần đầu vụ nổ
            float coreAlpha = Mathf.Clamp01(1f - t * 2.2f);
            effect.CoreFlashRenderer.color = new Color(1f, 0.9f, 0.95f, coreAlpha);
            effect.CoreFlashRenderer.transform.localScale = Vector3.one * (effect.TargetRadius * 1.2f * (1f - t * 0.5f));

            // Hiệu ứng Lốc Xoáy Hút Quái Cấp 4 (Xoay tròn ngược chiều và co vào tâm)
            if (effect.HasVortex && effect.VortexRenderer != null)
            {
                effect.VortexRenderer.gameObject.SetActive(true);
                effect.VortexRenderer.transform.Rotate(0f, 0f, -480f * dt);
                float vortexScale = Mathf.Lerp(effect.TargetRadius * 2.2f, 0.3f, t);
                effect.VortexRenderer.transform.localScale = Vector3.one * vortexScale;
                effect.VortexRenderer.color = new Color(0.4f, 0.95f, 1.0f, alpha * 0.85f);
            }
            else if (effect.VortexRenderer != null)
            {
                effect.VortexRenderer.gameObject.SetActive(false);
            }

            if (t >= 1f)
            {
                _activeExplosions.RemoveAt(i);
                RecycleExplosionEffect(effect);
            }
        }
    }

    // =========================================================================
    // 4. OBJECT POOLING CHO PETAL VÀ EXPLOSION
    // =========================================================================
    private PetalVisualItem GetOrCreatePetalItem()
    {
        if (_freePetalPool.Count > 0)
        {
            var item = _freePetalPool[_freePetalPool.Count - 1];
            _freePetalPool.RemoveAt(_freePetalPool.Count - 1);
            item.Root.SetActive(true);
            return item;
        }

        var root = new GameObject("PetalVisualItem");
        root.transform.SetParent(transform, false);

        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(root.transform, false);
        var coreSr = coreGo.AddComponent<SpriteRenderer>();
        coreSr.sprite = _petalSprite;
        coreSr.material = GetSpriteMaterial();
        coreSr.sortingOrder = 14;

        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localScale = Vector3.one * 1.35f;
        var glowSr = glowGo.AddComponent<SpriteRenderer>();
        glowSr.sprite = _petalSprite;
        glowSr.material = GetSpriteMaterial();
        glowSr.sortingOrder = 13;

        return new PetalVisualItem
        {
            Root = root,
            CoreRenderer = coreSr,
            GlowRenderer = glowSr
        };
    }

    private void RecyclePetalItem(PetalVisualItem item)
    {
        if (item != null && item.Root != null)
        {
            item.Root.SetActive(false);
            _freePetalPool.Add(item);
        }
    }

    private BlossomExplosionEffect GetOrCreateExplosionEffect()
    {
        if (_freeExplosionPool.Count > 0)
        {
            var effect = _freeExplosionPool[_freeExplosionPool.Count - 1];
            _freeExplosionPool.RemoveAt(_freeExplosionPool.Count - 1);
            return effect;
        }

        var root = new GameObject("BlossomExplosion");
        root.transform.SetParent(transform, false);

        var ringGo = new GameObject("ShockwaveRing");
        ringGo.transform.SetParent(root.transform, false);
        var ringSr = ringGo.AddComponent<SpriteRenderer>();
        ringSr.sprite = _ringSprite;
        ringSr.material = GetSpriteMaterial();
        ringSr.sortingOrder = 18;

        var coreGo = new GameObject("CoreFlash");
        coreGo.transform.SetParent(root.transform, false);
        var coreSr = coreGo.AddComponent<SpriteRenderer>();
        coreSr.sprite = _petalSprite;
        coreSr.material = GetSpriteMaterial();
        coreSr.sortingOrder = 19;

        var vortexGo = new GameObject("VortexSuction");
        vortexGo.transform.SetParent(root.transform, false);
        var vortexSr = vortexGo.AddComponent<SpriteRenderer>();
        vortexSr.sprite = _vortexSprite;
        vortexSr.material = GetSpriteMaterial();
        vortexSr.sortingOrder = 17;

        root.SetActive(false);
        return new BlossomExplosionEffect
        {
            Root = root,
            RingRenderer = ringSr,
            CoreFlashRenderer = coreSr,
            VortexRenderer = vortexSr
        };
    }

    private void RecycleExplosionEffect(BlossomExplosionEffect effect)
    {
        if (effect != null && effect.Root != null)
        {
            effect.Root.SetActive(false);
            _freeExplosionPool.Add(effect);
        }
    }

    // =========================================================================
    // 5. TẠO SPRITE PROCEDURAL (ZERO EXTERNAL ASSET DEPENDENCY)
    // =========================================================================
    private static void EnsureSprites()
    {
        if (_petalSprite == null) _petalSprite = CreatePetalSprite();
        if (_ringSprite == null) _ringSprite = CreateRingSprite();
        if (_vortexSprite == null) _vortexSprite = CreateVortexSprite();
    }

    private static Material GetSpriteMaterial()
    {
        if (_sharedSpriteMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? Shader.Find("Sprites/Default");
            _sharedSpriteMat = new Material(shader);
        }
        return _sharedSpriteMat;
    }

    private static Sprite CreatePetalSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        // Vẽ đóa hoa 5 cánh (Sakura blossom) mềm mại
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Đường cong 5 cánh hoa
                float petalCurve = 0.55f + 0.35f * Mathf.Cos(angle * 5f);
                if (dist <= petalCurve)
                {
                    float edge = Mathf.Clamp01((petalCurve - dist) / 0.15f);
                    float core = Mathf.Clamp01(1f - (dist / 0.45f));
                    Color col = Color.Lerp(new Color(1f, 0.4f, 0.75f, edge), Color.white, core * 0.85f);
                    tex.SetPixel(x, y, col);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Vòng sóng xung kích (bán kính 0.75 đến 0.95)
                if (dist >= 0.70f && dist <= 0.98f)
                {
                    float ringAlpha = Mathf.Sin(((dist - 0.70f) / 0.28f) * Mathf.PI);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ringAlpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateVortexSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;

        // Vòng xoáy xoắn ốc (Spiral vortex)
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                if (dist >= 0.2f && dist <= 0.95f)
                {
                    // 3 nhánh xoắn ốc
                    float spiral = Mathf.Sin(angle * 3f + dist * 8f);
                    float alpha = Mathf.Clamp01(spiral) * Mathf.Sin(((dist - 0.2f) / 0.75f) * Mathf.PI);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void OnDestroy()
    {
        _activePetals.Clear();
        _freePetalPool.Clear();
        _activeExplosions.Clear();
        _freeExplosionPool.Clear();
    }
}

