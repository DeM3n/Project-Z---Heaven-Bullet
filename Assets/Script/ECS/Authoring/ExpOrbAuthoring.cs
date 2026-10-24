using Unity.Entities;
using UnityEngine;

public enum ExpOrbTier
{
    Small = 0,
    Medium = 1,
    Big = 2
}

public class ExpOrbAuthoring : MonoBehaviour
{
    [Header("EXP Orb Tier")]
    public ExpOrbTier Tier = ExpOrbTier.Medium;
    public int DefaultExpValue = 5;

    class Baker : Baker<ExpOrbAuthoring>
    {
        public override void Bake(ExpOrbAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ExpOrbTag>(entity);
            AddComponent(entity, new ExpOrbData { ExpValue = authoring.DefaultExpValue, FlySpeed = 0f });
        }
    }
}