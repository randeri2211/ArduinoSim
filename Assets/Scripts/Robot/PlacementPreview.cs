using System.Collections.Generic;
using UnityEngine;

// Attached to a newly-spawned Shape/Component while the player positions it. Handles
// the whole spawned hierarchy, not just its root -- a compound part (like a motor
// assembly with a separate housing and shaft) can have its driving Rigidbody on a
// child rather than the root object this component lives on.
//
// Confirm is only allowed once it's touching something else on the robot; on confirm,
// this becomes a child of whatever it's touching. Everything is rigid by default (each
// Rigidbody found in the hierarchy is dropped, so it rides along via ordinary Transform
// inheritance) -- any Rigidbody that belongs to a Motor is the exception, since Motor
// actively applies torque and needs its own Rigidbody plus a HingeJoint back to its
// mount point to be held in place.
public class PlacementPreview : MonoBehaviour
{
    // Only one placement can be in progress at a time -- CameraMovement checks this to
    // avoid its own click-drag-select stealing the confirm click, and BuildUI checks it
    // to cancel whatever's currently held before starting a new spawn.
    public static PlacementPreview Current { get; private set; }

    public KeyCode confirmKey = KeyCode.Mouse0;
    public KeyCode cancelKey = KeyCode.Escape;
    public float scrollSpeed = 2f;

    Camera _cam;
    int _spawnFrame;
    float _distance;
    string _prefabBaseName;

    // Every Rigidbody anywhere in the spawned hierarchy, with whether it started
    // kinematic so non-motor ones can be restored correctly if ever needed.
    readonly List<(Rigidbody rb, bool wasKinematic)> _rigidbodies = new();
    readonly List<Collider> _colliders = new();
    readonly HashSet<Collider> _touching = new();

    // The push-out direction ComputePenetration found for whichever collider is
    // currently closest (same selection ClosestTouching() makes) -- doubles as an
    // approximate surface normal for Components' flush-orientation, reusing physics
    // data that's already being computed each frame rather than adding a raycast.
    Vector3 _surfaceNormal = Vector3.down;

    // prefabBaseName: the source prefab's name (e.g. "Motor"), used to auto-name this
    // part as "<prefabBaseName><N>" on confirm. Leave null for Shapes, which aren't
    // looked up by LocalName at all.
    public void Begin(Camera cam, string prefabBaseName = null)
    {
        Current = this;
        _spawnFrame = Time.frameCount;
        _cam = cam;
        _prefabBaseName = prefabBaseName;
        _distance = Vector3.Distance(cam.transform.position, transform.position);

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            _rigidbodies.Add((rb, rb.isKinematic));
            rb.isKinematic = true;
        }
        // A spawn with no Rigidbody anywhere (a plain Shape) still needs one so it can
        // be moved as a preview and behave physically once confirmed.
        if (_rigidbodies.Count == 0)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            _rigidbodies.Add((rb, false));
        }

        GetComponentsInChildren(_colliders);
        foreach (var c in _colliders)
            c.isTrigger = true;

        gameObject.AddComponent<EditablePart>().IsComponent = prefabBaseName != null;
        if (prefabBaseName == null)
        {
            gameObject.AddComponent<TransformGizmo>();
        }
        else
        {
            gameObject.AddComponent<PlacementRotator>();
        }
    }

    void Update()
    {
        if (!Parameters.EDITING)
        {
            Cancel();
            return;
        }

        _distance = Mathf.Max(0.3f, _distance - Input.mouseScrollDelta.y * scrollSpeed * Time.deltaTime * 60f);
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        transform.position = ray.origin + ray.direction * _distance;

        RefreshTouching();
        ResolveOverlap();
        if (_prefabBaseName != null) UpdateComponentOrientation();

        bool sameFrameAsSpawn = Time.frameCount == _spawnFrame;
        if (!sameFrameAsSpawn && Input.GetKeyDown(confirmKey) && _touching.Count > 0)
            Confirm();
        else if (!sameFrameAsSpawn && Input.GetKeyDown(cancelKey))
            Cancel();
    }

    // Rebuilds _touching by actively querying for overlaps every frame, rather than
    // relying on OnTriggerEnter/Exit -- Unity delivers those callbacks to whichever
    // GameObject the *nearest* Rigidbody in the hierarchy lives on, not necessarily to
    // this component's own GameObject. A compound part (like a motor with its own
    // Rigidbody on a child) would silently never trigger these callbacks here at all,
    // so this queries directly instead of depending on that routing.
    // Excludes the Gizmo layer (once created -- see TransformGizmo) so a shape's
    // own scale/rotate handles, which sit right at its faces, never register as
    // something it's "touching". LayerMask.NameToLayer returns -1 until that layer
    // actually exists, and 1 << -1 is 0, so this is just ~0 (everything) until then.
    static int TouchLayerMask()
    {
        int gizmoLayer = LayerMask.NameToLayer("Gizmo");
        return gizmoLayer >= 0 ? ~(1 << gizmoLayer) : ~0;
    }

    void RefreshTouching()
    {
        _touching.Clear();
        int layerMask = TouchLayerMask();
        foreach (var mine in _colliders)
        {
            var bounds = mine.bounds;
            float radius = bounds.extents.magnitude + 0.05f;
            foreach (var candidate in Physics.OverlapSphere(bounds.center, radius, layerMask, QueryTriggerInteraction.Collide))
            {
                if (candidate == null || _colliders.Contains(candidate)) continue;
                if (Physics.ComputePenetration(
                        mine, mine.transform.position, mine.transform.rotation,
                        candidate, candidate.transform.position, candidate.transform.rotation,
                        out _, out _))
                {
                    _touching.Add(candidate);
                }
            }
        }
    }

    // Pushes the preview back out of anything it's currently overlapping -- since this
    // runs after the raycast has already tried to push it forward this frame, the net
    // effect is that it stops right at the surface instead of sinking into it, which
    // reads as snapping.
    void ResolveOverlap()
    {
        var closest = ClosestTouching();
        foreach (var other in _touching)
        {
            if (other == null) continue;
            foreach (var mine in _colliders)
            {
                if (Physics.ComputePenetration(
                        mine, mine.transform.position, mine.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out var direction, out var distance))
                {
                    transform.position += direction * distance;
                    if (other == closest) _surfaceNormal = direction;
                }
            }
        }
    }

    // The single touching collider closest to this preview -- what Confirm() attaches
    // to, and (for Components) what UpdateComponentOrientation() aligns against.
    Collider ClosestTouching()
    {
        Collider best = null;
        float bestDist = float.MaxValue;
        foreach (var c in _touching)
        {
            if (c == null) continue;
            float d = Vector3.Distance(transform.position, c.ClosestPoint(transform.position));
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    // Continuously aligns a Component's local "bottom" (the mount-face convention every
    // component prefab follows) to the surface normal of whatever it's closest to
    // touching, then layers the player's manual spin (see PlacementRotator) on top
    // around that same local axis. Leaves rotation alone if nothing's touched.
    void UpdateComponentOrientation()
    {
        if (_touching.Count == 0) return;
        float spin = GetComponent<PlacementRotator>() is { } rotator ? rotator.SpinDegrees : 0f;
        transform.rotation = Quaternion.FromToRotation(Vector3.down, _surfaceNormal) * Quaternion.AngleAxis(spin, Vector3.down);
    }

    void Confirm()
    {
        var closest = ClosestTouching();
        Transform attachTo = closest != null ? closest.transform : null;

        foreach (var c in _colliders)
            c.isTrigger = false;

        Rigidbody attachToParentRigidbody = attachTo != null ? attachTo.GetComponentInParent<Rigidbody>() : null;

        // Rigid by default: each Rigidbody found in the hierarchy becomes a fixed part of
        // the robot, moving as one unit with whatever it's attached to (ultimately the
        // static Arduino UNO root), not an independent physics body merely resting via
        // gravity/contact. A Rigidbody that belongs to a Motor is the exception -- Motor
        // actively applies torque, so it keeps that Rigidbody and gets its HingeJoint
        // wired to the mount point's Rigidbody instead, which is what actually holds it
        // in place (Transform parenting alone doesn't constrain physics).
        foreach (var (rb, wasKinematic) in _rigidbodies)
        {
            if (rb == null) continue;
            var motor = rb.GetComponent<Motor>();
            if (motor != null)
            {
                rb.isKinematic = wasKinematic;
                var hinge = rb.GetComponent<HingeJoint>();
                if (hinge != null)
                    hinge.connectedBody = attachToParentRigidbody;
            }
            else
            {
                Destroy(rb);
            }
        }

        if (attachTo != null)
            transform.SetParent(attachTo, worldPositionStays: true);

        AssignLocalName();

        var rotator = GetComponent<PlacementRotator>();
        if (rotator != null) Destroy(rotator);

        var gizmo = GetComponent<TransformGizmo>();
        if (gizmo != null) Destroy(gizmo);

        Destroy(this);
    }

    void AssignLocalName()
    {
        if (string.IsNullOrEmpty(_prefabBaseName)) return;
        var peripheral = GetComponentInChildren<RobotPeripheral>();
        if (peripheral == null) return;

        var microcontroller = GetComponentInParent<Microcontroller>();
        if (microcontroller == null) return;

        int highest = 0;
        foreach (var sibling in microcontroller.GetComponentsInChildren<RobotPeripheral>())
        {
            if (sibling == peripheral) continue;
            string name = sibling.LocalName;
            if (name == null || !name.StartsWith(_prefabBaseName)) continue;
            if (int.TryParse(name.Substring(_prefabBaseName.Length), out var n) && n > highest)
                highest = n;
        }

        peripheral.LocalName = $"{_prefabBaseName}{highest + 1}";
    }

    void Cancel()
    {
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
