using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Đánh dấu entity chứa cấu hình và bộ đếm thời gian của kỹ năng Boomerang (Phi Tiêu Hồi Toàn)
/// </summary>
public struct BoomerangSkillTag : IComponentData {}

/// <summary>
/// Đánh dấu thực thể đạn Boomerang đang bay trên chiến trường
/// </summary>
public struct BoomerangTag : IComponentData {}

/// <summary>
/// Trạng thái bay của Boomerang
/// </summary>
public enum BoomerangFlightState : byte
{
    Outward = 0,    // Bay tới, xuyên qua quái vật
    Returning = 1   // Quay về theo hướng di chuyển của người chơi (không homing)
}

/// <summary>
/// Cấu hình dữ liệu kỹ năng Boomerang gắn trên Skill Entity
/// </summary>
public struct BoomerangSkillData : IComponentData
{
    public Entity BoomerangPrefab;         // Prefab (nếu có, Entity.Null nếu procedural)
    public float BaseDamage;               // Sát thương cơ bản (35 ở Lv1)
    public float FlySpeed;                 // Tốc độ bay (~12.5m/s)
    public float MaxDistance;              // Cự ly bay tối đa trước khi quay đầu (~7.5m)
    public float HitRadius;                // Bán kính va chạm chém quái (0.55m ở Lv1, 0.85m ở Lv2+)
    public float CatchRadius;              // Bán kính để Player bắt dính Boomerang (~1.4m)
    public float DetectRange;              // Tầm quét tìm mục tiêu quái (~9.5m)
    public float EmpoweredMultiplier;      // Hệ số nhân sát thương khi bắt dính ở Cấp 4 (x1.6 = +60% DMG)
    public int BoomerangCount;             // Số lượng phi tiêu (1 ở Lv1-2, 2 ở Lv3-4)
}

/// <summary>
/// Dữ liệu vận hành gắn trên từng chiếc Boomerang đang bay
/// </summary>
public struct BoomerangProjectileData : IComponentData
{
    public BoomerangFlightState State;     // Trạng thái bay (Outward / Returning)
    public float2 Direction;               // Hướng bay hiện tại (cố định trong mỗi pha, KHÔNG auto-homing)
    public float Speed;                    // Vận tốc bay
    public float Damage;                   // Sát thương gây ra khi chạm quái
    public float HitRadius;                // Bán kính quét va chạm quái
    public float DistanceTraveled;         // Quãng đường đã bay trong pha Outward
    public float MaxDistance;              // Cự ly bay tối đa của pha Outward
    public float ReturnDistanceTraveled;   // Quãng đường đã bay trong pha Returning
    public float CatchRadius;              // Bán kính để Player hứng dính
    public bool IsEmpowered;               // true nếu là đòn bắn phản xạ Cường Hóa (Cấp 4)
    public int SkillLevel;                 // Cấp độ kỹ năng khi bắn
    public Entity Owner;                   // Thực thể Player sở hữu
    public Entity SkillEntity;             // Thực thể Boomerang Skill
    public float RotationAngle;            // Góc quay hiển thị (được cập nhật liên tục)
}

/// <summary>
/// Buffer lưu danh sách quái đã trúng đòn trong pha bay hiện tại (tránh gây sát thương lặp liên tục nhiều frame)
/// Khi Boomerang chuyển sang pha Returning, buffer này được xóa để có thể chém trúng quái lần thứ 2 trên đường về.
/// </summary>
[InternalBufferCapacity(16)]
public struct BoomerangHitBuffer : IBufferElementData
{
    public Entity Target;
}

/// <summary>
/// Event thông báo người chơi đã chụp dính Boomerang thành công
/// Được Visual Bridge lắng nghe để sinh hiệu ứng sóng năng lượng & lóe sáng quanh người chơi.
/// </summary>
public struct BoomerangCatchEvent : IComponentData
{
    public float3 Position;
    public bool IsEmpowered;
}

/// <summary>
/// Dữ liệu Cân Bằng theo cấp độ được Bake trực tiếp từ ScriptableObject (BoomerangSkillConfigSO).
/// Cung cấp dữ liệu thuần unmanaged cho Burst Compiler tính toán thời gian hồi chiêu,
/// sát thương, bán kính và số lượng đạn.
/// </summary>
public struct BoomerangLevelBalanceData : IComponentData
{
    public float CooldownLv1;
    public float CooldownLv2;
    public float CooldownLv3;
    public float CooldownLv4;

    public float DamageLv1;
    public float DamageLv2;
    public float DamageLv3;
    public float DamageLv4;

    public float HitRadiusLv1;
    public float HitRadiusLv2;
    public float HitRadiusLv3;
    public float HitRadiusLv4;

    public int CountLv1;
    public int CountLv2;
    public int CountLv3;
    public int CountLv4;
}

