using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxBodyAuthoring : MonoBehaviour
{
    [Header("Box Geometry (meters)")]
    public Vector3 Size = new Vector3(1, 1, 1);

    [Header("Physical Properties")]
    [Tooltip("Density in kilograms per cubic meter (typical values: Wood=700, Steel=7800)")]
    public float Density = 1000f;

    [Tooltip("Global drag (0 = none)")]
    public float LinearDamping = 0.01f;
    public float AngularDamping = 0.01f;

    [Tooltip("Gravity scale (0 = no gravity)")]
    public float GravityFactor = 1f;

    class Baker : Baker<BoxBodyAuthoring>
    {
        public override void Bake(BoxBodyAuthoring a)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

            // 🔹 Compute the final box size (takes object scale into account)
            float3 scaledSize = (float3)a.Size * (float3)a.transform.lossyScale;
            float3 halfExtents = scaledSize * 0.5f;

            // 🔹 Build collider geometry
            var geom = new BoxGeometry
            {
                Center = float3.zero,
                Size = scaledSize,
                Orientation = quaternion.identity,
                BevelRadius = 0f
            };

            var filter = CollisionFilter.Default;
            var collider = Unity.Physics.BoxCollider.Create(geom, filter);

            // 🔹 Register collider blob with the baker (no leak)
            AddBlobAsset(ref collider, out _);
            AddComponent(entity, new PhysicsCollider { Value = collider });

            // 🔹 Compute volume and resulting mass from density
            float volume = scaledSize.x * scaledSize.y * scaledSize.z;
            float massKg = math.max(0.001f, volume * a.Density);
            Debug.Log(massKg);

            // 🔹 Compute physics mass (includes inertia)
            var massProps = collider.Value.MassProperties;
            var mass = PhysicsMass.CreateDynamic(massProps, massKg);
            AddComponent(entity, mass);

            // 🔹 Add default velocity & damping
            AddComponent(entity, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            AddComponent(entity, new PhysicsDamping
            {
                Linear = a.LinearDamping,
                Angular = a.AngularDamping
            });

            // 🔹 Gravity scale
            AddComponent(entity, new PhysicsGravityFactor { Value = a.GravityFactor });
        }
    }
}
