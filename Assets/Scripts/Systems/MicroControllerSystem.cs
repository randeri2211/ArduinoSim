using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics; 
using UnityEngine;
using Microsoft.VisualBasic;

[UpdateAfter(typeof(ProximitySensorSystem))]
public partial struct MicroControllerSystem : ISystem
{
    // [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Only run if we actually have MicroControllers in the world
        state.RequireForUpdate<MicroController>();
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (lt, mc, digitalPins, analogPins) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<MicroController>, DynamicBuffer<DigitalPin>, DynamicBuffer<AnalogPin>>())
        {
            int count = 0;
            foreach (var dPin in digitalPins)
            {
                Debug.Log("Pin #"+count+" Input:" + dPin.INPUT + "\nValue:" + dPin.HIGH + "\n");
                count++;
            }

            foreach (var aPin in analogPins)
            {
                
            }
        }
    }

    // [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}