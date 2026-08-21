import json
import os
import socket
import time

# Unity sets these when it launches this process, sourced directly from its own
# Constants.cs -- so there's one real source of truth, not a hand-synced copy here.
# The fallback defaults only apply when running this script standalone (outside Unity).
PORT = int(os.environ.get("ROBOT_PORT", 7003))
PWM_MAX = int(os.environ.get("ROBOT_PWM_MAX", 255))

connected = False
while not connected:
    try:
        s = socket.socket()
        s.settimeout(2.0)
        s.connect(("127.0.0.1", PORT))
        connected = True
    except Exception as e:
        print(f"connect failed: {e}")
        time.sleep(0.1)

def _send_command(cmd_type: str, robot: str, data: str) -> str:
    cmd = {"type": cmd_type, "robot": robot, "data": data}
    s.sendall(json.dumps(cmd).encode() + b"\n")
    print("sent " + str(cmd))
    reply = s.recv(1024).decode().strip()
    if reply == "ROBOT_NOT_FOUND":
        raise LookupError(f"robot '{robot}' not found")
    return reply


def SensorData(robot: str, sensor: str):
    if not type(robot) == str or not type(sensor) == str:
        print("Invalid robot/sensor type")
        return -2
    reply = _send_command("SensorData", robot, sensor)
    if reply == "SENSOR_NOT_FOUND":
        raise LookupError(f"sensor '{sensor}' not found on robot '{robot}'")
    return float(reply)


def DriveMotor(robot: str, motor: str, pwm: int):
    """pwm: signed duty cycle, -255..255. Sign = direction, magnitude = drive strength.
    Applies constant torque scaled from the motor's MaxTorque spec -- not a target speed."""
    if not type(robot) == str or not type(motor) == str:
        print("Invalid robot/motor type")
        return -2
    if not (-PWM_MAX <= pwm <= PWM_MAX):
        raise ValueError(f"pwm out of range: {pwm} (must be -{PWM_MAX}..{PWM_MAX})")
    reply = _send_command("MotorData", robot, f"{motor},{pwm}")
    if reply == "MOTOR_NOT_FOUND":
        raise LookupError(f"motor '{motor}' not found on robot '{robot}'")
    return True
