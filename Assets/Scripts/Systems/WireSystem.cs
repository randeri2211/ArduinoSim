using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics; 
using UnityEngine;
using Microsoft.VisualBasic;

[UpdateAfter(typeof(ProximitySensorSystem))]
public partial struct WireSystem : ISystem
{
    public static bool t = true;
    // [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (t)
        {
            byte pin = 0;
            foreach (var (lt, mc, digitalPins, analogPins, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<MicroController>, DynamicBuffer<DigitalPin>, DynamicBuffer<AnalogPin>>().WithEntityAccess())
            {
                if (digitalPins.Length > pin)
                {
                    foreach (var ps in SystemAPI.Query<RefRW<ProximitySensor>>())
                    {
                        ps.ValueRW.sensor.MC = entity;
                        ps.ValueRW.sensor.Pin = pin;
                        Debug.Log("Pin #" + pin + " set");
                        pin++;
                    }
                }
                break;
            }
            t = false;
        }
    }

    // [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}