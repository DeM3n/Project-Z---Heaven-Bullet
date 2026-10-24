using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Bridge đồng bộ TrailRenderer theo các thực thể Phi Kiếm (FlyingSword) trong ECS.
/// Giúp hiển thị vệt kiếm bay uốn lượn đẹp mắt mà vẫn giữ nguyên kiến trúc Pure ECS.
/// </summary>
public class FlyingSwordTrailBridge : MonoBehaviour
{
    [Header("Trail Settings")]
    [Tooltip("Thời gian tồn tại của vệt đuôi (giữ ngắn ~0.15s - 0.2s để không bị kéo quá dài)")]
    [SerializeField] private float trailTime = 0.18f;
    [SerializeField] private float startWidth = 0.22f;
    [SerializeField] private float endWidth = 0.02f;
    [SerializeField] private Color trailColor = new Color(0.2f, 0.88f, 1f, 0.85f);
    [SerializeField] private Material trailMaterial;

    private EntityManager _entityManager;
    private EntityQuery _swordQuery;
    private bool _initialized;

    private readonly List<TrailRenderer> _trailPool = new List<TrailRenderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<FlyingSwordTrailBridge>() == null)
        {
            var go = new GameObject("[FlyingSwordTrailBridge]");
            go.AddComponent<FlyingSwordTrailBridge>();
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
            ComponentType.ReadOnly<FlyingSwordTag>(),
            ComponentType.ReadOnly<LocalTransform>());

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;

        if (_swordQuery.IsEmpty)
        {
            // Tắt tất cả trail nếu không có kiếm
            for (int i = 0; i < _trailPool.Count; i++)
            {
                if (_trailPool[i] != null && _trailPool[i].gameObject.activeSelf)
                {
                    _trailPool[i].emitting = false;
                    _trailPool[i].gameObject.SetActive(false);
                }
            }
            return;
        }

        var transforms = _swordQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        int swordCount = transforms.Length;

        // Đảm bảo pool đủ số lượng TrailRenderer cho từng kiếm
        while (_trailPool.Count < swordCount)
        {
            _trailPool.Add(CreateNewTrailObject());
        }

        // Cập nhật vị trí của từng Trail theo kiếm tương ứng
        for (int i = 0; i < _trailPool.Count; i++)
        {
            var trail = _trailPool[i];
            if (i < swordCount)
            {
                if (!trail.gameObject.activeSelf)
                {
                    trail.gameObject.SetActive(true);
                    trail.Clear();
                }

                Vector3 targetPos = new Vector3(
                    transforms[i].Position.x,
                    transforms[i].Position.y,
                    transforms[i].Position.z);

                // Nếu kiếm dịch chuyển tức thời quá xa (> 4m) -> Clear trail để tránh vệt gạch ngang màn hình
                if (Vector3.Distance(trail.transform.position, targetPos) > 4f)
                {
                    trail.Clear();
                }

                trail.transform.position = targetPos;
                trail.transform.rotation = transforms[i].Rotation;
                trail.emitting = true;
            }
            else
            {
                if (trail.gameObject.activeSelf)
                {
                    trail.emitting = false;
                    trail.gameObject.SetActive(false);
                }
            }
        }

        transforms.Dispose();
    }

    private TrailRenderer CreateNewTrailObject()
    {
        var trailObj = new GameObject($"SwordTrail_{_trailPool.Count}");
        trailObj.transform.SetParent(transform);

        var trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.minVertexDistance = 0.04f;
        trail.sortingOrder = 0; // Đặt sau sprite thanh kiếm (order 1)

        // Cấu hình độ rộng thon gọn dần về đuôi
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, startWidth);
        widthCurve.AddKey(1f, endWidth);
        trail.widthCurve = widthCurve;

        // Cấu hình Gradient màu sáng ngọc (Cyan) mờ dần
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;

        // Sử dụng Material thích hợp (Default-Line hoặc Sprite Unlit)
        if (trailMaterial != null)
        {
            trail.material = trailMaterial;
        }
        else
        {
            var defaultShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                               ?? Shader.Find("Sprites/Default");
            if (defaultShader != null)
            {
                trail.material = new Material(defaultShader);
            }
        }

        return trail;
    }
}
