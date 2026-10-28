using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Phân loại nguồn gốc gây sát thương lên mục tiêu
/// </summary>
public enum DamageSourceType : byte
{
    Contact = 0,     // Va chạm trực tiếp từ quái cận chiến
    Projectile = 1,  // Đạn bắn từ xa
    Explosion = 2,   // Vụ nổ AOE (quái tự sát Bomber, bom, nổ thùng)
    Hazard = 3       // Môi trường (vũng độc, bẫy gai, laser)
}

/// <summary>
/// Phần tử đệm lưu trữ danh sách các cú đánh (Hits) mà một Entity tiếp nhận trong cùng một frame.
/// Áp dụng InternalBufferCapacity = 8 để lưu trữ trực tiếp trong Chunk memory, tuyệt đối không cấp phát Heap (Zero GC Alloc).
/// </summary>
[InternalBufferCapacity(8)]
public struct IncomingDamage : IBufferElementData
{
    public float Amount;                  // Lượng sát thương của cú đánh
    public DamageSourceType SourceType;    // Loại sát thương
    public float3 SourcePosition;          // Tọa độ nguồn phát để tính hướng hất văng (Knockback) hoặc phản đòn
}
