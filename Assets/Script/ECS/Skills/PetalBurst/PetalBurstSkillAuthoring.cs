using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component cho Kỹ năng Tiên Nữ Tán Hoa (Fairy Petal Burst).
/// Implement ISkillEffectAuthoring để SkillAuthoring tự động bake theo Open/Closed Principle.
/// </summary>
public class PetalBurstSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Cấu hình mặc định (Nếu không có ScriptableObject)")]
    public float InitialFlySpeed = 8.5f;
    public float DragDeceleration = 7.0f;
    public float HitRadius = 0.35f;

    public void Bake(IBaker baker, Entity entity)
    {
        var cfg = PetalBurstSkillConfigSO.Instance;

        baker.AddComponent<PetalBurstSkillTag>(entity);

        float dmg1 = cfg != null ? cfg.GetDamage(1) : 22f;
        float spd = cfg != null ? cfg.FlySpeed : InitialFlySpeed;
        float minSpdMul = cfg != null ? cfg.MinSpeedMultiplier : 0.65f;
        float maxSpdMul = cfg != null ? cfg.MaxSpeedMultiplier : 1.35f;
        float drag = cfg != null ? cfg.DragDeceleration : DragDeceleration;
        float angleJitter = cfg != null ? cfg.AngleSpreadJitter : 0.65f;
        float hitRad = cfg != null ? cfg.HitRadius : HitRadius;
        float aoeRad1 = cfg != null ? cfg.GetExplosionRadius(1) : 0.85f;
        float dur1 = cfg != null ? cfg.GetDuration(1) : 0.85f;
        int count1 = cfg != null ? cfg.GetCount(1) : 6;
        float vortexRad = cfg != null ? cfg.VortexPullRadius : 2.4f;
        float vortexForce = cfg != null ? cfg.VortexPullForce : 8.5f;

        baker.AddComponent(entity, new PetalBurstSkillData
        {
            BaseDamage = dmg1,
            FlySpeed = spd,
            MinSpeedMultiplier = minSpdMul,
            MaxSpeedMultiplier = maxSpdMul,
            DragDeceleration = drag,
            AngleSpreadJitter = angleJitter,
            HitRadius = hitRad,
            ExplosionRadius = aoeRad1,
            Duration = dur1,
            PetalCount = count1,
            VortexPullRadius = vortexRad,
            VortexPullForce = vortexForce
        });

        baker.AddComponent(entity, new PetalBurstLevelBalanceData
        {
            DamageLv1 = cfg != null ? cfg.GetDamage(1) : 22f,
            DamageLv2 = cfg != null ? cfg.GetDamage(2) : 32f,
            DamageLv3 = cfg != null ? cfg.GetDamage(3) : 48f,
            DamageLv4 = cfg != null ? cfg.GetDamage(4) : 70f,

            CooldownLv1 = cfg != null ? cfg.GetCooldown(1) : 3.5f,
            CooldownLv2 = cfg != null ? cfg.GetCooldown(2) : 3.0f,
            CooldownLv3 = cfg != null ? cfg.GetCooldown(3) : 2.6f,
            CooldownLv4 = cfg != null ? cfg.GetCooldown(4) : 2.2f,

            CountLv1 = cfg != null ? cfg.GetCount(1) : 6,
            CountLv2 = cfg != null ? cfg.GetCount(2) : 9,
            CountLv3 = cfg != null ? cfg.GetCount(3) : 13,
            CountLv4 = cfg != null ? cfg.GetCount(4) : 18,

            DurationLv1 = cfg != null ? cfg.GetDuration(1) : 0.85f,
            DurationLv2 = cfg != null ? cfg.GetDuration(2) : 0.95f,
            DurationLv3 = cfg != null ? cfg.GetDuration(3) : 1.10f,
            DurationLv4 = cfg != null ? cfg.GetDuration(4) : 1.25f,

            ExplosionRadiusLv1 = cfg != null ? cfg.GetExplosionRadius(1) : 0.85f,
            ExplosionRadiusLv2 = cfg != null ? cfg.GetExplosionRadius(2) : 0.95f,
            ExplosionRadiusLv3 = cfg != null ? cfg.GetExplosionRadius(3) : 1.10f,
            ExplosionRadiusLv4 = cfg != null ? cfg.GetExplosionRadius(4) : 1.35f
        });
    }
}

