using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class ProximitySensorAuthoring : MonoBehaviour
{
    public float MinRange = 0.02f;
    public float MaxRange = 4f;
    public float WorkingVoltage = 5f;
    public int MeasureAngle = 15;

    class Baker : Baker<ProximitySensorAuthoring>
    {
        public override void Bake(ProximitySensorAuthoring authoring)
        {
            // Create or get the baked entity, allowing transform updates
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add your ECS components
            AddComponent<SensorTag>(entity);
            AddComponent(entity, new ProximitySensor
            {
                MinRange = authoring.MinRange,
                MaxRange = authoring.MaxRange,
                WorkingVoltage = authoring.WorkingVoltage,
                MeasureAngle = authoring.MeasureAngle
            });

            AddComponent(entity, new ProximityHit {
                Distance  = -1f,
                Direction = float3.zero
                });
        }
    }
}
