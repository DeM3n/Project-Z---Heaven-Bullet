using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Đánh dấu entity là Phân Thân Ảo Ảnh (Mirror Clone) của Người chơi.
/// Phân thân không thể nhận sát thương, không thể nhặt ngọc EXP hoặc vật phẩm rơi.
/// </summary>
public struct PlayerCloneTag : IComponentData {}

/// <summary>
/// Dữ liệu điều khiển đội hình và trạng thái của Phân Thân Ảo Ảnh.
/// </summary>
public struct PlayerCloneData : IComponentData
{
    public int CloneIndex;         // 0: Phân thân Trái, 1: Phân thân Phải
    public float3 BaseOffset;      // Độ lệch tương đối so với Player (-5.2f hoặc +5.2f)
    public float FacingDirection;  // 1f: Hướng Phải, -1f: Hướng Trái
    public float FollowSpeed;      // Tốc độ bám theo chuyển động (mặc định 18f)
}

/// <summary>
/// Tag đánh dấu thực thể kỹ năng thuộc sở hữu của Phân Thân.
/// Giúp phân định ranh giới giữa Kỹ năng gốc của Player và Kỹ năng phân thân trong UI và Queries.
/// </summary>
public struct CloneSkillTag : IComponentData {}

/// <summary>
/// Liên kết thực thể kỹ năng của Phân Thân với thực thể kỹ năng gốc của Player để tự động đồng bộ cấp độ.
/// </summary>
public struct ClonedSkillLink : IComponentData
{
    public Entity PlayerSkillEntity; // Entity kỹ năng gốc của Player
    public Entity CloneOwner;        // Entity Phân thân sở hữu
}

