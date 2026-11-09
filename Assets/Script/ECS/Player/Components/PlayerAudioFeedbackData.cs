using Unity.Entities;

/// <summary>
/// Unmanaged component lưu các trigger âm thanh phản hồi của Player trong frame hiện tại
/// để Hybrid AudioBridge đọc và kích hoạt AudioManager một cách an toàn mà không làm mất tính Burst.
/// </summary>
public struct PlayerAudioFeedbackData : IComponentData
{
    public int ExpOrbsAbsorbed;
    public bool TookDamage;
    public bool TriggerRevival;
}

