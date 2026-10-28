using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Đánh dấu entity là Yêu Tinh Trộm Bảo (Treasure Goblin).
/// </summary>
public struct TreasureGoblinTag : IComponentData {}

/// <summary>
/// Chứa thông số hành vi bỏ chạy và đếm ngược tẩu thoát của Treasure Goblin.
/// </summary>
public struct TreasureGoblinData : IComponentData
{
    public float EscapeTimer;        // Thời gian còn lại trước khi đào hang tẩu thoát (e.g. 20 giây)
    public float FleeSpeed;          // Vận tốc chạy trốn khỏi người chơi (3.8 - 4.2 m/s)
    public float ZigzagTimer;        // Nhịp đổi hướng né tránh
    public float ZigzagAngle;        // Góc lạng lách né đạn
    public bool HasDetectedPlayer;   // true khi đã phát hiện Player hoặc bị tấn công
    public float DetectionRange;     // Cự ly phát hiện Player (mặc định 7.5m)
}

/// <summary>
/// Phần thưởng khi tiêu diệt được Treasure Goblin.
/// </summary>
public struct TreasureGoblinDropData : IComponentData
{
    public int ExpReward;
}
