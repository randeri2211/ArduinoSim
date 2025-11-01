using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

[UpdateInGroup(typeof(PhysicsBuildWorldGroup))]
[UpdateBefore(typeof(BuildPhysicsWorld))]
public partial struct PhysicsProbeSystem : ISystem
{
    public void OnUpdate(ref SystemState s)
    {
        int bodies = 0, joints = 0;
        foreach (var _ in SystemAPI.Query<RefRO<PhysicsMass>, RefRW<PhysicsVelocity>>()) bodies++;
        foreach (var _ in SystemAPI.Query<RefRO<PhysicsConstrainedBodyPair>, RefRO<PhysicsJoint>>()) joints++;
        if (bodies == 0 || joints == 0)
        {
            // Debug.Log($"[Probe] bodies={bodies}, joints={joints} (need both >0 before BuildPhysicsWorld)");
        }
        else
        {
            // Debug.Log($"[Probe] bodies={bodies}, joints={joints}");
        }
    }
}
