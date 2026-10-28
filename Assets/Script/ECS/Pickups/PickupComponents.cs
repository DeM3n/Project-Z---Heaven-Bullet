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
    FortuneDice = 4      // +2 Rerolls
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
