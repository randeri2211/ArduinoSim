using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics; 
using UnityEngine;
using Microsoft.VisualBasic;

public partial struct ProximitySensorSystem : ISystem
{
    const int Rings = 5;         // excludes center; total rings = Rings (θ > 0) + center (θ = 0)
    const int BaseAzimuth = 6;   // rays in ring k ~ BaseAzimuth * k

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Only run if we actually have ProximitySensors in the world
        state.RequireForUpdate<ProximitySensor>();
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (Parameters.EDITING)
        {
            return;
        }
        var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var world = physics.CollisionWorld;
        var filter = CollisionFilter.Default;

        foreach (var (lt, sensor, closest) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProximitySensor>, RefRW<ProximityHit>>())
        {
            float3 origin = lt.ValueRO.Position;
            quaternion rot = math.normalize(lt.ValueRO.Rotation);

            float3 forward = math.normalizesafe(math.forward(rot), new float3(0, 0, 1));

            // Orthonormal basis around forward
            float3 up = math.abs(math.dot(forward, new float3(0, 1, 0))) > 0.99f ? new float3(1, 0, 0) : new float3(0, 1, 0);
            float3 u = math.normalizesafe(math.cross(up, forward), new float3(1, 0, 0));
            float3 v = math.normalizesafe(math.cross(forward, u), new float3(0, 1, 0));

            float minRange = math.max(0f, sensor.ValueRO.MinRange);
            float maxRange = math.max(0.01f, sensor.ValueRO.MaxRange);

            float halfFovDeg = math.clamp(sensor.ValueRO.MeasureAngle, 0, 179) * 0.5f;
            float halfFovRad = math.radians(halfFovDeg);

            // Track the best (nearest valid) hit
            float bestDist = -1f;
            float3 bestDir = float3.zero;

            // Helper: cast and update best
            void TryCast(float3 dir, float thetaDeg, float phiDeg)
            {
                var input = new RaycastInput
                {
                    Start = origin,
                    End = origin + dir * maxRange,
                    Filter = filter
                };

                if (UnityEngine.Physics.Raycast((Vector3)origin, (Vector3)dir, out var hitInfo, maxRange))
                {
                    float dist = hitInfo.distance;
                    if (dist >= minRange && (bestDist < 0f || dist < bestDist))
                    {
                        bestDist = dist;
                        bestDir = dir;
                    }
                }
            }

            // Center ray
            TryCast(forward, 0f, 0f);

            // Rings over the cone
            for (int k = 1; k <= Rings; k++)
            {
                float t = (float)k / Rings;
                float theta = t * halfFovRad;   // radians
                int rays = math.max(1, BaseAzimuth * k);
                float dPhi = math.radians(360f / rays);

                float cosT = math.cos(theta);
                float sinT = math.sin(theta);

                for (int i = 0; i < rays; i++)
                {
                    float phi = i * dPhi; // radians
                    float3 dir = cosT * forward + sinT * (math.cos(phi) * u + math.sin(phi) * v);
                    dir = math.normalizesafe(dir, forward);

                    TryCast(dir, math.degrees(theta), math.degrees(phi));
                }
            }

            // Write the single closest result to the component
            closest.ValueRW.Distance = bestDist;            // -1 if none
            closest.ValueRW.Direction = bestDist > 0 ? bestDir : float3.zero; // Save direction for debug purposes
        }
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}