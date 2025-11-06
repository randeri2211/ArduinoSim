using Unity.Entities;
public struct ProximitySensor : IComponentData
{
    public float MinRange;
    public float MaxRange;
    public int MeasureAngle;
    public float WorkingVoltage;
}
