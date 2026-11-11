using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Hybrid Visual Bridge quản lý hiệu ứng thị giác nảy đòn theo phong cách Minecraft cho Quái vật (Enemy Hit Reaction):
/// 1. Minecraft Red Damage Flash: Sprite quái lập tức chuyển sang sắc đỏ tươi (kèm chớp sáng va chạm ở mili-giây đầu tiên).
/// 2. Damage Tilt: Nghiêng nảy thân hình theo góc xoay (+/- 12 độ), đảo chiều luân phiên trái/phải cho mỗi lần trúng đòn mới.
/// 3. Squash & Stretch: Đàn hồi co giãn nhẹ (co Y, dãn X) tạo cảm giác lực chém/bắn có trọng lượng giòn giã.
/// 4. Tự động hoàn trả màu sắc, góc xoay và kích thước ban đầu của từng loại quái khi hết thời gian hiệu ứng.
/// 
/// Tối ưu hóa hiệu năng cao độ (Performance First):
/// - Sử dụng IEnableableComponent (EnemyHitReactionData) để lọc trực tiếp ở tầng Chunk Bitmask.
/// - 90-95% quái nhàn rỗi được bỏ qua ở cấp độ phần cứng, LateUpdate chỉ xử lý 5-15 quái đang bị dính đòn.
/// - Zero GC Allocation: Không dùng LINQ, không tạo bộ nhớ rác, giải phóng tức thì khi quái tử trận.
/// </summary>
public class EnemyHitVisualBridge : MonoBehaviour
{
    [Header("Minecraft-Style Red Flash")]
    [Tooltip("Sắc đỏ đặc trưng khi bị trúng đòn (tương tự mob Minecraft)")]
    [SerializeField] private Color hitRedColor = new Color(1f, 0.22f, 0.22f, 1f);

    [Tooltip("Sắc chớp sáng ở đỉnh va đập ban đầu giúp quái màu đỏ (như Chaser) vẫn nổi bật rực rỡ")]
    [SerializeField] private Color hitWhiteFlashColor = new Color(1f, 0.85f, 0.85f, 1f);

    [Header("Damage Tilt")]
    [Tooltip("Góc nghiêng tối đa khi nảy đòn (độ)")]
    [SerializeField] private float maxTiltAngle = 12f;

    [Header("Squash & Stretch")]
    [Tooltip("Độ co giãn đàn hồi khi trúng đòn (0.15 = 15% biến dạng)")]
    [SerializeField] private float punchScaleRatio = 0.15f;

    private EntityManager _entityManager;
    private EntityQuery _hitQuery;
    private bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<EnemyHitVisualBridge>() == null)
        {
            var go = new GameObject("[EnemyHitVisualBridge]");
            go.AddComponent<EnemyHitVisualBridge>();
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
        // Query mặc định trong Entities 1.x CHỈ chọn các Entity có EnemyHitReactionData đang ENABLED
        _hitQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadWrite<EnemyHitReactionData>(),
            ComponentType.ReadOnly<EnemyTag>()
        );

        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;
        if (_hitQuery.IsEmpty) return;

        float dt = Time.deltaTime;

        // Lấy danh sách các entity đang kích hoạt trạng thái trúng đòn (thường chỉ 5 - 15 quái)
        using var entities = _hitQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity e = entities[i];
            if (!_entityManager.Exists(e)) continue;

            // Truy xuất SpriteRenderer từ Companion Component của Entity
            SpriteRenderer sr = null;
#pragma warning disable CS0618
            if (_entityManager.HasComponent<SpriteRenderer>(e))
            {
                sr = _entityManager.GetComponentObject<SpriteRenderer>(e);
            }
#pragma warning restore CS0618

            if (sr == null) continue;

            EnemyHitReactionData hit = _entityManager.GetComponentData<EnemyHitReactionData>(e);
            hit.Timer -= dt;

            if (hit.Timer > 0f)
            {
                float totalDuration = Mathf.Max(0.01f, hit.Duration);
                float progress = Mathf.Clamp01(hit.Timer / totalDuration); // 1.0 (vừa trúng đòn) -> 0.0 (kết thúc)

                // 1. Red Damage Flash:
                // Ở 25% đầu tiên (progress > 0.75f), chớp sáng trắng-đỏ tạo cảm giác va đập sắc nét, sau đó chuyển đỏ rực
                Color targetColor = (progress > 0.75f)
                    ? Color.Lerp(hitRedColor, hitWhiteFlashColor, (progress - 0.75f) / 0.25f)
                    : hitRedColor;
                sr.color = Color.Lerp((Color)hit.OriginalColor, targetColor, Mathf.Clamp01(progress * 1.7f));

                // 2. Damage Tilt (nghiêng người nảy đòn):
                float tiltAmount = Mathf.Sin(progress * Mathf.PI) * maxTiltAngle * hit.TiltDirection;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, tiltAmount);

                // 3. Squash & Stretch (đàn hồi va chạm):
                float squash = 1f + Mathf.Sin(progress * Mathf.PI) * punchScaleRatio;
                sr.transform.localScale = new Vector3(
                    hit.OriginalScale.x * squash,
                    hit.OriginalScale.y / squash,
                    hit.OriginalScale.z
                );

                _entityManager.SetComponentData(e, hit);
            }
            else
            {
                // Kết thúc thời gian hiệu ứng -> Hoàn trả nguyên vẹn trạng thái gốc
                sr.color = hit.OriginalColor;
                sr.transform.localRotation = Quaternion.identity;
                sr.transform.localScale = hit.OriginalScale;

                hit.Timer = 0f;
                _entityManager.SetComponentData(e, hit);

                // Tắt component -> Bitmask thành 0, frame kế tiếp Query sẽ tự động bỏ qua quái này!
                _entityManager.SetComponentEnabled<EnemyHitReactionData>(e, false);
            }
        }
    }
}

