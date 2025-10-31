
from UnityToPythonBridge.SensorData import *

if __name__ == '__main__':
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
            # for line in code:
            #     print(line)
            #     try:
            #         eval(line)
            #     except Exception as e:
            #         print(f"failed at code execution {line} due to {e}")

        except socket.timeout:
            print("timeout")
        except Exception as e:
            print("Finished with " + str(e))
            close()
            running = False


for i in range(10):
    print(SensorData("HC-SR04 Sensor"))