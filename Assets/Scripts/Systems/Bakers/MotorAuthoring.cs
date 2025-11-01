using Unity.Entities;
using UnityEngine;
using Unity.Physics;
using Unity.Mathematics; 
using Unity.Physics.Authoring;
using Unity.Rendering;

public class MotorAuthoring : MonoBehaviour
{
    [Header("Force (N) / Torque (N·m)")]
    public float MaxTorque = 0f;

    [Header("Direction")]
    public bool UseLocalAxis = true;
    public Vector3 Axis = Vector3.forward;        // +Z

    [Tooltip("Where force is applied. If UseLocalAxis=true => local point; else world point. Zero = center of mass.")]
    public Vector3 ForcePoint = Vector3.zero;

    [Range(-1f, 1f)] public float Throttle = 0f;
    public bool Enabled = true;
    public Vector3 BoxSize = new Vector3(1f, 1f, 1f);
    public float MassKg = 1f;

    class Baker : Baker<MotorAuthoring>
    {
        public override void Bake(MotorAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

            AddComponent(e, new Motor
            {
                MaxTorque = Mathf.Max(0f, a.MaxTorque),
                Throttle = Mathf.Clamp(a.Throttle, -1f, 1f),
                UseLocalAxis = a.UseLocalAxis,
                Axis = ((Vector3)a.Axis).normalized,
                ForcePoint = a.ForcePoint,
                Enabled = a.Enabled ? (byte)1 : (byte)0
            });

            // var geom = new BoxGeometry
            // {
            //     Center = float3.zero,
            //     Size = (float3)(a.BoxSize == Vector3.zero ? new float3(1f) : (float3)a.BoxSize),
            //     Orientation = quaternion.identity,
            //     BevelRadius = 0f
            // };

            // var filter = CollisionFilter.Default;

            // var collider = Unity.Physics.BoxCollider.Create(geom, filter);
            // // var collider = GetComponent<Unity.Physics.CapsuleCollider>();

            // AddBlobAsset(ref collider, out var hash);
            // AddComponent(e, new PhysicsCollider { Value = collider });


            // var massProps = collider.Value.MassProperties;
            // var mass = PhysicsMass.CreateDynamic(massProps, math.max(0.001f, a.MassKg));
            // AddComponent(e, mass);
            // AddComponent(e, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            // AddComponent(e, new PhysicsDamping { Linear = 0.01f, Angular = 0.2f });
            // AddComponent(e, new PhysicsGravityFactor { Value = 0f });

        }
    }
}
