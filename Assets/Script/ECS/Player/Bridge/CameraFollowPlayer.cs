using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


public class CameraFollowPlayer : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f); // z âm để camera 2D đứng trước player

    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private bool _initialized;

    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[CameraFollowPlayer] ECS World chưa sẵn sàng ở Start(), sẽ thử lại ở LateUpdate.");
            return;
        }

        _entityManager = world.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<LocalTransform>());
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized)
        {
            Start(); // thử init lại nếu World khởi tạo trễ hơn MonoBehaviour
            if (!_initialized) return;
        }

        if (_playerQuery.IsEmpty) return;

        LocalTransform playerTransform = _playerQuery.GetSingleton<LocalTransform>();
        Vector3 targetPosition = new Vector3(
            playerTransform.Position.x,
            playerTransform.Position.y,
            0f) + offset;

        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}