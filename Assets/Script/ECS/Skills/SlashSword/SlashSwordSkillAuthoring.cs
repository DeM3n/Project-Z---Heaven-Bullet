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
    public float SlashRadius = 2.0f;
    public float SlashAngleDeg = 140f;
    public float DetectRange = 7.0f;

    [Header("Chỉ số Kiếm Khí (Level 4 Piercing Wave)")]
    public float WaveSpeed = 15f;
    public float WaveDuration = 1.6f;
    public float WaveDamage = 65f;
    public float WaveRadius = 0.8f;

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
