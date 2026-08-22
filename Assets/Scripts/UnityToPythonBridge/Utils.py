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
    except Exception:
        time.sleep(0.1)

def _send_command(cmd_type: str, robot: str, data: str) -> str:
    """Raw send/receive, no interpretation of the reply -- callers decide what the
    reply means (also reused by sandbox_runner as a pure relay primitive)."""
    cmd = {"type": cmd_type, "robot": robot, "data": data}
    s.sendall(json.dumps(cmd).encode() + b"\n")
    return s.recv(1024).decode().strip()


def SensorData(robot: str, pin: str):
    """pin: the microcontroller pin the sensor is wired to (e.g. "A0"), not its name."""
    if not type(robot) == str or not type(pin) == str:
        return -2
    reply = _send_command("SensorData", robot, pin)
    if reply == "ROBOT_NOT_FOUND":
        raise LookupError(f"robot '{robot}' not found")
    if reply == "SENSOR_NOT_FOUND":
        raise LookupError(f"no sensor wired to pin '{pin}' on robot '{robot}'")
    return float(reply)


def DriveMotor(robot: str, pin: str, pwm: int):
    """pin: the microcontroller pin the motor is wired to (e.g. "D3"), not its name.
    pwm: signed duty cycle, -255..255. Sign = direction, magnitude = drive strength.
    Applies constant torque scaled from the motor's MaxTorque spec -- not a target speed."""
    if not type(robot) == str or not type(pin) == str:
        return -2
    if not (-PWM_MAX <= pwm <= PWM_MAX):
        raise ValueError(f"pwm out of range: {pwm} (must be -{PWM_MAX}..{PWM_MAX})")
    reply = _send_command("MotorData", robot, f"{pin},{pwm}")
    if reply == "ROBOT_NOT_FOUND":
        raise LookupError(f"robot '{robot}' not found")
    if reply == "MOTOR_NOT_FOUND":
        raise LookupError(f"no motor wired to pin '{pin}' on robot '{robot}'")
    return reply == "True"
