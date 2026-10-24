using Unity.Entities;
using UnityEngine;

public class SummonSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    public GameObject SummonPrefab;
    public float SummonLifetime = 10f;

    public void Bake(IBaker baker, Entity entity)
    {
        if (SummonPrefab == null)
        {
            Debug.LogError($"[SummonSkillAuthoring] Chưa gán SummonPrefab trên {name}", this);
            return;
        }

        baker.AddComponent<SummonSkillTag>(entity);
        baker.AddComponent(entity, new SummonSkillData
        {
            SummonPrefab = baker.GetEntity(SummonPrefab, TransformUsageFlags.Dynamic),
            SummonLifetime = SummonLifetime
        });
    }
}