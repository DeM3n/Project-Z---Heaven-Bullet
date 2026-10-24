using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(SkillAuthoring))]
public class SwordFusionSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Prefabs")]
    [Tooltip("Prefab thanh kiếm khổng lồ từ trên trời cắm xuống")]
    public GameObject GiantSwordPrefab;
    [Tooltip("Prefab kiếm khí 5 tia tỏa ra khi cắm đất")]
    public GameObject SwordWavePrefab;

    [Header("Chỉ số đòn Kiếm Khổng Lồ")]
    public float ImpactDamage = 220f;
    public float ImpactRadius = 4.5f;
    public float DetectRange = 7.5f;

    [Header("Chỉ số Kiếm Khí (5 Waves)")]
    public float WaveDamage = 75f;
    public float WaveSpeed = 10f;
    public float WaveLifetime = 1.8f;

    public void Bake(IBaker baker, Entity entity)
    {
        Entity giantSwordEntity = Entity.Null;
        if (GiantSwordPrefab != null)
        {
            giantSwordEntity = baker.GetEntity(GiantSwordPrefab, TransformUsageFlags.Dynamic);
        }
        else
        {
            Debug.LogError($"[SwordFusionSkillAuthoring] Chưa gán GiantSwordPrefab trên {name}", this);
        }

        Entity wavePrefabEntity = Entity.Null;
        if (SwordWavePrefab != null)
        {
            wavePrefabEntity = baker.GetEntity(SwordWavePrefab, TransformUsageFlags.Dynamic);
        }
        else
        {
            Debug.LogWarning($"[SwordFusionSkillAuthoring] Chưa gán SwordWavePrefab trên {name}", this);
        }

        baker.AddComponent<SwordFusionTag>(entity);
        baker.AddComponent(entity, new SwordFusionSkillData
        {
            GiantSwordPrefab = giantSwordEntity,
            SwordWavePrefab = wavePrefabEntity,
            Cooldown = 5.5f,
            Timer = 0f,
            ImpactDamage = ImpactDamage,
            ImpactRadius = ImpactRadius,
            WaveDamage = WaveDamage,
            WaveSpeed = WaveSpeed,
            WaveLifetime = WaveLifetime,
            DetectRange = DetectRange
        });
    }
}
