using UnityEngine;

// Base for anything Microcontroller looks up by LocalName (Motor, ProximitySensor, ...).
// Defaults LocalName to the GameObject's own name if left blank.
public abstract class RobotPeripheral : MonoBehaviour
{
    public string LocalName;

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(LocalName)) LocalName = name;
    }
}
