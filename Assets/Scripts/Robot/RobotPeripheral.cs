using System.Collections.Generic;
using UnityEngine;

// Base for anything Microcontroller can wire a pin to (Motor, ProximitySensor, ...).
// Defaults LocalName to the GameObject's own name if left blank -- used for display in
// the wiring UI, not for addressing (that's by microcontroller pin now, see Microcontroller).
public abstract class RobotPeripheral : MonoBehaviour
{
    public string LocalName;

    // Loaded from this component's sidecar JSON by whoever spawns it (see BuildUI).
    public Dictionary<string, List<string>> Pins = new();

    // Schematic position in WiringUI's grid, assigned once at spawn time (see BuildUI)
    // and updated live while the player drags the component's label there.
    public Vector2Int WiringGridPosition;

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(LocalName)) LocalName = name;
    }
}
