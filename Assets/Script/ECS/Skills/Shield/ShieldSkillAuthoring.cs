using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(SkillAuthoring))]
public class ShieldSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Thời gian hồi chiêu từng cấp độ (Cooldowns)")]
    [Tooltip("Thời gian hồi 1 lớp khiên ở Cấp 1 (mặc định 27s)")]
    public float BaseCooldown = 27f;
    [Tooltip("Thời gian hồi 1 lớp khiên ở Cấp 2 (mặc định 16.2s)")]
    public float Lv2Cooldown = 16.2f;
    [Tooltip("Thời gian hồi 1 lớp khiên ở Cấp 3 (mặc định 14.4s)")]
    public float Lv3Cooldown = 14.4f;
    [Tooltip("Thời gian hồi 1 lớp khiên ở Cấp 4 (mặc định 12.6s)")]
    public float Lv4Cooldown = 12.6f;

    [Header("Chỉ số Vụ nổ Phản pháo Cấp 4 (Shatter Nova)")]
    [Tooltip("Bán kính nổ khi khiên vỡ ở Cấp 4")]
    public float ExplosionRadius = 4.5f;
    [Tooltip("Sát thương vụ nổ khi khiên vỡ ở Cấp 4")]
    public float ExplosionDamage = 100f;
    [Tooltip("Lực đẩy lùi quái ra xa")]
    public float KnockbackForce = 2.5f;

    public void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent<ShieldSkillTag>(entity);
        baker.AddComponent(entity, new ShieldSkillData
        {
            BaseCooldown = BaseCooldown,
            Lv2Cooldown = Lv2Cooldown,
            Lv3Cooldown = Lv3Cooldown,
            Lv4Cooldown = Lv4Cooldown,
            ExplosionRadius = ExplosionRadius,
            ExplosionDamage = ExplosionDamage,
            KnockbackForce = KnockbackForce
        });
    }
}
