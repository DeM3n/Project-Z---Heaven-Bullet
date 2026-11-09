using Unity.Entities;

/// <summary>
/// Danh mục ID số đại diện cho các kỹ năng gây sát thương trong Pipeline (Hot Path).
/// TUYỆT ĐỐI KHÔNG TẠO MAPPING TABLE Ở NƠI NÀO KHÁC.
/// 
/// QUY TẮC KHI THÊM KỸ NĂNG MỚI:
/// 1. Khai báo hằng số ushort mới tại đây (SkillIds).
/// 2. Đăng ký thông tin hiển thị (TitleGetter song ngữ, MaxLevel, ThemeColor) trong SkillDefinitionRegistry
///    (Assets/Script/ECS/UI/Common/SkillDefinitionRegistry.cs).
/// </summary>
public static class SkillIds
{
    public const ushort Unattributed = 0;       // Gói sát thương ngoại vi chưa phân loại
    public const ushort FlyingSword = 1;        // Phi Kiếm (gồm lao xuyên và xung kích AoE Lv4)
    public const ushort SlashSword = 2;         // Trảm Kiếm (gồm chém cận chiến và Kiếm Khí xuyên thấu Lv4)
    public const ushort ProjectileSkill = 3;    // Arcane Volley (Pháp Bảo Đạn Đạo)
    public const ushort Fireball = 4;           // Cầu Lửa (gồm nổ chính, nổ chùm và vệt lửa DoT Lv4)
    public const ushort Shield = 5;             // Khiên Hộ Thể (vụ nổ phản pháo khi vỡ ở Lv4)
    public const ushort Boomerang = 6;          // Phi Tiêu Boomerang (lượt bay đi và lượt bay về)
    public const ushort SwordFusion = 7;        // Hợp Thể Kiếm (Kiếm Khổng Lồ cắm đất chấn động)
    public const ushort Electric = 8;           // Lôi Điện / Sét (gồm tia sét cha và các tia sét lan)
    public const ushort ThunderBoomerang = 9;   // Hợp Thể Lôi Tiêu (Boomerang + Electric)
}

/// <summary>
/// Phần tử đệm lưu các gói sát thương gửi đến quái vật trong cùng một frame.
/// Áp dụng InternalBufferCapacity = 4 để lưu trực tiếp trong chunk memory, Zero GC Alloc.
/// </summary>
[InternalBufferCapacity(4)]
public struct EnemyIncomingDamage : IBufferElementData
{
    public ushort SkillId;
    public float Amount;
    public Entity Source; // Entity người gây sát thương (Player hoặc Clone), mặc định Entity.Null
}

/// <summary>
/// Bản ghi thống kê sát thương tích lũy theo từng kỹ năng trong suốt ván đấu (Run).
/// Nằm trên Singleton entity mang RunStatsTag.
/// </summary>
public struct SkillDamageRecord : IBufferElementData
{
    public ushort SkillId;
    public double TotalDamage; // Dùng double để không bị tràn/ngừng tăng khi vượt 16.7 triệu
    public int HitCount;
}

/// <summary>
/// Tag đánh dấu Singleton Entity quản lý số liệu thống kê trận đấu (Run Statistics).
/// </summary>
public struct RunStatsTag : IComponentData
{
}

