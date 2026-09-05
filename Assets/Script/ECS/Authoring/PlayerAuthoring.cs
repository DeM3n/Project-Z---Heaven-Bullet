using Unity.Entities;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public int StartingExpToNextLevel = 10;
    public float PickupRange = 2f;
    public float MaxHP = 100f;

    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent(entity, new MovementData { MoveSpeed = authoring.MoveSpeed });
            AddComponent(entity, new PlayerLevelData { Level = 1, CurrentExp = 0, ExpToNextLevel = authoring.StartingExpToNextLevel });
            AddComponent(entity, new PickupRangeData {Range = authoring.PickupRange});
            AddComponent(entity, new HealthData { CurrentHP = authoring.MaxHP, MaxHP = authoring.MaxHP });
        }
    }
}