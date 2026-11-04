using Unity.Entities;

/// <summary>
/// Tag đánh dấu thực thể Kỹ năng Hợp Thể: Lôi Tiêu Thiên Kiếp (Thunder Boomerang = Boomerang + Electric).
/// </summary>
public struct ThunderBoomerangFusionTag : IComponentData { }

/// <summary>
/// Cấu hình dữ liệu của Kỹ năng Hợp Thể Thunder Boomerang trên Skill Entity.
/// </summary>
public struct ThunderBoomerangSkillData : IComponentData
{
    public float BoltDamage;          // Sát thương của mỗi tia sét phóng từ Boomerang (~90 DMG)
    public float DetectRange;         // Tầm quét quái quanh Boomerang (~8.5m)
    public float ChainRange;          // Bán kính lan sang quái kế tiếp (~6.0m)
    public int ChainCount;            // Số lần lan truyền của mỗi tia sét (2 lần)
    public int BoltsPerDischarge;     // Số tia sét phóng ra mỗi đợt (2 tia)
    public int MaxDischarges;         // Số lần phóng tối đa của 1 Boomerang (2 lần)
    public float InitialDelay;        // Thời gian bay trước khi bắn đợt 1 (0.5s)
    public float DischargeInterval;   // Khoảng cách thời gian giữa 2 đợt phóng (1.0s)
    public float StunDuration;        // Thời gian làm choáng quái khi dính sét (0.5s)
}

/// <summary>
/// Dữ liệu vận hành gắn trên từng chiếc Boomerang đang bay khi Kỹ năng Fusion đang hoạt động.
/// Quản lý chu kỳ phóng sét 2 đợt (đợt 1 sau 0.5s, đợt 2 cách 1.0s).
/// </summary>
public struct BoomerangLightningData : IComponentData
{
    public int DischargeCount;        // Số lần đã phóng sét (0 -> 1 -> 2)
    public int MaxDischarges;         // Giới hạn số lần phóng (mặc định 2)
    public float Timer;               // Đếm ngược đến đợt phóng tiếp theo (bắt đầu 0.5s, sau đó 1.0s)
    public int BoltsPerDischarge;     // Số tia sét mỗi lần phóng (mặc định 2)
    public float BoltDamage;          // Sát thương tia sét gốc
    public float DetectRange;         // Tầm quét quái quanh vị trí Boomerang
    public float ChainRange;          // Tầm tìm quái kế tiếp để phóng tia lan
    public int ChainCount;            // Số lần lan truyền
    public float StunDuration;        // Thời gian gây choáng
}

