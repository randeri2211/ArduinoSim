using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class ProximitySensorAuthoring : MonoBehaviour
{
    public float MinRange = 0.02f;
    public float MaxRange = 4f;
    public float WorkingVoltage = 5f;
    public int MeasureAngle = 15;
<<<<<<< HEAD
    public const bool Digital = true;
    public Entity MC;
=======
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c

    class Baker : Baker<ProximitySensorAuthoring>
    {
        public override void Bake(ProximitySensorAuthoring authoring)
        {
<<<<<<< HEAD
            var entity = GetEntity(TransformUsageFlags.Dynamic);

=======
            // Create or get the baked entity, allowing transform updates
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add your ECS components
            AddComponent<SensorTag>(entity);
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c
            AddComponent(entity, new ProximitySensor
            {
                MinRange = authoring.MinRange,
                MaxRange = authoring.MaxRange,
                WorkingVoltage = authoring.WorkingVoltage,
<<<<<<< HEAD
                MeasureAngle = authoring.MeasureAngle,
                sensor = new Sensor
                {
                    Digital = true,
                    MC = authoring.MC,
                },
=======
                MeasureAngle = authoring.MeasureAngle
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c
            });

            AddComponent(entity, new ProximityHit {
                Distance  = -1f,
                Direction = float3.zero
                });
        }
    }
}
