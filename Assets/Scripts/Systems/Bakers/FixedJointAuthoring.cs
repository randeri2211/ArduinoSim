using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class FixedJointAuthoring : MonoBehaviour
{
    public GameObject Parent;                    // first cube
    public GameObject Child;                     // second cube
    public Vector3 ParentAnchor = Vector3.zero;  // anchor in parent local space
    public Vector3 ChildAnchor  = Vector3.zero;  // anchor in child local space

    class Baker : Baker<FixedJointAuthoring>
    {
        public override void Bake(FixedJointAuthoring a)
        {
            if (a.Parent == null || a.Child == null) return;

            var parent = GetEntity(a.Parent, TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
            var child  = GetEntity(a.Child,  TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

            // Create a separate joint entity (recommended pattern)

            var parentFrame = new RigidTransform(quaternion.identity, (float3)a.ParentAnchor);
            var childFrame  = new RigidTransform(quaternion.identity, (float3)a.ChildAnchor);

            var joint = PhysicsJoint.CreateFixed(parentFrame, childFrame);
            var pair  = new PhysicsConstrainedBodyPair(parent, child, enableCollision: false);

            AddComponent(child, pair);
            AddComponent(child, joint);
        }
    }
}
