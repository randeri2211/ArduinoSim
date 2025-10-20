// Common tag to mark an entity as a sensor
using Unity.Entities;

public struct SensorTag : IComponentData {}     // empty tag



public struct SensorHit : IBufferElementData
{
    public Entity Target;
    public float Distance;
}