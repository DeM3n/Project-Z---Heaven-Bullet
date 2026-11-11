using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Unmanaged component lưu trữ trạng thái nảy đòn, đổi màu đỏ và đàn hồi (Hit Reaction) của Quái vật:
/// - Kế thừa IEnableableComponent để tối ưu hiệu năng: Mặc định TẮT (Disabled).
/// - Unity ECS tự động bỏ qua toàn bộ quái nhàn rỗi ở tầng Bitmask Chunk (Zero CPU cost).
/// - Chỉ BẬT khi quái nhận sát thương hiệu dụng, và tự TẮT khi hoàn tất hiệu ứng.
/// - Lưu trữ OriginalColor và OriginalScale được nạp sẵn từ Prefab lúc Bake (Zero GC, Zero Lookup runtime).
/// </summary>
public struct EnemyHitReactionData : IComponentData, IEnableableComponent
{
    /// <summary>
    /// Thời gian hiệu ứng còn lại (giây), đếm ngược về 0.
    /// </summary>
    public float Timer;

    /// <summary>
    /// Tổng thời lượng hiệu ứng (mặc định 0.14s - nhịp nhanh, giòn giã).
    /// </summary>
    public float Duration;

    /// <summary>
    /// Chiều nghiêng nảy đòn (+1.0f hoặc -1.0f), đảo dấu liên tục mỗi khi trúng đòn mới.
    /// </summary>
    public float TiltDirection;

    /// <summary>
    /// Màu gốc của SpriteRenderer trên Prefab.
    /// </summary>
    public Color32 OriginalColor;

    /// <summary>
    /// Tỉ lệ Scale gốc của Prefab (thường là 1, 1, 1).
    /// </summary>
    public float3 OriginalScale;
}

