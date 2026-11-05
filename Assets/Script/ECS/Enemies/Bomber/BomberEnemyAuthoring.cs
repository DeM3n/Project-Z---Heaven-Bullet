using Unity.Entities;
using UnityEngine;

public class BomberEnemyAuthoring : MonoBehaviour, IEnemyBehaviorAuthoring
{
    public float ExplodeRadius = 1.5f;
    public float ExplodeDamage = 25f;
    public float RushDistance = 4.2f;
    public float SpeedBoost = 1.35f;

    public void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent<BomberTag>(entity);
        baker.AddComponent(entity, new BomberExplodeData
        {
            ExplodeRadius = ExplodeRadius,
            ExplodeDamage = ExplodeDamage
        });

        // Gán AI Cuồng Nộ Cảm Tử: Tăng tốc 1.6x khi vào cự ly áp sát
        baker.AddComponent(entity, new BomberRushData
        {
            RushTriggerDistance = RushDistance,
            SpeedMultiplier = SpeedBoost,
            IsRushing = false
        });
    }
}
