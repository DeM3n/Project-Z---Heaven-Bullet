using System;

/// <summary>
/// Các trạng thái Popup Modal toàn màn hình có thể kích hoạt trong trận đấu.
/// </summary>
public enum HUDModalState
{
    None,
    LevelUp,
    ChestReward,
    GameOver
}

/// <summary>
/// Máy trạng thái (State Machine) điều phối việc hiển thị các Modal Popup của HUD,
/// đảm bảo thứ tự ưu tiên và ủy quyền kiểm soát tạm dừng trận đấu cho PauseService (Token/RefCount).
/// </summary>
public class HUDModalStateMachine
{
    public HUDModalState CurrentState { get; private set; } = HUDModalState.None;

    public event Action<HUDModalState> OnStateChanged;

    /// <summary>
    /// Chuyển đổi trạng thái Modal và cập nhật Token tạm dừng trong PauseService.
    /// </summary>
    public void TransitionTo(HUDModalState newState)
    {
        if (CurrentState == newState) return;

        // Trả token của trạng thái cũ (nếu có)
        var oldReason = GetReasonForState(CurrentState);
        if (oldReason.HasValue)
        {
            PauseService.Release(oldReason.Value);
        }

        CurrentState = newState;

        // Thêm token của trạng thái mới (nếu có)
        var newReason = GetReasonForState(CurrentState);
        if (newReason.HasValue)
        {
            PauseService.Acquire(newReason.Value);
        }

        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Đóng modal hiện tại và quay về trạng thái bình thường (trả token).
    /// </summary>
    public void CloseModal()
    {
        TransitionTo(HUDModalState.None);
    }

    private static PauseReason? GetReasonForState(HUDModalState state)
    {
        return state switch
        {
            HUDModalState.LevelUp => PauseReason.LevelUpModal,
            HUDModalState.ChestReward => PauseReason.ChestModal,
            HUDModalState.GameOver => PauseReason.GameOverModal,
            _ => null
        };
    }
}
