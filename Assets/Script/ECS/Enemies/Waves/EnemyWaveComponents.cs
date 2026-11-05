using Unity.Entities;

/// <summary>
/// Trạng thái của hệ thống Wave trận đấu:
/// - NormalTimeline: Đếm thời gian bình thường, sinh quái theo tiến trình từng phút.
/// - MegaWaveActive: Đợt tổng tấn công siêu lớn (ở phút 3, 6, 9) đang diễn ra. Thử thách người chơi sinh tồn trong vòng 1 phút (60 giây).
/// - MegaWaveCleared: Vừa sống sót qua 1 phút tổng tấn công, trao thưởng lớn và tiếp tục tiến trình trận đấu.
/// </summary>
public enum WaveState : byte
{
    NormalTimeline = 0,
    MegaWaveActive = 1,
    MegaWaveCleared = 2
}

/// <summary>
/// Dữ liệu điều phối Wave & Tiến trình thời gian trận đấu (Singleton).
/// Quản lý thử thách sinh tồn 1 phút tại các mốc phút 3, 6, 9 với các đợt quái dồn dập.
/// </summary>
public struct WaveManagerData : IComponentData
{
    public WaveState State;
    public float ElapsedGameTime;        // Thời gian sinh tồn trận đấu (giây)
    public bool IsTimerPaused;           // Cờ báo hiệu đồng hồ game bị tạm dừng
    public int ActiveMegaWaveMinute;     // Mốc phút Mega Wave hiện tại: 3, 6, hoặc 9
    public float MegaWaveDuration;       // Tổng thời gian thử thách Mega Wave (60s = 1 phút)
    public float MegaWaveTimer;          // Thời gian còn lại của đợt Mega Wave (đếm ngược từ 60s về 0s)
    public float MegaWaveSubSpawnTimer;  // Bộ đếm nhịp sinh các đợt quái dồn dập trong suốt 1 phút
    public byte MegaWaveSurgeStage;      // 0: chưa surge, 1: đã kích hoạt surge 20s, 2: đã kích hoạt surge 40s
    public int MegaWaveEnemiesKilled;    // Số quái Mega Wave người chơi đã tiêu diệt trong đợt
    public int MegaWaveRemainingEnemies; // Số quái vật Mega Wave hiện còn sống trên sân
    public int MegaWaveTotalEnemies;     // Tổng số quái vật được sinh ra trong đợt Mega Wave
    public byte Triggered3Min;           // 0: chưa kích hoạt, 1: đã kích hoạt
    public byte Triggered6Min;           // 0: chưa kích hoạt, 1: đã kích hoạt
    public byte Triggered9Min;           // 0: chưa kích hoạt, 1: đã kích hoạt
}

/// <summary>
/// Đánh dấu entity quái vật thuộc đợt tổng tấn công Mega Wave (Siege).
/// Khi bị tiêu diệt sẽ cộng vào MegaWaveEnemiesKilled.
/// </summary>
public struct MegaWaveEnemyTag : IComponentData {}

/// <summary>
/// Đánh dấu quái vật Tinh Anh / Chỉ Huy (Elite Boss) trong đợt Mega Wave.
/// Có máu dày vượt trội, scale to lớn và rơi phần thưởng giá trị cao.
/// </summary>
public struct MegaWaveEliteTag : IComponentData
{
    public float BaseScale;
}
