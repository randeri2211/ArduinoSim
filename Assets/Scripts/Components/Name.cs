using Unity.Entities;
using Unity.Collections;

public struct Name : IComponentData
{
    public FixedString64Bytes Value; // use 128/512 if you need longer names
}
