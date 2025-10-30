using Unity.Entities;
using Unity.Collections;

public struct Sensor : IComponentData
{
    public Entity MC;
    public byte Pin;
    public bool Digital;
    public FixedString64Bytes Name;
}
