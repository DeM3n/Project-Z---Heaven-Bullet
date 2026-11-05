using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tag đánh dấu các viên ngọc EXP được sinh ngẫu nhiên tự nhiên trên bản đồ (Ambient EXP Orbs),
/// giúp phân biệt với ngọc rơi ra khi quái bị tiêu diệt và kiểm soát giới hạn số lượng (Cap).
/// </summary>
public struct AmbientExpOrbTag : IComponentData {}

/// <summary>
/// Cấu hình và bộ đếm sinh ngọc EXP tự nhiên rải rác quanh người chơi trên bản đồ.
/// </summary>
public struct AmbientExpSpawnerData : IComponentData
{
    public Entity SmallOrbPrefab;
    public Entity MediumOrbPrefab;
    public float Interval;         // Chu kỳ sinh ngọc (giây)
    public float Timer;            // Bộ đếm ngược đến đợt sinh tiếp theo
    public int MaxAmbientOrbs;     // Giới hạn tối đa số ngọc tự nhiên trên sân cùng lúc
    public float MinRadius;        // Khoảng cách tối thiểu quanh Player (4.5m)
    public float MaxRadius;        // Khoảng cách tối đa quanh Player (11.5m)
    public Random Rng;
}

