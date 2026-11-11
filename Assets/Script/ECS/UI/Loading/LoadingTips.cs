using UnityEngine;

/// <summary>
/// Kho tàng bí kíp sinh tồn & mẹo chiến thuật hiển thị ngẫu nhiên trên Màn hình Tải cảnh (Loading Screen).
/// Tự động đồng bộ song ngữ Việt - Anh theo GameLocalization.
/// </summary>
public static class LoadingTips
{
    private static readonly (string vi, string en)[] Tips = new[]
    {
        ("💡 Mẹo: Dung Hợp Phi Kiếm và Trảm Kiếm để khai mở Tru Tiên Kiếm Trận (Celestial Blade) càn quét vạn giới!",
         "💡 Tip: Fuse Flying Sword and Slash Sword to unleash the Celestial Blade and obliterate enemies!"),

        ("💡 Mẹo: Cổ Vật Kính Ảo Ảnh (Mirror of Illusion) tạo ra 2 phân thân kế thừa 100% tất cả pháp bảo của bạn.",
         "💡 Tip: Mirror of Illusion spawns 2 clones that inherit 100% of all your active skills and relics."),

        ("💡 Mẹo: Quái Bomber sẽ phát nổ khi lại gần. Hãy chú ý giữ cự ly né tránh vùng sóng xung kích.",
         "💡 Tip: Bomber enemies detonate on contact. Keep your distance to avoid their deadly shockwave."),

        ("💡 Mẹo: Thu thập Ngọc Kinh Nghiệm liên tục để kích hoạt hiệu ứng Thăng Hoa Cao Độ (Pitch Escalation).",
         "💡 Tip: Continuously collect EXP Orbs to trigger Pitch Escalation audio and level up faster."),

        ("💡 Mẹo: Đợt tổng tấn công Mega Wave diễn ra ở các mốc Phút 3, 6, 9. Sống sót 60 giây để nhận Rương Cổ Vật!",
         "💡 Tip: Survive 60 seconds during Mega Waves at Minute 3, 6, 9 to claim an Ancient Chest reward!"),

        ("💡 Mẹo: Nâng cấp Trảm Kiếm lên Cấp 4 để phóng ra Kiếm Khí lưỡi liềm bay xuyên thấu quái vật.",
         "💡 Tip: Upgrade Slash Sword to Level 4 to unleash crescent sword waves that pierce through crowds."),

        ("💡 Mẹo: Bấm [F1] hoặc [~] bất kỳ lúc nào để mở Bảng Điều Khiển Lập Trình Viên (Dev Panel).",
         "💡 Tip: Press [F1] or [~] anytime during battle to open the Developer Control Panel."),

        ("💡 Mẹo: Linh Thạch tích lũy được dùng để nâng cấp Cây Thiên Phú vĩnh viễn trong Màn hình chính.",
         "💡 Tip: Spirit Stones earned in battle can be spent in the Main Menu to upgrade permanent Talents."),

        ("💡 Mẹo: Nhặt Nam Châm Hút EXP để lập tức thu hút toàn bộ ngọc kinh nghiệm trên khắp bản đồ.",
         "💡 Tip: Collect the EXP Magnet pickup to instantly draw in all experience orbs across the realm."),

        ("💡 Mẹo: Kỹ năng Tiên Nữ Tán Hoa (Petal Burst) ở Cấp 4 sẽ tạo ra Vòng Xoáy Hút Chân Không gom quái vật vào tâm nổ.",
         "💡 Tip: Petal Burst at Level 4 creates an Implosion Vortex that pulls nearby enemies into the detonation core."),

        ("💡 Mẹo: Kiếm Thể Hộ Thân (Divine Shield) sẽ tạo màng chắn hấp thụ toàn bộ sát thương của đòn đánh tiếp theo.",
         "💡 Tip: Divine Shield forms an energy barrier that absorbs the next incoming blow completely."),

        ("💡 Mẹo: Rương Cổ Vật sẽ xuất hiện khi bạn tiêu diệt Trùm hoặc vượt qua thử thách Mega Wave.",
         "💡 Tip: Ancient Chests spawn upon defeating Bosses or surviving brutal Mega Wave trials.")
    };

    public static string GetRandomTip()
    {
        bool isVi = GameLocalization.CurrentLanguage == GameLanguage.Vietnamese;
        int idx = Random.Range(0, Tips.Length);
        return isVi ? Tips[idx].vi : Tips[idx].en;
    }
}

