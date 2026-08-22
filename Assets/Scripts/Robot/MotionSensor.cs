using UnityEngine;

// Digital PIR-style sensor: reads "1" while another Rigidbody (not part of this robot)
// is moving within its cone/range, "0" otherwise. Detects OTHER things moving nearby,
// not this robot's own motion.
public class MotionSensor : Sensor
{
    public float Range = 3f;
    public int FovDegrees = 90;
    public float VelocityThreshold = 0.1f;

    [HideInInspector] public bool Detected;

    void FixedUpdate()
    {
        if (Parameters.EDITING) return;

        Detected = false;
        foreach (var col in Physics.OverlapSphere(transform.position, Range))
        {
            if (BelongsToSameRobot(col)) continue;

            var rb = col.attachedRigidbody;
            if (rb == null || rb.linearVelocity.magnitude < VelocityThreshold) continue;

            if (Vector3.Angle(transform.forward, col.transform.position - transform.position) > FovDegrees * 0.5f) continue;

            Detected = true;
            break;
        }
    }

    public override string Read(string peripheralPin) => Detected ? "1" : "0";
}
