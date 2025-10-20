using Unity.Entities;
using Unity.Mathematics;

public struct AnalogPin : IBufferElementData
{
    public float MinAnalogVoltage;
    public float MaxAnalogVoltage;
    public ushort Value;
}