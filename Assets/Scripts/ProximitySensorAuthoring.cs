using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class ProximitySensorAuthoring : MonoBehaviour
{
    public float MinRange = 0.02f;
    public float MaxRange = 4f;
    public float WorkingVoltage = 5f;
    public int MeasureAngle = 15;
    public const bool Digital = true;
    public Entity MC;

    class Baker : Baker<ProximitySensorAuthoring>
    {
        public override void Bake(ProximitySensorAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ProximitySensor
            {
                MinRange = authoring.MinRange,
                MaxRange = authoring.MaxRange,
                WorkingVoltage = authoring.WorkingVoltage,
                MeasureAngle = authoring.MeasureAngle,
                sensor = new Sensor
                {
                    Digital = true,
                    MC = authoring.MC,
                },
            });

            AddComponent(entity, new ProximityHit {
                Distance  = -1f,
                Direction = float3.zero
                });
        }
    }
}
