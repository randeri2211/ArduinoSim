using Microsoft.VisualBasic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
<<<<<<< HEAD
using Unity.Burst;
=======
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct ProximityClosestDebugDrawSystem : ISystem
{
<<<<<<< HEAD
    // [BurstCompile]
=======
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c
    public void OnCreate(ref SystemState s)
    {
        s.RequireForUpdate<ProximityHit>();
        s.RequireForUpdate<ProximitySensor>();
        s.RequireForUpdate<LocalTransform>();
    }

<<<<<<< HEAD
    // [BurstCompile]
=======
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c
    public void OnUpdate(ref SystemState s)
    {
        if (!Constants.DEBUG)
        {
            return;
        }
        foreach (var (lt, sensor, closest) in
<<<<<<< HEAD
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<ProximitySensor>, RefRO<ProximityHit>>())
        {
            float3 origin = lt.ValueRO.Position;
            float3 dir = closest.ValueRO.Direction;
            float dist = closest.ValueRO.Distance;
            float maxR = math.max(0.01f, sensor.ValueRO.MaxRange);
=======
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProximitySensor>, RefRO<ProximityHit>>())
        {
            float3 origin = lt.ValueRO.Position;
            float3 dir    = closest.ValueRO.Direction;
            float  dist   = closest.ValueRO.Distance;
            float  maxR   = math.max(0.01f, sensor.ValueRO.MaxRange);
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c

            if (dist >= 0f && math.lengthsq(dir) > 0f)
                Debug.DrawLine(origin, origin + dir * dist, Color.green, 0f, false);
            else
                Debug.DrawLine(origin, origin + math.normalizesafe(math.forward(math.normalize(lt.ValueRO.Rotation)),
<<<<<<< HEAD
                                                                   new float3(0, 0, 1)) * maxR,
=======
                                                                   new float3(0,0,1)) * maxR,
>>>>>>> f11b842e933bf12a28e6c86effca303a74d01c8c
                               Color.red, 0f, false);
        }
    }
}