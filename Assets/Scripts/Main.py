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
        pass
    except Exception as e:
        print("Finished with " + str(e))
        s.close()
        running = False


"""for i in range(10):
    print(SensorData("robot1", "Proximity_Sensor"))
pwm = 128  # -255..255, signed duty cycle
print(DriveMotor("robot1", "FLM", -pwm))
print(DriveMotor("robot1", "FRM", pwm))
print(DriveMotor("robot1", "BLM", -pwm))
print(DriveMotor("robot1", "BRM", pwm))"""
