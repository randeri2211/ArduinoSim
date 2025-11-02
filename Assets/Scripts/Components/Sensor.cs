using Unity.Entities;
using Unity.Collections;

public struct Sensor : IComponentData
{
    public FixedString64Bytes Name;
}
