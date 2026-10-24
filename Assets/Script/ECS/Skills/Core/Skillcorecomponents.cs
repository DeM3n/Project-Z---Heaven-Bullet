using Unity.Collections;
using Unity.Entities;

// Liên kết skill-entity về lại entity sở hữu nó (hiện tại luôn là Player, sau này có thể là ally/summon khác)
public struct SkillOwnerData : IComponentData
{
    public Entity Owner;
}

// Header chung cho MỌI loại skill, bất kể hiệu ứng cụ thể là gì
public struct SkillRuntimeData : IComponentData
{
    public float Cooldown;
    public float Timer;
    public int Level;              // 1-5 theo GDD 2.2, dùng cho UI/Fusion sau này
    public FixedString32Bytes SkillId; // key tra cứu SkillDefinition/FusionRecipe sau này (GDD 2.4)
}