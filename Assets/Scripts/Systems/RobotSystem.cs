// RobotServerSystem.cs
using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;
using Unity.Physics;

public partial struct RobotServerSystem : ISystem
{
    private ComponentLookup<Name> _nameLookup;
    public void OnCreate(ref SystemState state)
    {
        // Start server once when the world is created
        RobotServerRuntime.Start(7001);
        _nameLookup = state.GetComponentLookup<Name>(true);
    }

    public void OnDestroy(ref SystemState state)
    {
        RobotServerRuntime.Stop();
    }

    public void OnUpdate(ref SystemState state)
    {
        _nameLookup.Update(ref state);
        // Consume pending messages
        while (RobotServerRuntime.Commands.TryDequeue(out var msg))
        {
            try
            {
                // Expect: {"type":"Spawn","x":0,"y":1,"z":0}
                var cmd = JsonUtility.FromJson<Command>(msg);

                switch (cmd.type)
                {
                    case "MotorData":
                        {
                            foreach (var (joint, parent) in
                            SystemAPI.Query<RefRW<PhysicsJoint>, RefRO<Parent>>())
                            {
                                if (joint.ValueRW.JointType == JointType.AngularVelocityMotor)
                                {
                                    FixedString64Bytes pName;
                                    pName = _nameLookup[parent.ValueRO.Value].Value;
                                    string[] data = cmd.data.Split(",");
                                    if (pName == data[0])
                                    {
                                        float velocity;
                                        var success = float.TryParse(data[1], out velocity);
                                        var constraints = joint.ValueRW.GetConstraints();
                                        constraints.ElementAt(0).Target.x = velocity;
                                        joint.ValueRW.SetConstraints(constraints);
                                        RobotServerRuntime.Send($"{success}");
                                        break;
                                    }
                                }

                            }
                            break;
                        }
                    case "SensorData":
                        {
                            foreach (var (psensor, hit) in
                            SystemAPI.Query<RefRO<ProximitySensor>, RefRO<ProximityHit>>())
                            {
                                if (psensor.ValueRO.sensor.Name == cmd.data)
                                {
                                    var distance = hit.ValueRO.Distance;
                                    RobotServerRuntime.Send($"{distance}");
                                    break;
                                }
                            }
                            break;
                        }
                    default:
                        Debug.LogWarning($"[RobotServerSystem] Unknown cmd: {cmd.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RobotServerSystem] Bad JSON: {ex.Message} | {msg}");
            }
        }
    }

    [Serializable]
    public struct Command
    {
        public string type;
        public string data;
    }
}
