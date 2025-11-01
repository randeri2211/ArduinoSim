using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class MicroControllerAuthoring : MonoBehaviour
{
    public float MinAnalogVoltage = 0f;
    public float MaxAnalogVoltage = 5f;
    public byte AnalogPins = 8;
    public byte DigitalPins = 8;

    class Baker : Baker<MicroControllerAuthoring>
    {
        public override void Bake(MicroControllerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new MicroController
            {
                MinAnalogVoltage = authoring.MinAnalogVoltage,
                MaxAnalogVoltage = authoring.MaxAnalogVoltage,
                AnalogPins = authoring.AnalogPins,
                DigitalPins = authoring.DigitalPins
            });

            var bufA = AddBuffer<AnalogPin>(entity);
            bufA.Capacity = math.max(0, authoring.AnalogPins);
            for(int i = 0;i < bufA.Capacity; i++)
            {
                bufA.Add(new AnalogPin
                {
                    MinAnalogVoltage = authoring.MinAnalogVoltage,
                    MaxAnalogVoltage = authoring.MaxAnalogVoltage,
                    Value = 0
                });
            }

            var bufD = AddBuffer<DigitalPin>(entity);
            bufD.Capacity = math.max(0, authoring.DigitalPins);
            for (int i = 0; i < bufD.Capacity; i++)
            {
                bufD.Add(new DigitalPin
                {
                    INPUT = true,
                    HIGH = false,
                });
            }
        }
    }
}
