using Unity.Entities;

public struct DamageOnContactData : IComponentData
{
    public float Damage;
    public float HitRadius;
}