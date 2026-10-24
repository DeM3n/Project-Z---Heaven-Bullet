using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;


// chỉ đẩy kết quả vào 1 singleton entity cho ECS đọc.
public class PlayerInputBridge : MonoBehaviour
{
    private EntityManager _em;
    private Entity _inputEntity;

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        _inputEntity = _em.CreateEntity(typeof(PlayerInputSingleton));
    }

    void Update()
    {
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