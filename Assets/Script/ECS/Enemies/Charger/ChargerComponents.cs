using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Trạng thái hoạt động của Charger:
/// - Phase 0 (Approach): Đi chậm về phía Player đến khi vào HoldDistance, sau đó đứng yên.
/// - Phase 1 (Telegraph): Đứng yên, có dấu hiệu báo hiệu. Chốt DashDirection ở frame cuối.
/// - Phase 2 (Dash): Lao thẳng theo DashDirection với DashSpeed, không đổi hướng.
/// - Phase 3 (Recover): Đứng im hồi phục, hết RecoverTime quay về Approach.
/// </summary>
public struct ChargerState : IComponentData
{
    public byte Phase;            // 0: Approach, 1: Telegraph, 2: Dash, 3: Recover
    public byte PreviousPhase;    // Phase trước đó (dùng để log chuyển phase)
    public float PhaseTimer;       // Thời gian trôi qua trong phase hiện tại
    public float CooldownTimer;    // Đếm ngược DashCooldown
    public float2 DashDirection;   // Hướng lao cố định (không homing)
    public float DashTraveled;     // Quãng đường đã lao trong đợt dash hiện tại
    public bool HasHitThisDash;    // Đã gây sát thương Player trong cú dash này chưa (chỉ 1 lần)
}

/// <summary>
/// Cấu hình hành vi Charger (Bake từ Inspector qua ChargerAuthoring).
/// </summary>
public struct ChargerConfigData : IComponentData
{
    public float HoldDistance;          // Khoảng cách dừng giữ cự ly với Player (mặc định 9m)
    public float TriggerRange;          // Khoảng cách cho phép kích hoạt Telegraph (mặc định 11m)
    public float WindupTime;            // Thời gian báo hiệu trước khi lao (mặc định 0.6s)
    public float DashSpeed;             // Vận tốc lao (mặc định 5 m/s)
    public float DashDistance;          // Quãng đường lao tối đa (mặc định 13m)
    public float AimSpreadDeg;          // Độ lệch góc ngắm tối đa (+/- 10 độ)
    public float RecoverTime;           // Thời gian đứng im hồi phục sau dash (mặc định 1.2s)
    public float DashCooldown;          // Thời gian hồi chiêu giữa 2 lần dash (mặc định 2.5s)
    public float DashDamageMultiplier;  // Hệ số nhân sát thương va chạm khi dash (mặc định 1.5x)
}

/// <summary>
/// Random state riêng biệt cho từng Charger entity sử dụng Unity.Mathematics.Random.
/// </summary>
public struct ChargerRandomData : IComponentData
{
    public Unity.Mathematics.Random Value;
}

/// <summary>
/// Enableable component đánh dấu trạng thái Telegraph (Báo hiệu trước khi lao).
/// BẬT ở Phase 1 và TẮT ở các phase khác.
/// </summary>
public struct ChargerTelegraphVisual : IComponentData, IEnableableComponent {}

