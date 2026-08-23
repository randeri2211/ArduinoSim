using UnityEngine;

// Tags a Shape or Component as re-selectable later to bring its TransformGizmo back up
// after it's already been placed/confirmed. See CameraMovement.HandleClick.
public class EditablePart : MonoBehaviour
{
    // Set once at spawn time (PlacementPreview.Begin()), when it's unambiguous which
    // kind this is. TransformGizmo reads this to decide whether Scale mode makes sense
    // -- deliberately NOT re-derived later via GetComponentInChildren<RobotPeripheral>,
    // which would search this object's WHOLE subtree and wrongly flag a Shape as a
    // Component just because some other Component (a Motor, say) happens to be
    // physically attached to it further down the hierarchy.
    public bool IsComponent;
}
