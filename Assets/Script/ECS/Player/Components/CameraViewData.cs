using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Dữ liệu khung nhìn Camera thời gian thực (World Position, HalfWidth, HalfHeight)
/// được CameraFollowPlayer (MonoBehaviour) đồng bộ vào ECS mỗi frame.
/// Giúp các hệ thống ECS như EnemyRepositionSystem, Frustum Culling, Off-screen Spawning
/// xác định CHÍNH XÁC 100% hình chữ nhật tầm nhìn thực tế trên mọi độ phân giải màn hình & mức zoom.
/// </summary>
public struct CameraViewData : IComponentData
{
    public float3 Position;         // Tọa độ tâm camera trong không gian thế giới
    public float HalfWidth;        // Nửa chiều rộng màn hình (World Units)
    public float HalfHeight;       // Nửa chiều cao màn hình (World Units = orthographicSize)
    public float OrthographicSize; // Kích thước orthographic hiện tại
    public float AspectRatio;      // Tỉ lệ khung hình thực tế (Width / Height)
}

