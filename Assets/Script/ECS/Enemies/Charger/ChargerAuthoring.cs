using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component cho Charger Enemy.
/// Tự động được bake bởi EnemyAuthoring thông qua interface IEnemyBehaviorAuthoring.
/// </summary>
public class ChargerAuthoring : MonoBehaviour, IEnemyBehaviorAuthoring
{
    [Header("Cự Ly Tương Tác")]
    [Tooltip("Khoảng cách tiếp cận tối thiểu, khi đạt khoảng cách này quái sẽ dừng lại")]
    public float HoldDistance = 9f;

    [Tooltip("Khoảng cách tối đa với Player cho phép kích hoạt Telegraph để chuẩn bị lao")]
    public float TriggerRange = 11f;

    [Header("Cơ Chế Báo Hiệu (Telegraph)")]
    [Tooltip("Thời gian đứng yên báo hiệu trước khi phóng")]
    public float WindupTime = 0.6f;

    [Tooltip("Độ lệch góc tối đa (+/- độ) khi khóa hướng nhắm vào Player")]
    public float AimSpreadDeg = 10f;

    [Header("Cơ Chế Lao (Dash)")]
    [Tooltip("Vận tốc di chuyển khi lao thẳng (m/s)")]
    public float DashSpeed = 5f;

    [Tooltip("Quãng đường lao tối đa")]
    public float DashDistance = 13f;

    [Tooltip("Hệ số nhân sát thương va chạm khi đang lao (ContactDamage * DashDamageMultiplier)")]
    public float DashDamageMultiplier = 1.5f;

    [Header("Hồi Phục & Hồi Chiêu")]
    [Tooltip("Thời gian đứng yên hồi phục sau khi kết thúc cú lao")]
    public float RecoverTime = 1.2f;

    [Tooltip("Thời gian hồi chiêu giữa 2 lần lao")]
    public float DashCooldown = 2.5f;

    public void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent<ChargerTag>(entity);

        baker.AddComponent(entity, new ChargerConfigData
        {
            HoldDistance = math.max(0.5f, HoldDistance),
            TriggerRange = math.max(HoldDistance, TriggerRange),
            WindupTime = math.max(0.1f, WindupTime),
            DashSpeed = math.max(0.5f, DashSpeed),
            DashDistance = math.max(1f, DashDistance),
            AimSpreadDeg = math.clamp(AimSpreadDeg, 0f, 60f),
            RecoverTime = math.max(0.1f, RecoverTime),
            DashCooldown = math.max(0.5f, DashCooldown),
            DashDamageMultiplier = math.max(1f, DashDamageMultiplier)
        });

        baker.AddComponent(entity, new ChargerState
        {
            Phase = 0,
            PreviousPhase = 255,
            PhaseTimer = 0f,
            CooldownTimer = 0f,
            DashDirection = float2.zero,
            DashTraveled = 0f,
            HasHitThisDash = false
        });

        uint seed = (uint)(entity.Index + 1) * 2654435761u;
        if (seed == 0) seed = 12345u;

        baker.AddComponent(entity, new ChargerRandomData
        {
            Value = new Unity.Mathematics.Random(seed)
        });
    }
}

