using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring MonoBehaviour cho phép tạo Prefab vật phẩm rơi (Heart, Magnet, Treasure Chest) trong Unity Editor.
/// </summary>
public class PickupAuthoring : MonoBehaviour
{
    public PickupType Type = PickupType.HealthHeart;
    public float Value = 25f;
    public float Radius = 0.8f;

    class Baker : Baker<PickupAuthoring>
    {
        public override void Bake(PickupAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PickupItemTag>(entity);
            AddComponent(entity, new PickupData
            {
                Type = authoring.Type,
                Value = authoring.Value,
                Radius = authoring.Radius,
                FlySpeed = 0f
            });
        }
    }
}
