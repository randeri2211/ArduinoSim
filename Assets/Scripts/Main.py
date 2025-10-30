
from UnityToPythonBridge.SensorData import *

if __name__ == '__main__':
    # Connect To Unity System Socket
    coms = Commands()


    print(coms.SensorData("HC-SR04 Sensor"))

    coms.close()



