using UnityEngine;

// Handles rotating a part while PlacementPreview is positioning it. Kept as its own
// component so rotation behavior (step size, axis, free rotation, etc.) can be extended
// independently later without touching PlacementPreview or BuildUI at all.
public class PlacementRotator : MonoBehaviour
{
    public KeyCode rotateKey = KeyCode.R;
    public float rotateStepDegrees = 90f;

    void Update()
    {
        if (Input.GetKeyDown(rotateKey))
            transform.Rotate(Vector3.up, rotateStepDegrees, Space.World);
    }
}
