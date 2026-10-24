using Unity.Entities;
using UnityEngine;

public class ProjectileAuthoring : MonoBehaviour
{
    class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ProjectileTag>(entity);
            AddComponent<MovementData>(entity);          // giá trị thật do SkillCastSystem set lúc Instantiate
            AddComponent<ProjectileLifetimeData>(entity);
            AddComponent<DamageOnContactData>(entity);
        }
    }
}