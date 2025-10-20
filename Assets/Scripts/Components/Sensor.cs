using Unity.Entities;
public struct Sensor : IComponentData
{
    public Entity MC;
    public byte Pin;
    public bool Digital;
}
