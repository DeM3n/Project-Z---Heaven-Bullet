using Unity.Entities;

/// <summary>
/// Thống kê chung của người chơi trong màn đấu (Run Stats).
/// Chỉ lưu trữ các dữ liệu chung như số quái đã tiêu diệt.
/// Hoàn toàn độc lập, không gắn cứng bất kỳ kỹ năng cụ thể nào.
/// </summary>
public struct PlayerRunStats : IComponentData
{
    public int EnemiesKilled;
}

