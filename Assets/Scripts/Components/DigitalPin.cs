using Unity.Entities;
using Unity.Mathematics;

public struct DigitalPin : IBufferElementData
{
    public bool INPUT;
    public bool HIGH;
}