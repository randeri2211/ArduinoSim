using UnityEngine;

// RGB color sensor: raycasts forward like ProximitySensor and reads the hit surface's
// material color, exposed as three separate Analog pins (R/G/B) -- the player wires
// each channel to its own microcontroller pin, mirroring how a real color sensor IC
// exposes one output per channel.
public class ColorSensor : Sensor
{
    public float MaxRange = 1f;

    [Tooltip("Beam width -- a bare raycast is infinitely thin, so small jitter (physics " +
             "settling, target micro-movement) flickers on/off a hit near the target's edge.")]
    public float BeamRadius = 0.02f;

    [HideInInspector] public Color LastColor = Color.black;

    void FixedUpdate()
    {
        if (Parameters.EDITING) return;

        LastColor = Color.black;
        if (Physics.SphereCast(transform.position, BeamRadius, transform.forward, out var hit, MaxRange))
        {
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null) LastColor = renderer.sharedMaterial.color;
        }
    }

    public override string Read(string peripheralPin)
    {
        float v = peripheralPin switch
        {
            "R" => LastColor.r,
            "G" => LastColor.g,
            "B" => LastColor.b,
            _ => 0f,
        };
        return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    void Update()
    {
        if (!Constants.DEBUG) return;
        Debug.DrawLine(transform.position, transform.position + transform.forward * MaxRange, LastColor);
    }
}
