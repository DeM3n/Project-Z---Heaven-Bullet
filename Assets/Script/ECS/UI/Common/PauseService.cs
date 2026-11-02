using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Các lý do tạm dừng game (Pause Reasons). Mỗi lý do hoạt động như một Token độc lập.
/// </summary>
public enum PauseReason
{
    LevelUpModal,
    ChestModal,
    GameOverModal,
    PauseMenu,
    DevPanel
}

/// <summary>
/// Dịch vụ quản lý tạm dừng trận đấu (Pause Service) tập trung cho toàn game:
/// - Sử dụng cơ chế Token / RefCount (Acquire / Release).
/// - Duy nhất khi không còn bất kỳ Token nào thì Time.timeScale mới trở về 1f.
/// - Ngăn ngừa triệt để bug xung đột Time.timeScale giữa các Popup HUD và Menu Settings.
/// </summary>
public static class PauseService
{
    private static readonly HashSet<PauseReason> _activeTokens = new HashSet<PauseReason>();

    public static bool IsPaused => _activeTokens.Count > 0;
    public static event Action<bool> OnPauseStateChanged;

    /// <summary>
    /// Kích hoạt tạm dừng với lý do cụ thể (Thêm token).
    /// </summary>
    public static void Acquire(PauseReason reason)
    {
        if (_activeTokens.Add(reason))
        {
            ApplyTimeScale();
        }
    }

    /// <summary>
    /// Hủy bỏ tạm dừng cho lý do cụ thể (Trả token).
    /// </summary>
    public static void Release(PauseReason reason)
    {
        if (_activeTokens.Remove(reason))
        {
            ApplyTimeScale();
        }
    }

    /// <summary>
    /// Xóa toàn bộ các token tạm dừng (ví dụ khi Restart trận đấu hoặc chuyển sang MainMenu).
    /// </summary>
    public static void ClearAll()
    {
        _activeTokens.Clear();
        ApplyTimeScale();
    }

    /// <summary>
    /// Kiểm tra xem một lý do cụ thể có đang giữ token tạm dừng hay không.
    /// </summary>
    public static bool HasToken(PauseReason reason) => _activeTokens.Contains(reason);

    /// <summary>
    /// Kiểm tra xem có bất kỳ modal nào của HUD (LevelUp, Chest, GameOver) đang giữ token hay không.
    /// Dùng để chặn các phím ESC/P mở menu Pause đè lên HUD.
    /// </summary>
    public static bool HasAnyHUDModal()
    {
        return _activeTokens.Contains(PauseReason.LevelUpModal) ||
               _activeTokens.Contains(PauseReason.ChestModal) ||
               _activeTokens.Contains(PauseReason.GameOverModal);
    }

    private static void ApplyTimeScale()
    {
        Time.timeScale = (_activeTokens.Count > 0) ? 0f : 1f;
        OnPauseStateChanged?.Invoke(IsPaused);
    }
}

