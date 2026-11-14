using Unity.Entities;

public enum PickupType : byte
{
    HealthHeart = 0,
    ExpMagnet = 1,
    TreasureChest = 2
}

public enum RelicType : byte
{
    WindrunnerBoots = 0, // +15% MoveSpeed
    SpiritMagnet = 1,    // +50% Pickup Range
    HeartCrystal = 2,    // +30 Max HP & Full Heal
    PowerCharm = 3,      // +15% All Damage
    FortuneDice = 4,     // +2 Rerolls
    MirrorOfIllusion = 5, // [LEGENDARY] 2 Mirror Clones mimicking movement and casting all skills (25% DMG, 150% CD)
    WisdomTome = 6       // +15% EXP Gain per level
}

public struct PickupItemTag : IComponentData {}

public struct PickupData : IComponentData
{
    public PickupType Type;
    public float Value;    // e.g. Heal amount (25f)
    public float Radius;   // Bán kính va chạm nhặt
    public float FlySpeed; // 0 = nằm yên trên đất, >0 = đang bay vào người chơi
}

/// <summary>
/// Event kích hoạt khi người chơi nhặt được Rương Báu để GameHUDBridge mở Popup Cổ Vật
/// </summary>
public struct ChestOpenEvent : IComponentData
{
    public Entity PlayerEntity;
}
