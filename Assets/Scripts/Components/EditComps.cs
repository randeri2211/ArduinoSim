using Unity.Entities;
using Unity.Mathematics;

public enum EditOp : byte { Spawn, Move, Rotate, ScaleUniform, ScaleXYZ, SetParent, ClearParent }

public struct EditRequest : IBufferElementData
{
    public EditOp Op;
    public int PrefabIndex;     // for Spawn
    public Entity Target;       // for Move/Rotate/Scale/Parent ops
    public Entity Parent;       // for SetParent
    public float3 P;            // position or scaleXYZ
    public float  F;            // angle (deg) or uniform scale
}

// Singleton holding the buffer:
public struct EditQueueTag : IComponentData {}
