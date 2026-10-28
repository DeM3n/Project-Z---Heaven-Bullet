using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Hybrid Bridge đồng bộ hiệu ứng thị giác khi Người chơi nhận sát thương theo phong cách Minecraft (Damage Tint & Tilt):
/// - Khi nhận đòn, sprite nhân vật lập tức chuyển sang màu Đỏ (Red Damage Flash).
/// - Đi kèm hiệu ứng nghiêng nảy (Damage Tilt / Flinch wobble) và đàn hồi nhẹ (Squash & Stretch).
/// - Sau khi kết thúc thời gian miễn sát thương (0.2s), sprite tự động hoàn trả màu sắc và hình dạng ban đầu.
/// - Hoàn toàn loại bỏ khiên chắn / hào quang để giữ đúng phong cách mộc mạc, giòn giã của game action.
/// </summary>
public class PlayerVisualBridge : MonoBehaviour
{
    [Header("Minecraft-Style Hit Effects")]
    [Tooltip("Màu đỏ đặc trưng khi bị đánh trúng (tương tự mob Minecraft bị đòn)")]
    [SerializeField] private Color hitRedColor = new Color(1f, 0.22f, 0.22f, 1f);

    [Tooltip("Góc nghiêng tối đa khi bị nảy người trúng đòn (độ)")]
    [SerializeField] private float maxTiltAngle = 12f;

    [Tooltip("Độ đàn hồi co giãn (Squash & Stretch) khi trúng đòn")]
    [SerializeField] private float punchScaleRatio = 0.14f;

    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private bool _initialized;

    // SpriteRenderer của Player (Companion Component trong Unity ECS)
    private SpriteRenderer _playerRenderer;
    private Color _originalColor = Color.white;
    private Vector3 _originalScale = Vector3.one;
    private bool _hasOriginalProps = false;

    private float _lastTimer = 0f;
    private float _tiltDirection = 1f;
    private bool _wasInHitState = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<PlayerVisualBridge>() == null)
        {
            var go = new GameObject("[PlayerVisualBridge]");
            go.AddComponent<PlayerVisualBridge>();
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
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<PlayerInvulnerabilityData>()
        );
        _initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_initialized && !InitECS()) return;
        if (_playerQuery.IsEmpty) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        if (!_entityManager.Exists(playerEntity)) return;

        // Truy xuất SpriteRenderer từ Companion Component của Player
        if (_playerRenderer == null)
        {
#pragma warning disable CS0618
            if (_entityManager.HasComponent<SpriteRenderer>(playerEntity))
            {
                _playerRenderer = _entityManager.GetComponentObject<SpriteRenderer>(playerEntity);
            }
#pragma warning restore CS0618

            // Fallback nếu không qua managed ECS component
            if (_playerRenderer == null)
            {
                var playerGo = GameObject.Find("Player(Clone)") ?? GameObject.Find("Player");
                if (playerGo != null)
                {
                    _playerRenderer = playerGo.GetComponent<SpriteRenderer>();
                }
            }

            if (_playerRenderer != null)
            {
                _originalColor = _playerRenderer.color;
                _originalScale = _playerRenderer.transform.localScale;
                _hasOriginalProps = true;
            }
        }

        if (_playerRenderer == null) return;

        PlayerInvulnerabilityData invuln = _entityManager.GetComponentData<PlayerInvulnerabilityData>(playerEntity);

        // Phát hiện khoảnh khắc bắt đầu nhận đòn (CurrentTimer tăng vọt từ <= 0 lên > 0)
        if (_lastTimer <= 0f && invuln.CurrentTimer > 0f)
        {
            // Đổi hướng nghiêng luân phiên trái/phải cho mỗi lần trúng đòn
            _tiltDirection = -_tiltDirection;
        }
        _lastTimer = invuln.CurrentTimer;

        if (invuln.CurrentTimer > 0f)
        {
            _wasInHitState = true;

            // Tiến độ thời gian bất tử (1.0 khi mới nhận đòn -> 0.0 khi kết thúc 0.2s)
            float totalDuration = Mathf.Max(0.01f, invuln.Duration);
            float progress = Mathf.Clamp01(invuln.CurrentTimer / totalDuration);

            // 1. Minecraft Red Damage Flash: Chuyển toàn bộ sprite thành sắc đỏ rực
            // Giữ đỏ đậm ở đầu rồi dịu dần về màu gốc ở những mili-giây cuối
            _playerRenderer.color = Color.Lerp(_originalColor, hitRedColor, Mathf.Clamp01(progress * 1.8f));

            // 2. Damage Tilt (nghiêng người như Minecraft damage tilt)
            float tiltAmount = Mathf.Sin(progress * Mathf.PI) * maxTiltAngle * _tiltDirection;
            _playerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, tiltAmount);

            // 3. Squash & Stretch (đàn hồi nhẹ thân hình tạo cảm giác lực va đập giòn giã)
            float squash = 1f + Mathf.Sin(progress * Mathf.PI) * punchScaleRatio;
            _playerRenderer.transform.localScale = new Vector3(
                _originalScale.x * squash,
                _originalScale.y / squash,
                _originalScale.z
            );
        }
        else if (_wasInHitState)
        {
            // Kết thúc thời gian miễn sát thương -> khôi phục lại trạng thái bình thường
            ResetToNormal();
            _wasInHitState = false;
        }
    }

    private void ResetToNormal()
    {
        if (_playerRenderer != null && _hasOriginalProps)
        {
            _playerRenderer.color = _originalColor;
            _playerRenderer.transform.localRotation = Quaternion.identity;
            _playerRenderer.transform.localScale = _originalScale;
        }
    }

    private void OnDisable()
    {
        ResetToNormal();
    }

    private void OnDestroy()
    {
        ResetToNormal();
    }
}
