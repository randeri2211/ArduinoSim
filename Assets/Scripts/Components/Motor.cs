using Unity.Entities;
using Unity.Mathematics;

public struct Motor : IComponentData
{
    public float MaxForce;      // Newtons, along motor axis
    public float MaxTorque;     // N·m, optional spin around axis
    public float Throttle;      // -1..1 (signed)
    public bool  UseLocalAxis;  // true = axis is local +Z, false = world Axis
    public float3 Axis;         // direction (normalized). If UseLocalAxis=true, this is local
    public float3 ForcePoint;   // point of application (local if UseLocalAxis, else world). Use float3.zero to apply at center of mass
    public byte  Enabled;       // 0/1
}