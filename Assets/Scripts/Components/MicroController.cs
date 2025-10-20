using Unity.Entities;

public struct MicroController : IComponentData
{
    public float MinAnalogVoltage;
    public float MaxAnalogVoltage;
    public byte AnalogPins;
    public byte DigitalPins;
}