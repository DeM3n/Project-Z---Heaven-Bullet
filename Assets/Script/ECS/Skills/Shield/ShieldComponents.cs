using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Đánh dấu entity chứa cấu hình và bộ đếm thời gian của kỹ năng Khiên Hộ Thể (Shield Skill)
/// </summary>
public struct ShieldSkillTag : IComponentData {}

/// <summary>
/// Dữ liệu Khiên Hộ Thể gắn trực tiếp trên thực thể Player để tra cứu nhanh trong mọi hệ thống nhận sát thương
/// </summary>
public struct PlayerShieldData : IComponentData
{
    public int SkillLevel;                   // Cấp độ kỹ năng (0 = chưa học, 1-4)
    public int CurrentCharges;               // Số lượng lớp khiên hiện có (0, 1, 2)
    public int MaxCharges;                   // Số lượng lớp khiên tối đa (1 ở Lv1-2, 2 ở Lv3-4)
    public int HitsRemainingInCurrentShield; // Số hit chặn còn lại của lớp khiên đang kích hoạt (1 ở Lv1-3, 2 ở Lv4)
    public int HitsPerShield;                // Số hit chịu được của mỗi lớp khiên (1 ở Lv1-3, 2 ở Lv4)
    public float RechargeTimer;              // Thời gian đã tích lũy để hồi 1 lớp khiên mới (giây)
    public float RechargeCooldown;           // Thời gian cần thiết để hồi 1 lớp khiên (30s ở Lv1, 18s ở Lv2, 16s ở Lv3, 14s ở Lv4)
    public bool HasJustBlocked;              // Cờ đánh dấu vừa chặn thành công 1 đòn (để Visual Bridge kích hoạt flash)
}

/// <summary>
/// Cấu hình thông số kỹ năng Khiên Hộ Thể trên Skill Entity
/// </summary>
public struct ShieldSkillData : IComponentData
{
    public float BaseCooldown;               // Cooldown Lv1: 30s
    public float Lv2Cooldown;                // Cooldown Lv2: 18s
    public float Lv3Cooldown;                // Cooldown Lv3: 16s
    public float Lv4Cooldown;                // Cooldown Lv4: 14s
    public float ExplosionRadius;            // Bán kính phát nổ khi vỡ khiên ở Lv4 (~4.5m)
    public float ExplosionDamage;            // Sát thương nổ AOE ở Lv4 (~100 DMG)
    public float KnockbackForce;             // Lực đẩy lùi quái ở Lv4 (~2.5m)
}

/// <summary>
/// Event thông báo một lớp khiên vừa bị phá vỡ (được lắng nghe bởi ShieldExplosionSystem và ShieldVisualBridge)
/// </summary>
public struct ShieldBreakEvent : IComponentData
{
    public float3 Position;
    public float Radius;
    public float Damage;
    public float KnockbackForce;
    public bool IsExplosion;                 // true = Level 4 (nổ AOE + đẩy lùi quái + diệt đạn), false = vỡ thường
}

/// <summary>
/// Tag đánh dấu event ShieldBreak đã được hệ thống chiến đấu (ShieldExplosionSystem) xử lý xong
/// </summary>
public struct ShieldExplosionProcessedTag : IComponentData {}
