using Unity.Entities;

public struct PlayerLevelData : IComponentData
{
    public int Level;
    public int CurrentExp;
    public int ExpToNextLevel;
}

public struct PickupRangeData : IComponentData
{
    public float Range;
}