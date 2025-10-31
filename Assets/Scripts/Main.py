
from UnityToPythonBridge.SensorData import *

if __name__ == '__main__':
    # Connect To Unity System Socket
    running = True
    while running:
        try:
            code = s.recv(1024).decode().strip().split('\n')
            for line in code:
                try:
                    eval(line)
                except Exception as e:
                    print(f"failed at code execution {line} due to {e}")
            # print(SensorData("HC-SR04 Sensor"))
        except socket.timeout:
            print("timeout")
        except Exception as e:
            print("Finished with " + str(e))
            close()
            running = False



