using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hybrid Bridge: nhận input từ bàn phím (WASD) -> ghi vào singleton PlayerInputSingleton trong ECS.
/// Quản lý vòng đời chặt chẽ: tự động dọn dẹp entity khi scene unloads / reload (Retry)
/// tránh sinh ra nhiều hơn 1 PlayerInputSingleton gây lỗi HasSingleton trong ECS.
/// </summary>
public class PlayerInputBridge : MonoBehaviour
{
    private EntityManager _em;
    private Entity _inputEntity;

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        _em = world.EntityManager;

        // Đảm bảo chỉ tồn tại DUY NHẤT 1 singleton PlayerInputSingleton trong ECS World
        using (var q = _em.CreateEntityQuery(ComponentType.ReadOnly<PlayerInputSingleton>()))
        {
            int count = q.CalculateEntityCount();
            if (count > 0)
            {
                var existing = q.ToEntityArray(Allocator.Temp);
                _inputEntity = existing[0];
                for (int i = 1; i < existing.Length; i++)
                {
                    _em.DestroyEntity(existing[i]);
                }
                existing.Dispose();
            }
            else
            {
                _inputEntity = _em.CreateEntity(typeof(PlayerInputSingleton));
            }
        }
    }

    void OnDestroy()
    {
        if (_em.World != null && _em.World.IsCreated && _em.Exists(_inputEntity))
        {
            _em.DestroyEntity(_inputEntity);
        }
    }

    void Update()
    {
        if (_em.World == null || !_em.World.IsCreated || !_em.Exists(_inputEntity)) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        float2 move = float2.zero;
        if (kb.wKey.isPressed) move.y += 1;
        if (kb.sKey.isPressed) move.y -= 1;
        if (kb.dKey.isPressed) move.x += 1;
        if (kb.aKey.isPressed) move.x -= 1;

        _em.SetComponentData(_inputEntity, new PlayerInputSingleton
        {
            MoveInput = math.normalizesafe(move)
        });
    }
}
