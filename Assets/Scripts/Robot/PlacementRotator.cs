using UnityEngine;

// Tracks a spin offset while PlacementPreview positions a Component -- PlacementPreview
// reads SpinDegrees each frame and layers it on top of the continuous surface-normal
// alignment it computes, rather than this component rotating the transform directly
// (rotating around world-up doesn't make sense once the component is aligned to an
// arbitrary surface normal). Only ever attached to Components, never Shapes -- Shapes
// use ShapeTransformGizmo instead.
public class PlacementRotator : MonoBehaviour
{
    public KeyCode rotateKey = KeyCode.R;
    public float rotateStepDegrees = 90f;

    public float SpinDegrees { get; private set; }

    void Update()
    {
        if (Input.GetKeyDown(rotateKey))
            SpinDegrees += rotateStepDegrees;
    }
}
