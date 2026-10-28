using Unity.Entities;

/// <summary>
/// Lưu trữ các chỉ số Cổ Vật Bị Động (Relics) mà người chơi thu thập được từ Rương Báu.
/// Cung cấp các buff vĩnh viễn trong trận đấu.
/// </summary>
public struct PlayerRelicData : IComponentData
{
    public int BootsLevel;       // Thần Hành Hài (+15% Tốc độ di chuyển mỗi cấp)
    public int MagnetLevel;      // Tụ Linh Châu (+50% Bán kính nhặt ngọc mỗi cấp)
    public int VitalityLevel;    // Trái Tim Pha Lê (+30 Max HP và hồi đầy máu mỗi cấp)
    public int PowerLevel;       // Cổ Kiếm Bùa Chú (+15% Sát thương tổng thể mỗi cấp)
    public int FortuneDiceLevel; // Xúc Xắc Vận Mệnh (+2 Lượt Reroll mỗi cấp)
    public float DamageMultiplier; // Hệ số nhân sát thương tổng thể (bắt đầu từ 1.0f)
}
