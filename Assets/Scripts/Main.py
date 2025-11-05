# print("starting server1")
#
from UnityToPythonBridge.Utils import *

print("starting server")
# Connect To Unity System Socket
running = True
while running:
    try:
        code = s.recv(1024).decode().strip()
        try:
            print(code)
            exec(code)
        except Exception as e:
            print(f"failed at code execution due to {e}")

    except socket.timeout:
        print("timeout")
    except Exception as e:
        print("Finished with " + str(e))
        s.close()
        running = False


"""for i in range(10):
    print(SensorData("Proximity_Sensor"))
speed = 1
print(DriveMotor("FLM", -speed))
print(DriveMotor("FRM", speed))
print(DriveMotor("BLM", -speed))
print(DriveMotor("BRM", speed))"""
