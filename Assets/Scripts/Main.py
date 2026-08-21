from UnityToPythonBridge.Utils import *
from UnityToPythonBridge import sandbox_runner

print("starting server")
sandbox_runner.init()

# Connect To Unity System Socket
running = True
while running:
    try:
        code = s.recv(1024).decode().strip()
        sandbox_runner.run_code(code)

    except socket.timeout:
        pass
    except Exception as e:
        print("Finished with " + str(e))
        s.close()
        running = False

sandbox_runner.shutdown()


"""for i in range(10):
    print(SensorData("robot1", "Proximity_Sensor"))
pwm = 128  # -255..255, signed duty cycle
print(DriveMotor("robot1", "FLM", -pwm))
print(DriveMotor("robot1", "FRM", pwm))
print(DriveMotor("robot1", "BLM", -pwm))
print(DriveMotor("robot1", "BRM", pwm))"""
