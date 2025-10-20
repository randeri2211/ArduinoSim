using Unity.Entities;
using Unity.Mathematics;

public struct ProximityHit : IComponentData
{
    public float3 Direction;
    public float Distance;   // meters; -1 if no hit, or clamped to MaxRange
}