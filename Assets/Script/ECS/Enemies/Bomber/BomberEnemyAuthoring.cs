using Unity.Entities;
using UnityEngine;

public class BomberEnemyAuthoring : MonoBehaviour, IEnemyBehaviorAuthoring
{
    public float ExplodeRadius = 2.0f;
    public float ExplodeDamage = 25f;
    public float RushDistance = 4.2f;
    public float SpeedBoost = 1.35f;
    [Tooltip("Thời gian trễ ngòi nổ trước khi phát nổ (giúp người chơi có cửa sổ né đòn: 0.2s - 0.3s)")]
    public float FuseDelay = 0.25f;

    public void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent<BomberTag>(entity);
        baker.AddComponent(entity, new BomberExplodeData
        {
            ExplodeRadius = ExplodeRadius,
            ExplodeDamage = ExplodeDamage,
            FuseDelay = FuseDelay > 0f ? FuseDelay : 0.25f,
            FuseTimer = 0f,
            IsPrimed = false
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
