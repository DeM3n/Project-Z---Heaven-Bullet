using Unity.Entities;

public struct DamageOnContactData : IComponentData
{
    public float Damage;
    public float HitRadius;
    public ushort SkillId;
    public Entity Source; // Entity người gây sát thương (Player hoặc Clone)
}