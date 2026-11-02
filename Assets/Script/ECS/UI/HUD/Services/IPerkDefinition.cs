using Unity.Entities;

/// <summary>
/// Hợp đồng Strategy cho mỗi loại nâng cấp (Perk) khi lên cấp.
/// Cho phép thêm kỹ năng mới hoặc chỉ số mới mà không cần chỉnh sửa code UI hay Bridge (OCP).
/// </summary>
public interface IPerkDefinition
{
    /// <summary>
    /// ID định danh duy nhất của Perk (vd: "FlyingSword", "SwordFusion", "MaxHp").
    /// </summary>
    string PerkId { get; }

    /// <summary>
    /// Độ ưu tiên hiển thị (Priority > 0 được ưu tiên xếp đầu danh sách, vd SwordFusion = 100).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Trọng số xuất hiện trong vòng quay ngẫu nhiên (Weighted Random).
    /// </summary>
    float Weight { get; }

    /// <summary>
    /// Kiểm tra điều kiện xuất hiện (chưa max level, đủ điều kiện tiến hóa).
    /// </summary>
    bool IsAvailable(EntityManager em, Entity player);

    /// <summary>
    /// Lấy dữ liệu DTO hiển thị thẻ bài theo ngôn ngữ hiện tại.
    /// </summary>
    PerkOptionData GetOptionData(EntityManager em, Entity player);

    /// <summary>
    /// Áp dụng hiệu ứng nâng cấp trực tiếp vào dữ liệu ECS.
    /// </summary>
    void Apply(EntityManager em, Entity player);
}

