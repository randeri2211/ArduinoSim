using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MotorInputSystem : ISystem
{
    [BurstCompile] public void OnCreate(ref SystemState s) {}
    [BurstCompile] public void OnDestroy(ref SystemState s) {}

    public void OnUpdate(ref SystemState s)
    {
        // Editor-only input; remove or gate behind a define in real builds.
        float t = 0f;
        float tStep = 0.05f;
        if (Input.GetKey(KeyCode.F3))
        {
            t += tStep;
        }
        if (Input.GetKey(KeyCode.F4))
        {
            t -= tStep;
        }
        // Debug.Log("t " + t);
        foreach (var motor in SystemAPI.Query<RefRW<Motor>>())
        {
            motor.ValueRW.Throttle = math.clamp(t, -1f, 1f);
        }
    }
}
