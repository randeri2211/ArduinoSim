import socket, json
import time

class Commands:
    def __init__(self):
        self.s = socket.socket()
        self.s.connect(("127.0.0.1", 7001))

    def SensorData(self, sensor: str):
        cmd = {"type": "SensorData", "data": sensor}
        self.s.sendall(json.dumps(cmd).encode() + b"\n")
        print("sent " + str(cmd))
        reply = self.s.recv(1024).decode().strip()
        return reply

    def close(self):
        self.s.close()
