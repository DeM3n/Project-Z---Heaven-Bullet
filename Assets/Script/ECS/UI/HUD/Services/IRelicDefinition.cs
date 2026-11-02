using Unity.Entities;

/// <summary>
/// Hợp đồng Strategy cho mỗi loại Cổ Vật (Relic) rơi từ Rương Báu.
/// Cho phép thêm Cổ Vật mới mà không cần chỉnh sửa switch-case trong code UI.
/// </summary>
public interface IRelicDefinition
{
    /// <summary>
    /// Loại Cổ Vật (tương thích với enum RelicType trong ECS).
    /// </summary>
    RelicType Type { get; }

    /// <summary>
    /// ID chuỗi định danh duy nhất (vd: "WindrunnerBoots", "SpiritMagnet").
    /// </summary>
    string RelicId { get; }

    /// <summary>
    /// Trọng số xuất hiện khi mở Rương.
    /// </summary>
    float Weight { get; }

    /// <summary>
    /// Lấy dữ liệu DTO hiển thị thẻ Cổ Vật theo ngôn ngữ hiện tại.
    /// </summary>
    RelicRewardData GetRewardData(EntityManager em, Entity player);

    /// <summary>
    /// Áp dụng hiệu ứng Cổ Vật và tăng cấp vào dữ liệu người chơi trong ECS.
    /// </summary>
    void Apply(EntityManager em, Entity player);
}

