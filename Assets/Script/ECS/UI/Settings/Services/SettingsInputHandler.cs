using System;
using UnityEngine.InputSystem;

/// <summary>
/// Chuyên trách lắng nghe phím tắt điều khiển Pause và Developer Mode:
/// - Chặn phím [ESC] / [P] khi đang có bất kỳ Popup Modal nào của HUD (Level-Up, Chest, Game Over).
/// - Ngăn ngừa xung đột giao diện và bug unpause ngoài ý muốn.
/// </summary>
public class SettingsInputHandler
{
    public event Action OnTogglePause;
    public event Action OnToggleDevPanel;
    public event Action OnCloseDevPanel;

    public void Update(bool isDevPanelOpen)
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Phím [Escape] hoặc [P]
        if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
        {
            // Nếu Dev Panel đang mở -> Đóng Dev Panel trước
            if (isDevPanelOpen)
            {
                OnCloseDevPanel?.Invoke();
                return;
            }

            // Nếu đang có bất kỳ Modal nào của HUD mở -> Chặn ESC không cho mở Pause Menu đè lên
            if (PauseService.HasAnyHUDModal())
            {
                return;
            }

            OnTogglePause?.Invoke();
        }

        // Phím [F1] hoặc phím [`] (Backquote): Bật/Tắt Chế độ Developer
        if (kb.f1Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)
        {
            OnToggleDevPanel?.Invoke();
        }
    }
}
