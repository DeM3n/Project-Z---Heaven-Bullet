using System.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Bộ điều phối Màn hình Tải cảnh (Loading Controller).
/// Quản lý nạp ngầm bất đồng bộ (Async Loading), đồng bộ Pure ECS SubScene,
/// và bảo đảm gameplay sẵn sàng 100% trước khi mở màn trận đấu.
/// </summary>
public class LoadingController : MonoBehaviour
{
    public const string LoadingSceneName = "LoadingScene";
    public const string DefaultGameplaySceneName = "SampleScene";

    public static string TargetSceneName { get; private set; } = DefaultGameplaySceneName;

    private static LoadingController _instance;
    private LoadingView _view;
    private bool _isLoading;

    /// <summary>
    /// Phương thức công khai toàn cục để kích hoạt quá trình nạp cảnh mượt mà qua Loading Scene.
    /// </summary>
    public static void LoadScene(string targetSceneName = DefaultGameplaySceneName)
    {
        TargetSceneName = string.IsNullOrEmpty(targetSceneName) ? DefaultGameplaySceneName : targetSceneName;
        SceneManager.LoadScene(LoadingSceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (SceneManager.GetActiveScene().name == LoadingSceneName)
        {
            var controller = EnsureControllerInstance();
            controller.BeginLoading();
        }

        SceneManager.sceneLoaded += OnGlobalSceneLoaded;
    }

    private static void OnGlobalSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == LoadingSceneName)
        {
            var controller = EnsureControllerInstance();
            controller.BeginLoading();
        }
    }

    private static LoadingController EnsureControllerInstance()
    {
        if (_instance == null)
        {
            _instance = FindAnyObjectByType<LoadingController>();
        }

        if (_instance == null)
        {
            var go = new GameObject("[LoadingController]");
            _instance = go.AddComponent<LoadingController>();
            DontDestroyOnLoad(go);
        }

        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == LoadingSceneName && !_isLoading)
        {
            BeginLoading();
        }
    }

    public void BeginLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        StopAllCoroutines();
        StartCoroutine(LoadProcessRoutine());
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(eventSystemObj);
        }
    }

    private IEnumerator LoadProcessRoutine()
    {
        EnsureEventSystem();

        // 1. Khởi tạo Loading View UI (Canvas DontDestroyOnLoad)
        if (_view == null)
        {
            var viewObj = new GameObject("[LoadingView]");
            DontDestroyOnLoad(viewObj);
            _view = viewObj.AddComponent<LoadingView>();
            _view.Init();
        }
        else
        {
            _view.RefreshTip();
        }

        // 2. Hiệu ứng Fade In mềm mại
        float fadeInDuration = 0.25f;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _view.SetAlpha(Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }
        _view.SetAlpha(1f);

        float startTime = Time.unscaledTime;
        float minDisplayDuration = 0.85f; // Thời gian hiển thị tối thiểu để người chơi đọc tip và tránh chớp giật hình ảnh

        // 3. Bắt đầu nạp Target Scene bất đồng bộ (Async)
        Debug.Log($"[LoadingController] Bắt đầu nạp bất đồng bộ cảnh: '{TargetSceneName}'...");
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(TargetSceneName);
        asyncOp.allowSceneActivation = false;

        // Chu kỳ nạp bất đồng bộ: asyncOp.progress chạy từ 0.0 đến 0.9 khi allowSceneActivation = false
        while (!asyncOp.isDone)
        {
            float normalizedProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
            float visualProgress = normalizedProgress * 0.85f;
            _view.SetProgress(visualProgress, GetStatusText(visualProgress));

            // Khi tài nguyên scene đã nạp đầy đủ trong bộ nhớ và đạt thời gian hiển thị tối thiểu
            if (asyncOp.progress >= 0.89f && (Time.unscaledTime - startTime >= minDisplayDuration))
            {
                Debug.Log($"[LoadingController] Scene '{TargetSceneName}' đã nạp xong (progress={asyncOp.progress:F2}), kích hoạt scene...");
                _view.SetProgress(0.9f, GetStatusText(0.9f));
                asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }

        Debug.Log($"[LoadingController] Cảnh '{TargetSceneName}' đã kích hoạt thành công!");

        // 4. Scene Gameplay đã được kích hoạt và trở thành Active Scene!
        // Người chơi vẫn nhìn thấy Loading Screen phủ kín màn hình nhờ DontDestroyOnLoad.
        // Bây giờ tiến hành đồng bộ và kiểm tra trạng thái Pure ECS SubScene.
        _view.SetProgress(0.94f, GetStatusText(0.94f));

        yield return StartCoroutine(WaitForECSReadyRoutine());

        // 5. Toàn bộ gameplay đã 100% sẵn sàng!
        _view.SetProgress(1.0f, GetStatusText(1.0f));
        yield return new WaitForSecondsRealtime(0.2f);

        // 6. Fade Out Loading Screen để mở màn trận chiến mượt mà
        float fadeOutDuration = 0.35f;
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _view.SetAlpha(1f - Mathf.Clamp01(elapsed / fadeOutDuration));
            yield return null;
        }

        // 7. Giải phóng tài nguyên Loading
        if (_view != null)
        {
            Destroy(_view.gameObject);
            _view = null;
        }

        _instance = null;
        _isLoading = false;
        Destroy(gameObject);
    }

    /// <summary>
    /// Chờ đợi cho đến khi SubSceneECS được stream hoàn chỉnh, PlayerTag xuất hiện,
    /// và CameraFollowPlayer đã snap vị trí chính xác vào tâm nhân vật.
    /// </summary>
    private IEnumerator WaitForECSReadyRoutine()
    {
        if (TargetSceneName != DefaultGameplaySceneName)
        {
            yield break;
        }

#if UNITY_EDITOR
        // Đảm bảo SubScene trong Editor được kích hoạt nạp nếu chưa load
        var subScene = FindAnyObjectByType<Unity.Scenes.SubScene>();
        if (subScene != null && !subScene.IsLoaded)
        {
            var w = World.DefaultGameObjectInjectionWorld;
            if (w != null && w.IsCreated)
            {
                Unity.Scenes.SceneSystem.LoadSceneAsync(w.Unmanaged, subScene.SceneGUID);
            }
        }
#endif

        float timeout = 5.0f;
        float elapsed = 0f;
        bool playerReady = false;

        Debug.Log("[LoadingController] Đang chờ ECS World & SubScene sẵn sàng...");
        while (elapsed < timeout && !playerReady)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>()))
                {
                    if (query.CalculateEntityCount() > 0)
                    {
                        playerReady = true;
                    }
                }
            }

            if (!playerReady)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        Debug.Log($"[LoadingController] ECS PlayerTag sẵn sàng: {playerReady} (sau {elapsed:F2}s). Snap camera & HUD...");
        // Đợi thêm 2 frame để:
        // Frame 1: CameraFollowPlayer.LateUpdate() snap ngay lập tức vào tâm nhân vật
        // Frame 2: GameHUDBridge đọc snapshot đầu tiên và thiết lập thanh HP/EXP
        yield return null;
        yield return null;
        Debug.Log("[LoadingController] Hoàn tất quá trình tải gameplay!");
    }

    private string GetStatusText(float progress)
    {
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;

        if (progress < 0.35f)
            return isVi ? "Đang mở cổng giới vực..." : "Opening realm rift...";
        if (progress < 0.75f)
            return isVi ? "Đang rèn đúc pháp bảo & kiếm khí..." : "Forging spiritual weapons...";
        if (progress < 0.90f)
            return isVi ? "Đang triệu hồi thực thể vạn vật..." : "Streaming entity realm...";
        if (progress < 0.99f)
            return isVi ? "Đang đồng bộ phân thân & kiếm trận..." : "Synchronizing world state...";
        return isVi ? "Sẵn sàng xuất trận!" : "Ready for battle!";
    }
}
