using System;
using UnityEngine;

public class RobotCommandDispatcher : MonoBehaviour
{
    void OnEnable()
    {
        // Force a clean restart every time, rather than relying on RobotServerRuntime's
        // own "already running" guard -- if the Editor's domain reload is disabled,
        // static state (including a stale listener thread from a previous Play session)
        // survives across Play/Stop cycles, which otherwise silently reuses that old
        // thread instead of starting a fresh one for this session.
        RobotServerRuntime.Stop();
        RobotServerRuntime.Start(Constants.Port);
    }

    void OnDisable() => RobotServerRuntime.Stop();

    void Update()
    {
        while (RobotServerRuntime.Commands.TryDequeue(out var msg))
        {
            try
            {
                // Expect: {"type":"MotorData","robot":"robot1","data":"left_wheel,1.5"}
                var cmd = JsonUtility.FromJson<Command>(msg);

                if (!Microcontroller.Registry.TryGetValue(cmd.robot, out var mc))
                {
                    Debug.LogWarning($"[RobotCommandDispatcher] Unknown robot: {cmd.robot}");
                    RobotServerRuntime.Send("ROBOT_NOT_FOUND");
                    continue;
                }

                switch (cmd.type)
                {
                    case "MotorData":
                    {
                        // data: "<motorLocalName>,<pwm>" where pwm is -255..255
                        string[] data = cmd.data.Split(',');
                        bool found = mc.TryGetMotor(data[0], out var motor);
                        if (found)
                        {
                            var success = int.TryParse(data[1], out var pwm);
                            if (success) motor.SetPwm(pwm);
                            RobotServerRuntime.Send($"{success}");
                        }
                        else
                        {
                            RobotServerRuntime.Send("MOTOR_NOT_FOUND");
                        }
                        break;
                    }
                    case "SensorData":
                    {
                        bool found = mc.TryGetSensor(cmd.data, out var sensor, out var peripheralPin);
                        RobotServerRuntime.Send(found ? sensor.Read(peripheralPin) : "SENSOR_NOT_FOUND");
                        break;
                    }
                    default:
                        Debug.LogWarning($"[RobotCommandDispatcher] Unknown cmd: {cmd.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RobotCommandDispatcher] Bad JSON: {ex.Message} | {msg}");
            }
        }
    }

    [Serializable]
    public struct Command
    {
        public string type;
        public string robot;
        public string data;
    }
}
