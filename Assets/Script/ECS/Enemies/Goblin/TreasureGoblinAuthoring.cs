using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring cho hành vi Treasure Goblin.
/// Tuân thủ quy chuẩn Open/Closed Principle: Implement IEnemyBehaviorAuthoring
/// để EnemyAuthoring tự động bake mà không cần sửa code cũ.
/// </summary>
public class TreasureGoblinAuthoring : MonoBehaviour, IEnemyBehaviorAuthoring
{
    [Header("Chỉ số Treasure Goblin")]
    public float EscapeTimer = 22f;
    public float FleeSpeed = 3.8f;
    public float DetectionRange = 7.5f;
    public int ExpReward = 60;

    public void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent<TreasureGoblinTag>(entity);
        baker.AddComponent(entity, new TreasureGoblinData
        {
            EscapeTimer = EscapeTimer,
            FleeSpeed = FleeSpeed,
            ZigzagTimer = 0.3f,
            ZigzagAngle = 0f,
            HasDetectedPlayer = false,
            DetectionRange = DetectionRange
        });
        baker.AddComponent(entity, new TreasureGoblinDropData
        {
            ExpReward = ExpReward
        });
    }
}
