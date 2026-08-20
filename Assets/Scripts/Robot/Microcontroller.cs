using System.Collections.Generic;
using UnityEngine;

// The addressable identity of one robot: aggregates its own Motor/ProximitySensor
// children under local names, so RobotCommandDispatcher can resolve peripherals
// per-robot instead of by a name that must be unique across the whole scene.
public class Microcontroller : MonoBehaviour
{
    public string RobotId;

    public static readonly Dictionary<string, Microcontroller> Registry = new();

    readonly Dictionary<string, Motor> _motors = new();
    readonly Dictionary<string, ProximitySensor> _sensors = new();

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

    // Re-scans children for Motor/ProximitySensor components. Not called on enable --
    // parts can be added/rearranged interactively many times before the robot is ever
    // actually run, so scanning eagerly would just go stale. Called instead right before
    // running code (see CodeUI), so the lookup always reflects the robot as it currently is.
    public void Rescan()
    {
        _motors.Clear();
        foreach (var m in GetComponentsInChildren<Motor>())
            _motors[m.LocalName] = m;

        _sensors.Clear();
        foreach (var s in GetComponentsInChildren<ProximitySensor>())
            _sensors[s.LocalName] = s;
    }

    public bool TryGetMotor(string localName, out Motor motor) => _motors.TryGetValue(localName, out motor);
    public bool TryGetSensor(string localName, out ProximitySensor sensor) => _sensors.TryGetValue(localName, out sensor);

    // Static (kinematic) while building -- immune to gravity/collisions so parts don't
    // get jostled while being attached. Dynamic once actually run, so the robot behaves
    // as a real physics body driven by its own motors.
    public void SetStatic(bool isStatic)
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = isStatic;
    }
}
