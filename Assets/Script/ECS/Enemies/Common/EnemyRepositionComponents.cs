using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Cấu hình và trạng thái của Hệ thống Tái định vị quái vật (Enemy Repositioning).
/// Khi người chơi di chuyển nhanh bỏ xa các quái chậm chạp ra khỏi tầm camera,
/// hệ thống sẽ dựa trên KÍCH THƯỚC VÀ VỊ TRÍ THỰC TẾ CỦA CAMERA VIEWPORT (Bounding Box AABB)
/// để dời quái vật về phía ngoài màn hình (Forward-Biased Cone hoặc Flanks).
/// 
/// Quy chuẩn toán học:
/// - Camera Box = [CamPos.x +/- HalfWidth, CamPos.y +/- HalfHeight]
/// - Vùng cấm xuất hiện = Camera Box mở rộng thêm SafetyMargin (mặc định 2.0m)
/// - Vị trí sinh quái = Chiếu tia (Ray-Box Perimeter Intersection) từ tâm Camera ra ngoài rìa hình chữ nhật
///   + Thêm khoảng ngẫu nhiên (Scatter 0.5m - 2.5m)
///   -> ĐẢM BẢO 100% TUYỆT ĐỐI KHÔNG BAO GIỜ NẰM BÊN TRONG KHUNG NHÌN CAMERA TRÊN MỌI MỨC ZOOM VÀ TỈ LỆ MÀN HÌNH!
/// </summary>
public struct EnemyRepositionConfigData : IComponentData
{
    public bool Enabled;                    // Bật / Tắt hệ thống
    public float CheckInterval;             // Chu kỳ quét tối ưu hiệu năng (mặc định 0.2s = 5 lần/giây)
    public float Timer;                     // Bộ đếm chu kỳ quét
    public float SafetyMargin;              // Khoảng cách đệm an toàn tuyệt đối ngoài mép camera (m, mặc định 2.0m)
    public float OutOfRangeOffset;          // Khoảng cách ngoài tầm nhìn để kích hoạt reposition (m, mặc định 5.5m)
    public float ForwardConeHalfAngleRad;   // Nửa góc mở nón chặn đầu tính bằng radian (vd: 55 độ ~ 0.96 rad)
    public float ForwardBiasChance;         // Tỉ lệ ưu tiên đón đầu phía trước (vd: 0.75 = 75%)
    public Random Rng;                      // Bộ sinh số ngẫu nhiên Burst-compatible
}
