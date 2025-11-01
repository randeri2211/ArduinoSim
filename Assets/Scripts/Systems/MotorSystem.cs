using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

[BurstCompile]
// [UpdateInGroup(typeof(PhysicsSimulationGroup))]
[UpdateInGroup(typeof(PhysicsBuildWorldGroup))]
[UpdateBefore(typeof(BuildPhysicsWorld))]
// [UpdateAfter(typeof(ExportPhysicsWorld))]
public partial struct MotorSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState s)
    {
        s.RequireForUpdate<PhysicsWorldSingleton>();
        s.RequireForUpdate<Motor>();
    }

    [BurstCompile] public void OnUpdate(ref SystemState s)
    {
        var job = new ApplyMotorJob { dt = SystemAPI.Time.DeltaTime };
        s.Dependency = job.ScheduleParallel(s.Dependency); // ✅ no named args
    }

    [BurstCompile]
    public partial struct ApplyMotorJob : IJobEntity
    {
        public float dt;

        void Execute(
            ref PhysicsVelocity vel,
            in PhysicsMass mass,
            in LocalTransform lt,
            in Motor motor)
        {
            if (motor.Enabled == 0 || (motor.MaxForce == 0f && motor.MaxTorque == 0f))
                return;

            // Axis/world
            float3 axisWorld = motor.UseLocalAxis
                ? math.normalizesafe(math.mul(lt.Rotation, motor.Axis), new float3(0,0,1))
                : math.normalizesafe(motor.Axis, new float3(0,0,1));

            // Torque-only example; add your force if needed
            float th = math.clamp(motor.Throttle, -1f, 1f);
            float3 torque = axisWorld * (motor.MaxTorque * th);
            float3 L = torque * dt;

            // world inverse inertia
            var orient = math.mul(lt.Rotation, mass.InertiaOrientation);
            float3x3 R = new float3x3(orient);
            float3x3 IbInv = float3x3.Scale(mass.InverseInertia);
            float3x3 invIw = math.mul(math.mul(R, IbInv), math.transpose(R));

            vel.Angular += math.mul(invIw, L);

            // Linear force example (optional)
            // float3 force = axisWorld * (motor.MaxForce * th);
            // vel.Linear += force * dt * mass.InverseMass;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState s) { }

    static float3x3 ComputeInverseInertiaWorld(in PhysicsMass mass, in quaternion worldRotation)
    {
        // Rotate the body-space inertia into world space:
        // Iw⁻¹ = R * Ib⁻¹ * Rᵀ, where R = (worldRotation * mass.InertiaOrientation)
        var orient = math.mul(worldRotation, mass.InertiaOrientation);
        float3x3 R = new float3x3(orient);                 // rotation matrix
        float3x3 IbInv = float3x3.Scale(mass.InverseInertia); // diagonal inverse inertia in body space
        return math.mul(math.mul(R, IbInv), math.transpose(R));
    }
}
