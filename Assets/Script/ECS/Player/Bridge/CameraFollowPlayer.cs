using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]
public class CameraFollowPlayer : MonoBehaviour
{
    [Header("Camera Following")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -15f); // z = -15f để lùi camera ra xa

    [Header("Camera View & Zoom (16:9 Aspect Ratio)")]
    [Tooltip("Độ rộng tầm nhìn Orthographic chuẩn 16:9 (7.2 giúp nhìn bao quát rộng rãi, thấy rõ quái và giữ nhân vật chi tiết)")]
    [Range(5f, 15f)]
    [SerializeField] private float orthographicSize = 7.2f;

    [Tooltip("Tự động bảo toàn chuẩn tầm nhìn 16:9 trên mọi kích thước màn hình mà không làm lệch UI Canvas")]
    [SerializeField] private bool maintain16By9View = true;

    private Camera _camera;
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private bool _initialized;
    private bool _hasSnappedToPlayer;

    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;

    public static CameraFollowPlayer Instance { get; private set; }

    public float OrthographicSize
    {
        get => orthographicSize;
        set
        {
            orthographicSize = Mathf.Clamp(value, 4f, 16f);
            UpdateCameraView();
        }
    }

    private void Awake()
    {
        Instance = this;
        _camera = GetComponent<Camera>();

        // 1. Dọn dẹp bất kỳ Camera trùng lặp nào trong bộ nhớ
        var allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (var c in allCameras)
        {
            if (c != null && c != _camera && c.gameObject.name == "Main Camera")
            {
                Debug.LogWarning($"[CameraFollowPlayer] Phát hiện Camera trùng lặp '{c.gameObject.name}' (Scene: '{c.gameObject.scene.name}'), tự động dọn dẹp!");
                Destroy(c.gameObject);
            }
        }

        // 2. Dọn dẹp AudioListener trùng lặp
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
        if (listeners != null && listeners.Length > 1)
        {
            var myListener = GetComponent<AudioListener>();
            foreach (var l in listeners)
            {
                if (l != null && l != myListener)
                {
                    Destroy(l);
                }
            }
        }

        UpdateCameraView();
    }

    private void Start()
    {
        UpdateCameraView();
        TryInitQuery();
    }

    private void Update()
    {
        if (maintain16By9View && (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight))
        {
            UpdateCameraView();
        }
    }

    private void OnValidate()
    {
        UpdateCameraView();
    }

    /// <summary>
    /// Điều chỉnh kích thước hiển thị của Camera để luôn nhìn rộng và chuẩn 16:9.
    /// Giữ camera.rect full (0, 0, 1, 1) để UI và hiệu ứng không bị co rút.
    /// </summary>
    public void UpdateCameraView()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null) return;

        _camera.orthographic = true;
        _camera.rect = new Rect(0f, 0f, 1f, 1f);

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        if (!maintain16By9View || Screen.width <= 0 || Screen.height <= 0)
        {
            _camera.orthographicSize = orthographicSize;
            return;
        }

        float targetAspect = 16f / 9f;
        float currentAspect = (float)Screen.width / Screen.height;

        // Nếu màn hình hẹp hơn 16:9 (ví dụ 16:10, 4:3 hoặc màn dọc), tự scale orthographicSize
        // để chiều ngang màn hình luôn bao quát được tối thiểu 30.2m (chuẩn 16:9 ở size 8.5)
        if (currentAspect < targetAspect)
        {
            _camera.orthographicSize = orthographicSize * (targetAspect / currentAspect);
        }
        else
        {
            _camera.orthographicSize = orthographicSize;
        }
    }

    private bool TryInitQuery()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        var em = world.EntityManager;
        if (!_initialized || _entityManager != em)
        {
            _entityManager = em;
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _initialized = true;
        }
        return true;
    }

    private void LateUpdate()
    {
        if (!TryInitQuery()) return;
        if (_playerQuery.IsEmpty) return;

        LocalTransform playerTransform = _playerQuery.GetSingleton<LocalTransform>();
        Vector3 targetPosition = new Vector3(
            playerTransform.Position.x,
            playerTransform.Position.y,
            0f) + offset;

        // Khung hình đầu tiên snap ngay lập tức vào tâm Player, các khung hình sau Lerp mượt mà
        if (!_hasSnappedToPlayer)
        {
            transform.position = targetPosition;
            _hasSnappedToPlayer = true;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        }
    }
}
