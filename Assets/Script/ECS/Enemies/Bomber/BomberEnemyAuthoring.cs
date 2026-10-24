using Unity.Entities;
using UnityEngine;

public class BomberEnemyAuthoring : MonoBehaviour, IEnemyBehaviorAuthoring
{
    public float ExplodeRadius = 1.5f;
    public float ExplodeDamage = 25f;

    public void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent<BomberTag>(entity);
        baker.AddComponent(entity, new BomberExplodeData
        {
            ExplodeRadius = ExplodeRadius,
            ExplodeDamage = ExplodeDamage
        });
    }
}