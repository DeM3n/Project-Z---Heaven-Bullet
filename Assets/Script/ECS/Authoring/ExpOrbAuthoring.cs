using Unity.Entities;
using UnityEngine;

public class ExpOrbAuthoring : MonoBehaviour
{
    class Baker : Baker<ExpOrbAuthoring>
    {
        public override void Bake(ExpOrbAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ExpOrbTag>(entity);
            AddComponent<ExpOrbData>(entity); // giá trị thật do DeathCleanupSystem set lúc Instantiate
        }
    }
}