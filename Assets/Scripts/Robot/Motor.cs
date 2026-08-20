using UnityEngine;

// Models a PWM-driven DC motor: SetPwm(-255..255) applies a constant torque
// (scaled from MaxTorque) around Axis every physics step -- no velocity target,
// matching how a real motor driver (PWM duty cycle -> torque) works.
[RequireComponent(typeof(Rigidbody))]
public class Motor : RobotPeripheral
{
    [Tooltip("Torque (N*m) applied at full PWM (255), per the motor's spec sheet.")]
    public float MaxTorque = 50f;

    [Tooltip("Local axis this motor spins around (should match the Hinge Joint's axis).")]
    public Vector3 Axis = Vector3.right;

    int _pwm;
    Rigidbody _rb;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();
    }

    // pwm: signed, -255..255. Sign = direction, magnitude = drive strength (duty cycle).
    public void SetPwm(int pwm)
    {
        _pwm = Mathf.Clamp(pwm, -Constants.PwmMax, Constants.PwmMax);
    }

    void FixedUpdate()
    {
        if (_pwm == 0) return;
        float torque = _pwm / (float)Constants.PwmMax * MaxTorque;
        _rb.AddRelativeTorque(Axis.normalized * torque, ForceMode.Force);
    }

#if UNITY_EDITOR
    [ContextMenu("Test: SetPwm(1)")]
    void TestPwmForward() => SetPwm(1);

    [ContextMenu("Test: SetPwm(-1)")]
    void TestPwmReverse() => SetPwm(-1);

    [ContextMenu("Test: Stop")]
    void TestStop() => SetPwm(0);
#endif
}
