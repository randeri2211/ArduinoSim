using Microsoft.VisualBasic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Burst;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct ProximityClosestDebugDrawSystem : ISystem
{
    // [BurstCompile]
    public void OnCreate(ref SystemState s)
    {
        s.RequireForUpdate<ProximityHit>();
        s.RequireForUpdate<ProximitySensor>();
        s.RequireForUpdate<LocalTransform>();
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState s)
    {
        if (!Constants.DEBUG)
        {
            return;
        }
        foreach (var (lt, sensor, closest) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<ProximitySensor>, RefRO<ProximityHit>>())
        {
            float3 origin = lt.ValueRO.Position;
            float3 dir = closest.ValueRO.Direction;
            float dist = closest.ValueRO.Distance;
            float maxR = math.max(0.01f, sensor.ValueRO.MaxRange);

            if (dist >= 0f && math.lengthsq(dir) > 0f)
                Debug.DrawLine(origin, origin + dir * dist, Color.green, 0f, false);
            else
                Debug.DrawLine(origin, origin + math.normalizesafe(math.forward(math.normalize(lt.ValueRO.Rotation)),
                                                                   new float3(0, 0, 1)) * maxR,
                               Color.red, 0f, false);
        }
    }
}