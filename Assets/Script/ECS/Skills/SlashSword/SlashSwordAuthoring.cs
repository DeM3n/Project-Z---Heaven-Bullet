using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SlashSwordAuthoring : MonoBehaviour
{
    class Baker : Baker<SlashSwordAuthoring>
    {
        public override void Bake(SlashSwordAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SlashSwordTag>(entity);
            AddComponent(entity, new SlashSwordData
            {
                SkillEntity = Entity.Null,
                State = 0,
                OrbitAngle = 0f,
                CooldownTimer = 0f,
                StateTimer = 0f,
                TargetPosition = float3.zero,
                TargetEnemy = Entity.Null,
                SlashDirection = new float3(1f, 0f, 0f),
                SwordIndex = 0,
                TotalSwords = 1,
                SlashProgress = 0f
            });
        }
    }
}
