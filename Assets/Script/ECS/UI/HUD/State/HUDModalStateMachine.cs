using System;
using UnityEngine;

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
/// Máy trạng thái (State Machine) điều phối việc hiển thị các Modal Popup,
/// đảm bảo thứ tự ưu tiên, tự động kiểm soát Time.timeScale (Pause/Resume),
/// và ngăn chặn xung đột sự kiện (vd vừa nhặt rương vừa lên cấp).
/// </summary>
public class HUDModalStateMachine
{
    public HUDModalState CurrentState { get; private set; } = HUDModalState.None;

    public event Action<HUDModalState> OnStateChanged;

    /// <summary>
    /// Chuyển đổi trạng thái Modal và tự động đồng bộ Time.timeScale.
    /// </summary>
    public void TransitionTo(HUDModalState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;

        // Nếu có Modal đang mở, tạm dừng game. Khi đóng toàn bộ modal, trả về tốc độ bình thường.
        Time.timeScale = (CurrentState == HUDModalState.None) ? 1f : 0f;

        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Đóng modal hiện tại và quay về trạng thái bình thường.
    /// </summary>
    public void CloseModal()
    {
        TransitionTo(HUDModalState.None);
    }
}

