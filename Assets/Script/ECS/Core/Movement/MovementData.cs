using Unity.Entities;
using Unity.Mathematics;

public struct MovementData : IComponentData
{
    public float2 Direction;  
    public float MoveSpeed;
}