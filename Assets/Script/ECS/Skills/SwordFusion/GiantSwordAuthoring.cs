using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GiantSwordAuthoring : MonoBehaviour
{
    public float DefaultFallSpeed = 42f;

    class Baker : Baker<GiantSwordAuthoring>
    {
        public override void Bake(GiantSwordAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<GiantSwordTag>(entity);
            AddComponent(entity, new GiantSwordData
            {
                TargetPosition = float3.zero,
                CurrentPosition = float3.zero,
                FallSpeed = authoring.DefaultFallSpeed,
                StateTimer = 0f,
                State = 0,
                SwordWavePrefab = Entity.Null,
                ImpactDamage = 220f,
                ImpactRadius = 4.5f,
                WaveDamage = 75f,
                WaveSpeed = 10f,
                WaveLifetime = 1.8f
            });
        }
    }
}
