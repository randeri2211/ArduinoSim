using UnityEngine;

// Common base for anything readable via SensorData -- lets RobotCommandDispatcher read
// any sensor type polymorphically instead of hardcoding ProximitySensor.Distance.
public abstract class Sensor : RobotPeripheral
{
    // peripheralPin lets multi-pin sensors (e.g. ColorSensor's R/G/B) differentiate;
    // single-pin sensors just ignore it.
    public abstract string Read(string peripheralPin);

    // Shared self-exclusion for sensors that scan their surroundings (Touch, Motion) --
    // a robot's own body parts shouldn't trigger its own touch/motion sensors.
    protected bool BelongsToSameRobot(Collider other) =>
        other.GetComponentInParent<Microcontroller>() == GetComponentInParent<Microcontroller>();
}
