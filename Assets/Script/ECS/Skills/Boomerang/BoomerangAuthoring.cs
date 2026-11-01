using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring script gắn trên Boomerang.prefab (Assets/Prefabs/Skills/Boomerang.prefab).
/// Khi Unity SubScene bake prefab này, Baker sẽ nạp sẵn các Component và Buffer cần thiết
/// vào Entity tương ứng (BoomerangTag, BoomerangProjectileData, BoomerangHitBuffer).
/// </summary>
public class BoomerangAuthoring : MonoBehaviour
{
    class Baker : Baker<BoomerangAuthoring>
    {
        public override void Bake(BoomerangAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BoomerangTag>(entity);
            AddComponent(entity, new BoomerangProjectileData
            {
                State = BoomerangFlightState.Outward,
                Direction = new float2(1f, 0f),
                Speed = 12.5f,
                Damage = 35f,
                HitRadius = 0.55f,
                DistanceTraveled = 0f,
                MaxDistance = 7.5f,
                ReturnDistanceTraveled = 0f,
                CatchRadius = 1.4f,
                IsEmpowered = false,
                SkillLevel = 1,
                Owner = Entity.Null,
                SkillEntity = Entity.Null,
                RotationAngle = 0f
            });
            AddBuffer<BoomerangHitBuffer>(entity);
        }
    }
}

