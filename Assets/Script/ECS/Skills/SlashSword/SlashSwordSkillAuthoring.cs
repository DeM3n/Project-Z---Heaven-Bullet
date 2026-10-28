using Unity.Entities;
using UnityEngine;

public class SlashSwordSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Prefabs")]
    [Tooltip("Prefab thanh kiếm chém")]
    public GameObject SwordPrefab;
    [Tooltip("Prefab kiếm khí phóng ra ở Cấp 4")]
    public GameObject SwordWavePrefab;

    [Header("Chỉ số cơ bản (Level 1)")]
    public float BaseDamage = 38f;
    public float ApproachSpeed = 13f;
    public float ReturnSpeed = 14f;
    public float SlashRadius = 3.2f;
    public float SlashAngleDeg = 155f;
    public float DetectRange = 8.5f;

    [Header("Chỉ số Kiếm Khí (Level 4 Piercing Wave)")]
    public float WaveSpeed = 15f;
    public float WaveDuration = 1.6f;
    public float WaveDamage = 65f;
    public float WaveRadius = 1.2f;

    public void Bake(IBaker baker, Entity entity)
    {
        Entity swordPrefabEntity = Entity.Null;
        if (SwordPrefab != null)
        {
            swordPrefabEntity = baker.GetEntity(SwordPrefab, TransformUsageFlags.Dynamic);
        }
        else
        {
            Debug.LogError($"[SlashSwordSkillAuthoring] Chưa gán SwordPrefab trên {name}", this);
        }

        Entity wavePrefabEntity = Entity.Null;
        if (SwordWavePrefab != null)
        {
            wavePrefabEntity = baker.GetEntity(SwordWavePrefab, TransformUsageFlags.Dynamic);
        }

        baker.AddComponent<SlashSwordSkillTag>(entity);
        baker.AddComponent(entity, new SlashSwordSkillData
        {
            SwordPrefab = swordPrefabEntity,
            SwordWavePrefab = wavePrefabEntity,
            BaseDamage = BaseDamage,
            ApproachSpeed = ApproachSpeed,
            ReturnSpeed = ReturnSpeed,
            SlashRadius = SlashRadius,
            SlashAngleDeg = SlashAngleDeg,
            DetectRange = DetectRange,
            WaveSpeed = WaveSpeed,
            WaveDuration = WaveDuration,
            WaveDamage = WaveDamage,
            WaveRadius = WaveRadius
        });
    }
}
