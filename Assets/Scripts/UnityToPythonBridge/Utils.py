import json
import socket

s = socket.socket()
s.settimeout(2.0)
s.connect(("127.0.0.1", 7001))
def SensorData(sensor: str):
    if not type(sensor) == str:
        print("Invalid sensor type")
        return -2
    cmd = {"type": "SensorData", "data": sensor}
    s.sendall(json.dumps(cmd).encode() + b"\n")
    print("sent " + str(cmd))
    reply = s.recv(1024).decode().strip()
    return reply

def close():
    s.close()

def DriveMotor(motor: str, throttle):
    if not type(motor) == str:
        print("Invalid sensor type")
        return -2
    cmd = {"type": "MotorData", "data": f"{motor},{throttle}"}
    s.sendall(json.dumps(cmd).encode() + b"\n")
    print("sent " + str(cmd))
    # reply = s.recv(1024).decode().strip()
    # return reply