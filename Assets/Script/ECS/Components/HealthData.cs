using Unity.Entities;

public struct HealthData : IComponentData
{
    public float CurrentHP;
    public float MaxHP;
}