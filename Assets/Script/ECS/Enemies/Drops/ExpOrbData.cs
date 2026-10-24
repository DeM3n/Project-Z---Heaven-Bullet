using Unity.Entities;

public struct ExpOrbTag : IComponentData {}

public struct ExpOrbData : IComponentData
{
    public int ExpValue;
    public float FlySpeed; // 0 = nằm yên trên đất, >0 = đang tăng tốc bay vào người chơi
}