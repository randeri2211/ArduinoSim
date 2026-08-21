using UnityEngine;

public class ProximitySensor : RobotPeripheral
{
    [Header("Sensor Settings")]
    public float MinRange = 0.02f;
    public float MaxRange = 4f;
    public int MeasureAngle = 15;
    public float WorkingVoltage = 5f;

    [HideInInspector] public float Distance = -1f;
    [HideInInspector] public Vector3 Direction = Vector3.zero;

    const int Rings = 5;         // excludes center; total rings = Rings (theta > 0) + center (theta = 0)
    const int BaseAzimuth = 6;   // rays in ring k ~ BaseAzimuth * k
    const float MinRangeFloor = 0.01f;
    const int MaxFovDegrees = 179; // clamp below 180 so the cone math stays well-defined

    void FixedUpdate()
    {
        if (Parameters.EDITING) return;
        Sense();
    }

    void Sense()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        Vector3 upRef = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(upRef, forward).normalized;
        Vector3 v = Vector3.Cross(forward, u).normalized;

        float minRange = Mathf.Max(0f, MinRange);
        float maxRange = Mathf.Max(MinRangeFloor, MaxRange);

        float halfFovDeg = Mathf.Clamp(MeasureAngle, 0, MaxFovDegrees) * 0.5f;
        float halfFovRad = halfFovDeg * Mathf.Deg2Rad;

        float bestDist = -1f;
        Vector3 bestDir = Vector3.zero;

        void TryCast(Vector3 dir)
        {
            if (Physics.Raycast(origin, dir, out var hitInfo, maxRange))
            {
                float dist = hitInfo.distance;
                if (dist >= minRange && (bestDist < 0f || dist < bestDist))
                {
                    bestDist = dist;
                    bestDir = dir;
                }
            }
        }

        // Center ray
        TryCast(forward);

        // Rings over the cone
        for (int k = 1; k <= Rings; k++)
        {
            float t = (float)k / Rings;
            float theta = t * halfFovRad;
            int rays = Mathf.Max(1, BaseAzimuth * k);
            float dPhi = (360f / rays) * Mathf.Deg2Rad;

            float cosT = Mathf.Cos(theta);
            float sinT = Mathf.Sin(theta);

            for (int i = 0; i < rays; i++)
            {
                float phi = i * dPhi;
                Vector3 dir = cosT * forward + sinT * (Mathf.Cos(phi) * u + Mathf.Sin(phi) * v);
                dir = dir.normalized;
                TryCast(dir);
            }
        }

        // Store the single closest result
        Distance = bestDist;                                  // -1 if none
        Direction = bestDist > 0 ? bestDir : Vector3.zero;     // saved for debug draw
    }

    void Update()
    {
        if (!Constants.DEBUG) return;

        Vector3 origin = transform.position;
        if (Distance >= 0f && Direction.sqrMagnitude > 0f)
            Debug.DrawLine(origin, origin + Direction * Distance, Color.green);
        else
            Debug.DrawLine(origin, origin + transform.forward * Mathf.Max(MinRangeFloor, MaxRange), Color.red);
    }
}
