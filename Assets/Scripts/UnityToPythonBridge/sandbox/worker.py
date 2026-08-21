# Runs INSIDE the sandbox container, never on the host. Reads one {"type":"exec",
# "code": ...} JSON line at a time from stdin, execs it, and reports back over stdout.
# SensorData/DriveMotor here don't talk to Unity directly (no network in this
# container) -- they write a request line to stdout and block reading the reply from
# stdin; the host process is the one actually talking to Unity, and relays the reply
# back down this same pipe.
import json
import sys


def _read_line():
    line = sys.stdin.readline()
    if not line:
        raise EOFError("stdin closed")
    return json.loads(line)


def _write(msg: dict):
    sys.stdout.write(json.dumps(msg) + "\n")
    sys.stdout.flush()


def _request(cmd_type: str, robot: str, data: str) -> str:
    _write({"type": "request", "cmd": cmd_type, "robot": robot, "data": data})
    reply = _read_line()
    return reply.get("value", "")


def SensorData(robot: str, pin: str):
    """pin: the microcontroller pin the sensor is wired to (e.g. "A0"), not its name."""
    if not type(robot) == str or not type(pin) == str:
        return -2
    reply = _request("SensorData", robot, pin)
    if reply == "ROBOT_NOT_FOUND":
        raise LookupError(f"robot '{robot}' not found")
    if reply == "SENSOR_NOT_FOUND":
        raise LookupError(f"no sensor wired to pin '{pin}' on robot '{robot}'")
    return float(reply)


def DriveMotor(robot: str, pin: str, pwm: int):
    """pin: the microcontroller pin the motor is wired to (e.g. "D3"), not its name.
    pwm: signed duty cycle, -255..255. Sign = direction, magnitude = drive strength."""
    if not type(robot) == str or not type(pin) == str:
        return -2
    reply = _request("MotorData", robot, f"{pin},{pwm}")
    if reply == "ROBOT_NOT_FOUND":
        raise LookupError(f"robot '{robot}' not found")
    if reply == "MOTOR_NOT_FOUND":
        raise LookupError(f"no motor wired to pin '{pin}' on robot '{robot}'")
    return reply == "True"


def _sandboxed_print(*args, sep=" ", end="\n", **_ignored):
    _write({"type": "output", "text": sep.join(str(a) for a in args) + end})


def main():
    while True:
        try:
            msg = _read_line()
        except EOFError:
            break

        if msg.get("type") != "exec":
            continue

        code = msg.get("code", "")
        sandbox_globals = {
            "SensorData": SensorData,
            "DriveMotor": DriveMotor,
            "print": _sandboxed_print,
        }
        try:
            exec(code, sandbox_globals)
        except Exception as e:
            _write({"type": "error", "text": f"failed at code execution due to {e}"})
        _write({"type": "done"})


if __name__ == "__main__":
    main()
