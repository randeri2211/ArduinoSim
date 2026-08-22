using UnityEngine;

// Digital bump sensor: reads "1" while touching something that isn't part of this
// robot, "0" otherwise. Uses an active Physics.OverlapSphere each FixedUpdate rather
// than OnTriggerEnter/Exit -- Unity delivers trigger callbacks to the GameObject owning
// the nearest ANCESTOR Rigidbody, not necessarily this sensor's own GameObject, so a
// passive trigger callback here would silently never fire (same issue PlacementPreview
// had to work around for touch detection during placement).
public class TouchSensor : Sensor
{
    public float DetectRadius = 0.1f;

    [HideInInspector] public bool Touching;

    void FixedUpdate()
    {
        if (Parameters.EDITING) return;

        Touching = false;
        foreach (var col in Physics.OverlapSphere(transform.position, DetectRadius))
        {
            if (BelongsToSameRobot(col)) continue;
            Touching = true;
            break;
        }
    }

    public override string Read(string peripheralPin) => Touching ? "1" : "0";
}
