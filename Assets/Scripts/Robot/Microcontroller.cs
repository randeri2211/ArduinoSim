using System.Collections.Generic;
using UnityEngine;

// A wire's full routed path, not just its two endpoints -- the bends are the player's
// deliberate schematic layout choice (see WiringUI) and persist with the connection.
public class WireRoute
{
    public RobotPeripheral Peripheral;
    public string PeripheralPin;
    public List<Vector2Int> Bends = new(); // grid-cell coords, in click order from the mcu-pin side
}

// The addressable identity of one robot: holds its pin layout (loaded from a sidecar
// JSON, see PinLayout) and the wiring table connecting its pins to attached
// RobotPeripherals, so RobotCommandDispatcher can resolve peripherals per-robot by
// microcontroller pin name instead of by LocalName.
public class Microcontroller : MonoBehaviour
{
    public string RobotId;

    public static readonly Dictionary<string, Microcontroller> Registry = new();

    // Loaded from this microcontroller's sidecar JSON by whoever spawns it (see BuildUI).
    public Dictionary<string, List<string>> Pins = new();

    readonly Dictionary<string, WireRoute> _wiring = new();

    void OnEnable()
    {
        if (string.IsNullOrEmpty(RobotId))
        {
            Debug.LogError($"Microcontroller on '{name}' has no RobotId assigned.");
            return;
        }

        // The robot root is the static anchor everything else is built on -- kinematic
        // so gravity/collisions can never move it; only a Motor's HingeJoint (or some
        // future explicit joint) introduces relative motion within the robot.
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        Registry[RobotId] = this;
    }

    void OnDisable()
    {
        if (!string.IsNullOrEmpty(RobotId) && Registry.TryGetValue(RobotId, out var mc) && mc == this)
            Registry.Remove(RobotId);
    }

    // Connects a microcontroller pin to a peripheral's pin along the given routed path.
    // Fails if either pin doesn't exist, is already wired, or the two pins' capability
    // tags don't overlap at all.
    public bool TryConnect(string mcuPin, RobotPeripheral peripheral, string peripheralPin, List<Vector2Int> bends, out string error)
    {
        if (!Pins.TryGetValue(mcuPin, out var mcuCaps))
        {
            error = $"no such pin '{mcuPin}'";
            return false;
        }
        if (peripheral == null || !peripheral.Pins.TryGetValue(peripheralPin, out var wantCaps))
        {
            error = $"no such component pin '{peripheralPin}'";
            return false;
        }
        if (_wiring.ContainsKey(mcuPin))
        {
            error = $"'{mcuPin}' is already wired";
            return false;
        }
        if (TryGetMcuPin(peripheral, peripheralPin, out _))
        {
            error = $"'{peripheral.LocalName}.{peripheralPin}' is already wired";
            return false;
        }
        if (!PinLayout.Compatible(mcuCaps, wantCaps))
        {
            error = $"'{mcuPin}' is not compatible with '{peripheralPin}'";
            return false;
        }

        _wiring[mcuPin] = new WireRoute
        {
            Peripheral = peripheral,
            PeripheralPin = peripheralPin,
            Bends = bends ?? new List<Vector2Int>(),
        };
        error = null;
        return true;
    }

    public void Disconnect(string mcuPin) => _wiring.Remove(mcuPin);

    public bool TryGetWire(string mcuPin, out RobotPeripheral peripheral, out string peripheralPin)
    {
        if (_wiring.TryGetValue(mcuPin, out var route))
        {
            peripheral = route.Peripheral;
            peripheralPin = route.PeripheralPin;
            return true;
        }
        peripheral = null;
        peripheralPin = null;
        return false;
    }

    // Full routed path (including bends), for WiringUI's rendering.
    public bool TryGetRoute(string mcuPin, out WireRoute route) => _wiring.TryGetValue(mcuPin, out route);

    // True if mcuPin is currently wired to something.
    public bool IsWired(string mcuPin) => _wiring.ContainsKey(mcuPin);

    // True if this peripheral's own pin is currently wired to something.
    public bool IsWired(RobotPeripheral peripheral, string peripheralPin) => TryGetMcuPin(peripheral, peripheralPin, out _);

    // Reverse lookup: which microcontroller pin (if any) is this peripheral's pin wired to.
    public bool TryGetMcuPin(RobotPeripheral peripheral, string peripheralPin, out string mcuPin)
    {
        foreach (var kv in _wiring)
        {
            if (kv.Value.Peripheral == peripheral && kv.Value.PeripheralPin == peripheralPin)
            {
                mcuPin = kv.Key;
                return true;
            }
        }
        mcuPin = null;
        return false;
    }

    public bool TryGetMotor(string mcuPin, out Motor motor)
    {
        motor = TryGetWire(mcuPin, out var peripheral, out _) ? peripheral as Motor : null;
        return motor != null;
    }

    // peripheralPin is returned too so multi-pin sensors (e.g. ColorSensor's R/G/B) know
    // which of their pins was actually wired to mcuPin.
    public bool TryGetSensor(string mcuPin, out Sensor sensor, out string peripheralPin)
    {
        bool found = TryGetWire(mcuPin, out var peripheral, out peripheralPin);
        sensor = found ? peripheral as Sensor : null;
        return sensor != null;
    }

    // Prunes wires whose peripheral was destroyed or detached from this robot. Not
    // called automatically -- parts can be added/rearranged/rewired many times before
    // the robot is ever actually run. Called instead right before running code (see
    // CodeUI), so wiring always reflects the robot as it currently is.
    public void Rescan()
    {
        var stale = new List<string>();
        foreach (var kv in _wiring)
        {
            var peripheral = kv.Value.Peripheral;
            if (peripheral == null || !peripheral.transform.IsChildOf(transform))
                stale.Add(kv.Key);
        }
        foreach (var mcuPin in stale)
            _wiring.Remove(mcuPin);
    }

    // Static (kinematic) while building -- immune to gravity/collisions so parts don't
    // get jostled while being attached. Dynamic once actually run, so the robot behaves
    // as a real physics body driven by its own motors.
    public void SetStatic(bool isStatic)
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = isStatic;
    }
}
