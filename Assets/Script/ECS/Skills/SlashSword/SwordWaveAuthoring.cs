using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SwordWaveAuthoring : MonoBehaviour
{
    class Baker : Baker<SwordWaveAuthoring>
    {
        public override void Bake(SwordWaveAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SwordWaveTag>(entity);
            AddComponent(entity, new SwordWaveData
            {
                Direction = new float2(1f, 0f),
                Speed = 15f,
                Damage = 100f,
                Radius = 0.8f,
                Lifetime = 1.6f,
                MaxLifetime = 1.6f
            });
            AddBuffer<SwordWaveHitTargetBuffer>(entity);
        }
    }
}
