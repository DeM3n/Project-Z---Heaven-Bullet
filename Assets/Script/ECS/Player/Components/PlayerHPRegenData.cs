using Unity.Entities;

/// <summary>
/// Component quản lý tốc độ tự hồi phục HP mỗi giây (HP Regen) cho nhân vật chính
/// </summary>
public struct PlayerHPRegenData : IComponentData
{
    public float RegenPerSecond;
    public float Timer;
}

