using Unity.Entities;
using Unity.Mathematics;

public struct PlayerInputSingleton : IComponentData
{
    public float2 MoveInput;
}