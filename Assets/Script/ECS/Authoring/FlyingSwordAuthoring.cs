using Unity.Entities;
using UnityEngine;

public class FlyingSwordAuthoring : MonoBehaviour
{
    class Baker : Baker<FlyingSwordAuthoring>
    {
        public override void Bake(FlyingSwordAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<FlyingSwordTag>(entity);
            AddComponent(entity, new FlyingSwordData
            {
                State = 0,
                CurrentAngle = 0f,
                Timer = 0f,
                SwordIndex = 0,
                TotalSwords = 1,
                UltimateTimer = 0f,
                IsUltimate = false,
                LastTarget = Entity.Null,
                LoopDirection = 1
            });
            AddBuffer<SwordHitTargetBuffer>(entity);
        }
    }
}
