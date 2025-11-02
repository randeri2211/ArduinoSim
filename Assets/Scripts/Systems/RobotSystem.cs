// RobotServerSystem.cs
using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;


public partial struct RobotServerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Start server once when the world is created
        RobotServerRuntime.Start(7001);
    }

    public void OnDestroy(ref SystemState state)
    {
        RobotServerRuntime.Stop();
    }

    public void OnUpdate(ref SystemState state)
    {
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
                            foreach (var (motor, name) in
                            SystemAPI.Query<RefRW<Motor>, RefRO<Name>>())
                            {
                                string[] data = cmd.data.Split(",");
                                Debug.Log(name.ValueRO.Value);

                                if (name.ValueRO.Value == data[0])
                                {
                                    float throttle;
                                    var success = float.TryParse(data[1],out throttle);
                                    motor.ValueRW.Throttle = throttle;
                                    RobotServerRuntime.Send($"{success}");
                                    break;
                                }
                            }
                            Debug.Log("Moving");
                            break;
                        }
                    case "SensorData":
                        {
                            bool found = false;
                            foreach (var (psensor, hit) in
                            SystemAPI.Query<RefRO<ProximitySensor>, RefRO<ProximityHit>>())
                            {
                                if (psensor.ValueRO.sensor.Name == cmd.data)
                                {
                                    var distance = hit.ValueRO.Distance;
                                    RobotServerRuntime.Send($"{distance}");
                                    found = true;
                                    break;
                                }
                            }
                            Debug.Log(found);
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
