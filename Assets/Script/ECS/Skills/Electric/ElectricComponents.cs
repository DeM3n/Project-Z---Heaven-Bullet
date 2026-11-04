using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tag nhận diện thực thể kỹ năng Lôi Điện (Electric / Chain Lightning).
/// </summary>
public struct ElectricSkillTag : IComponentData { }

/// <summary>
/// Dữ liệu vận hành thời gian thực của kỹ năng Electric.
/// </summary>
public struct ElectricSkillData : IComponentData
{
    public float BaseDamage;         // Sát thương của tia sét cha
    public float DetectRange;        // Tầm tìm quái của tia cha quanh Player (~9.5m)
    public float ChainRange;         // Tầm tìm quái kế tiếp để truyền tia lan (~6.0m)
    public float ChainDamageRatio;   // Tỉ lệ sát thương tia lan (mặc định 0.30f = 30%)
    public int PrimaryBoltCount;     // Số lượng tia sét cha bắn cùng lúc (Lv1-2: 1 tia, Lv3-4: 2 tia)
    public int ChainCount;           // Số lần lan sang mục tiêu kế tiếp (Lv1: 1, Lv2-3: 2, Lv4: 4)
    public bool CanBounceBack;       // Cấp 4: Cho phép lan ngược về quái cũ đã từng dính sét
    public float StunDuration;       // Thời gian gây choáng (Cấp 4: 0.5s, Cấp 1-3: 0s)
}

/// <summary>
/// Bảng chỉ số cân bằng 4 cấp độ nạp từ ScriptableObject (Single Source of Truth) cho Burst Job.
/// </summary>
public struct ElectricLevelBalanceData : IComponentData
{
    // Cooldown từng cấp (s): Lv1: 6.0s, Lv2: 5.4s, Lv3: 4.86s, Lv4: 4.86s
    public float CooldownLv1;
    public float CooldownLv2;
    public float CooldownLv3;
    public float CooldownLv4;

    // Sát thương tia cha: Lv1: 60, Lv2: 90, Lv3: 90, Lv4: 120
    public float DamageLv1;
    public float DamageLv2;
    public float DamageLv3;
    public float DamageLv4;

    // Số tia sét cha: Lv1: 1, Lv2: 1, Lv3: 2, Lv4: 2
    public int PrimaryCountLv1;
    public int PrimaryCountLv2;
    public int PrimaryCountLv3;
    public int PrimaryCountLv4;

    // Số lần lan truyền: Lv1: 1, Lv2: 2, Lv3: 2, Lv4: 4
    public int ChainCountLv1;
    public int ChainCountLv2;
    public int ChainCountLv3;
    public int ChainCountLv4;
}

/// <summary>
/// Component quản lý trạng thái Choáng (Tê liệt dòng điện) trên quái vật.
/// Implement IEnableableComponent để bật/tắt tức thì bằng bitmask, ZERO Structural Changes, ZERO GC Alloc!
/// </summary>
public struct EnemyStunData : IComponentData, IEnableableComponent
{
    public float StunTimer; // Thời gian choáng còn lại (s)
}

/// <summary>
/// Sự kiện phóng 1 luồng hồ quang điện từ điểm A đến điểm B (gồm tia cha hoặc tia lan).
/// Sinh ra từ Burst System và được ElectricVisualBridge tiêu thụ trong LateUpdate.
/// </summary>
public struct ElectricArcEvent : IComponentData
{
    public float3 StartPos;     // Tọa độ phát tia điện
    public float3 EndPos;       // Tọa độ mục tiêu tiếp nhận
    public float Damage;        // Sát thương gây ra tại mục tiêu
    public bool IsPrimary;      // true: Tia sét cha từ Player, false: Tia sét lan giữa các quái
    public bool InflictsStun;   // Cấp 4: Đánh dấu gây hiệu ứng Stun 0.5s
    public int HopIndex;        // Bước lan truyền (0 = cha, 1..4 = lan)
    public Entity TargetEntity; // Quái vật tiếp nhận đòn sét
}

