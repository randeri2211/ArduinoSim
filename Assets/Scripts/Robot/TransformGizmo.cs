using System.Collections.Generic;
using UnityEngine;

// Runtime transform gizmo for a Shape or Component -- arrows on the +X/+Y/+Z faces for
// Scale (Shapes only) or Move, rings around each axis for Rotate, toggled with a key.
// Fully self-contained, no dependency on PlacementPreview state, so it works
// identically whether attached during initial placement or added later by re-selecting
// an already-placed part (see CameraMovement.HandleClick).
public class TransformGizmo : MonoBehaviour
{
    // Only one gizmo active at a time -- attaching a new one closes whatever's current.
    public static TransformGizmo Current { get; private set; }

    public KeyCode toggleModeKey = KeyCode.R;
    public KeyCode closeKey = KeyCode.Escape;

    // World units of arrow size per world unit of camera distance -- keeps the arrows a
    // constant size on screen. Tune in the Inspector if they read too big/small.
    public float handleScreenSize = 0.3f;

    enum GizmoMode { Scale, Rotate, Move }

    static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };
    static readonly Color[] AxisColors = { Color.red, Color.green, Color.blue };

    const float ArrowLength = 0.4f;
    const float ArrowShaftRadius = 0.02f;
    const float ArrowTipSize = 0.08f;
    const int RingSegments = 48;
    const float RingClickTolerance = 0.06f;
    const float ScaleSensitivity = 0.01f;
    const float MinScale = 0.05f;
    const float MoveSensitivity = 0.001f;

    GizmoMode _mode = GizmoMode.Scale;
    Vector3 _localExtents;

    // Components (Motor, sensors) don't support Scale here -- resizing a motor or
    // sensor doesn't mean anything the way resizing a Shape does -- so the mode cycle
    // skips it for them and Move is the default mode instead of Scale. Read off
    // EditablePart.IsComponent (set once at spawn, when it's unambiguous) rather than
    // searching for RobotPeripheral here -- that search would walk this object's WHOLE
    // subtree and wrongly flag a Shape as a Component just because some other Component
    // happens to be attached to it further down the hierarchy.
    bool _isComponent;

    // Each attached child's anchor point on this shape's own canonical (unscaled)
    // surface, captured ONCE when the gizmo attaches and reused for every scale drag
    // afterward, on any axis, in any order -- see ContinueDrag's Scale branch for why
    // re-deriving this fresh from the child's current (already-corrected) position each
    // frame drifts under sequential non-uniform scaling: freezing the child's world
    // size preserves its offset from the anchor in local/mesh-space, and repeatedly
    // re-normalizing that offset's direction after a non-uniform (per-axis) scale
    // change doesn't actually preserve which point on the surface it was originally
    // touching. Keeping the anchor fixed at its true original value sidesteps that
    // entirely.
    readonly Dictionary<Transform, Vector3> _anchorLocalCache = new();

    readonly GameObject[] _arrows = new GameObject[3];
    readonly GameObject[] _moveArrows = new GameObject[3];
    readonly LineRenderer[] _rings = new LineRenderer[3];

    int _draggingAxis = -1;
    Vector3 _dragStartMouse;
    Vector3 _dragStartScale;
    Vector2 _lastMouseScreen;

    // Move-drag state -- snapshotted at drag-start so a drag that ends up touching
    // nothing can be reverted wholesale, and so the whole drag is measured from one
    // fixed reference rather than accumulating per-frame (matching how Scale tracks
    // _dragStartScale rather than incrementally re-scaling each frame).
    Vector3 _dragStartPosition;
    Quaternion _dragStartRotation;
    List<Collider> _dragColliders;
    Collider _lastTouching;

    // Any Rigidbody among this part's descendants (e.g. a Motor's, physics-driven via
    // its own HingeJoint) with whether it started kinematic, so it can be restored on
    // close. Non-kinematic Rigidbodies don't follow ordinary Transform changes -- only
    // physics simulation moves them -- so without this, editing the part here would
    // leave a mounted Motor's shaft physically behind while its housing (a plain
    // Transform child, no Rigidbody of its own) follows correctly. Same fix
    // PlacementPreview already applies during initial placement, for the same reason,
    // and covers Move as well as Scale/Rotate since it's just locked for this
    // component's whole lifetime regardless of mode.
    readonly List<(Rigidbody rb, bool wasKinematic)> _lockedRigidbodies = new();

    void Awake()
    {
        if (Current != null && Current != this) Destroy(Current);
        Current = this;

        var mesh = GetComponent<MeshFilter>()?.sharedMesh;
        _localExtents = mesh != null ? mesh.bounds.extents : Vector3.one * 0.5f;
        _isComponent = GetComponent<EditablePart>()?.IsComponent ?? false;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            Vector3 childLocalPure = transform.InverseTransformPoint(child.position);
            _anchorLocalCache[child] = ClosestLocalMeshPoint(childLocalPure);
        }

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            _lockedRigidbodies.Add((rb, rb.isKinematic));
            rb.isKinematic = true;
        }

        BuildArrows();
        BuildMoveArrows();
        BuildRings();
        SetMode(_isComponent ? GizmoMode.Move : GizmoMode.Scale);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;

        foreach (var (rb, wasKinematic) in _lockedRigidbodies)
            if (rb != null) rb.isKinematic = wasKinematic;

        // Destroy(component) only removes this script, not the arrow/ring child
        // GameObjects it built -- without this they'd stay behind as orphaned children
        // every time a gizmo closes, and the next one built on top would double up.
        foreach (var arrow in _arrows)
            if (arrow != null) Destroy(arrow);
        foreach (var arrow in _moveArrows)
            if (arrow != null) Destroy(arrow);
        foreach (var ring in _rings)
            if (ring != null) Destroy(ring.gameObject);
    }

    // Closest point to localPoint on this shape's own canonical primitive surface, in
    // pure unscaled local/mesh space (i.e. as if transform.localScale were still
    // Vector3.one). Deliberately NOT using Collider.ClosestPoint -- Unity's
    // SphereCollider/CapsuleCollider don't actually support non-uniform scaling in
    // physics (they stay a true sphere/capsule, sized off roughly the largest scale
    // axis) even though the visual mesh correctly stretches into an ellipsoid, so
    // querying the collider under a non-uniform scale silently returns a point that no
    // longer matches what's on screen (this is also why BuildUI swaps those colliders
    // for a convex MeshCollider on spawn -- but that means the collider TYPE alone
    // can't tell a sphere from a capsule/cylinder here anymore, hence checking the
    // mesh name instead: Unity's built-in primitive meshes are reliably named "Sphere",
    // "Cube", etc.). Doing the math ourselves against the known canonical shape
    // sidesteps the scaling issue entirely: for a sphere, direction-from-center times
    // radius is exact in this unscaled space (the ellipsoid distortion only happens
    // once the caller re-applies the shape's real scale afterward); for a box, clamping
    // to the extents is the standard closest-point-on-AABB formula, exact for a cube.
    // Anything else (capsule, cylinder) falls back to the box formula as a reasonable
    // approximation.
    Vector3 ClosestLocalMeshPoint(Vector3 localPoint)
    {
        bool isSphere = GetComponent<MeshFilter>()?.sharedMesh?.name == "Sphere";
        if (isSphere)
        {
            float radius = _localExtents.x;
            return localPoint.sqrMagnitude > 0.0001f ? localPoint.normalized * radius : new Vector3(radius, 0f, 0f);
        }
        return new Vector3(
            Mathf.Clamp(localPoint.x, -_localExtents.x, _localExtents.x),
            Mathf.Clamp(localPoint.y, -_localExtents.y, _localExtents.y),
            Mathf.Clamp(localPoint.z, -_localExtents.z, _localExtents.z));
    }

    static Material MakeUnlitMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { color = color };
        // Handles should always read as on top, since raycasting for them is already
        // layer-isolated and unaffected by what's visually in front -- without this,
        // nearby geometry can occlude a handle the player can still click, which was
        // being worked around by manually shrinking/moving other objects to reveal it.
        if (mat.HasProperty("_ZTest"))
            mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = 4000;
        return mat;
    }

    // Shared by both BuildArrows (Scale, cube tip) and BuildMoveArrows (Move, sphere
    // tip) -- same shaft, same click-handle setup, only the tip shape and GizmoHandle
    // axis differ, so a glance at the handle shape tells the player which mode a set of
    // arrows belongs to.
    GameObject BuildAxisArrow(int axis, string namePrefix, PrimitiveType tipType)
    {
        var arrow = new GameObject($"{namePrefix}{axis}");
        // Deliberately NOT parented to the shape -- that transform's scale is what the
        // player is actively editing via this gizmo, and Transform has no "world scale"
        // setter (only the read-only lossyScale getter), so a child's rendered size
        // always inherits a non-uniformly-scaled parent's scale no matter what
        // localScale is set to. Position/rotation are tracked to the shape directly in
        // world space each frame in RepositionHandles(), and scale is driven purely by
        // camera distance so handles stay a constant size on screen regardless of the
        // shape's size or zoom.

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(arrow.transform, false);
        Destroy(shaft.GetComponent<Collider>());
        shaft.transform.localPosition = new Vector3(0f, ArrowLength * 0.5f, 0f);
        shaft.transform.localScale = new Vector3(ArrowShaftRadius, ArrowLength * 0.5f, ArrowShaftRadius);
        shaft.GetComponent<Renderer>().material = MakeUnlitMaterial(AxisColors[axis]);

        var tip = GameObject.CreatePrimitive(tipType);
        tip.name = "Tip";
        tip.transform.SetParent(arrow.transform, false);
        tip.transform.localPosition = new Vector3(0f, ArrowLength, 0f);
        tip.transform.localScale = Vector3.one * ArrowTipSize;
        tip.GetComponent<Renderer>().material = MakeUnlitMaterial(AxisColors[axis]);
        Destroy(tip.GetComponent<Collider>());

        int gizmoLayer = LayerMask.NameToLayer("Gizmo");
        if (gizmoLayer >= 0)
        {
            arrow.layer = gizmoLayer;
            shaft.layer = gizmoLayer;
            tip.layer = gizmoLayer;
        }

        var sphere = tip.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 1.5f; // local space -- scales with the tip's own small transform

        var handle = tip.AddComponent<GizmoHandle>();
        handle.axisIndex = axis;

        return arrow;
    }

    void BuildArrows()
    {
        for (int i = 0; i < 3; i++)
            _arrows[i] = BuildAxisArrow(i, "ScaleArrow", PrimitiveType.Cube);
    }

    void BuildMoveArrows()
    {
        for (int i = 0; i < 3; i++)
            _moveArrows[i] = BuildAxisArrow(i, "MoveArrow", PrimitiveType.Sphere);
    }

    void BuildRings()
    {
        for (int i = 0; i < 3; i++)
        {
            var ringObj = new GameObject($"RotateRing{i}");
            // Not parented to the shape either (see BuildAxisArrow) -- a non-uniformly
            // scaled shape would otherwise stretch these circles into ellipses.
            // Position/rotation are tracked to the shape each frame in
            // RepositionHandles(), with a single shared "bounding sphere" radius so all
            // three stay true circles regardless of the shape's own scale.

            var lr = ringObj.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.positionCount = RingSegments;
            lr.widthMultiplier = 0.02f;
            lr.material = MakeUnlitMaterial(AxisColors[i]);
            lr.startColor = lr.endColor = AxisColors[i];

            _rings[i] = lr;
        }
    }

    void SetMode(GizmoMode mode)
    {
        _mode = mode;
        foreach (var arrow in _arrows)
            if (arrow != null) arrow.SetActive(mode == GizmoMode.Scale);
        foreach (var arrow in _moveArrows)
            if (arrow != null) arrow.SetActive(mode == GizmoMode.Move);
        foreach (var ring in _rings)
            if (ring != null) ring.gameObject.SetActive(mode == GizmoMode.Rotate);
    }

    // Cycles Scale -> Rotate -> Move -> Scale for a Shape; Components skip Scale
    // entirely (see _isComponent) and just toggle between Rotate and Move.
    GizmoMode NextMode(GizmoMode mode)
    {
        if (_isComponent)
            return mode == GizmoMode.Rotate ? GizmoMode.Move : GizmoMode.Rotate;
        return mode switch
        {
            GizmoMode.Scale => GizmoMode.Rotate,
            GizmoMode.Rotate => GizmoMode.Move,
            _ => GizmoMode.Scale,
        };
    }

    Vector3 CurrentExtents() => new Vector3(
        _localExtents.x * transform.localScale.x,
        _localExtents.y * transform.localScale.y,
        _localExtents.z * transform.localScale.z);

    float CurrentRingRadius(Vector3 extents) => Mathf.Max(extents.x, extents.y, extents.z) * 1.3f;

    void RepositionHandles()
    {
        // Arrows aren't parented to the shape (see BuildAxisArrow), so position/rotation
        // are tracked to it directly in world space, and scale comes purely from camera
        // distance -- constant on-screen size, independent of the shape's own scale.
        var cam = Camera.main;
        for (int i = 0; i < 3; i++)
        {
            Vector3 worldPos = transform.TransformPoint(Axes[i] * _localExtents[i]);
            Quaternion worldRot = transform.rotation * Quaternion.FromToRotation(Vector3.up, Axes[i]);
            float dist = cam != null ? Vector3.Distance(cam.transform.position, worldPos) : 1f;
            Vector3 handleScale = Vector3.one * (dist * handleScreenSize);

            if (_arrows[i] != null)
            {
                _arrows[i].transform.SetPositionAndRotation(worldPos, worldRot);
                _arrows[i].transform.localScale = handleScale;
            }
            if (_moveArrows[i] != null)
            {
                _moveArrows[i].transform.SetPositionAndRotation(worldPos, worldRot);
                _moveArrows[i].transform.localScale = handleScale;
            }
        }

        // Rings still grow with the object (unlike the arrows' constant screen size),
        // but as true circles sized to a single shared bounding-sphere radius -- not
        // stretched per-axis by a non-uniformly scaled shape.
        float ringRadius = CurrentRingRadius(CurrentExtents());
        for (int i = 0; i < 3; i++)
        {
            if (_rings[i] == null) continue;
            _rings[i].transform.SetPositionAndRotation(transform.position, transform.rotation);
            for (int s = 0; s < RingSegments; s++)
            {
                float t = (float)s / RingSegments * Mathf.PI * 2f;
                _rings[i].SetPosition(s, AxisPlanePoint(i, t) * ringRadius);
            }
        }
    }

    static Vector3 AxisPlanePoint(int axis, float angle)
    {
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        return axis switch
        {
            0 => new Vector3(0f, c, s),  // ring around X, lies in local Y-Z plane
            1 => new Vector3(c, 0f, s),  // ring around Y, lies in local X-Z plane
            _ => new Vector3(c, s, 0f),  // ring around Z, lies in local X-Y plane
        };
    }

    void Update()
    {
        RepositionHandles();

        if (_draggingAxis < 0 && Input.GetKeyDown(toggleModeKey))
            SetMode(NextMode(_mode));

        if (Input.GetKeyDown(closeKey))
        {
            Destroy(this);
            return;
        }

        if (_draggingAxis < 0)
        {
            if (Input.GetMouseButtonDown(0))
                TryBeginDrag();
        }
        else if (Input.GetMouseButton(0))
        {
            ContinueDrag();
        }
        else
        {
            if (_mode == GizmoMode.Move) EndMoveDrag();
            _draggingAxis = -1;
        }
    }

    void TryBeginDrag()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (_mode == GizmoMode.Rotate)
        {
            float ringRadius = CurrentRingRadius(CurrentExtents());
            for (int i = 0; i < 3; i++)
            {
                if (PlaneAngle(i, ray, out _, out float radiusHere) &&
                    Mathf.Abs(radiusHere - ringRadius) <= RingClickTolerance)
                {
                    _draggingAxis = i;
                    _lastMouseScreen = Input.mousePosition;
                    break;
                }
            }
            return;
        }

        // Scale and Move both grab an arrow-tip handle on the Gizmo layer.
        int gizmoLayer = LayerMask.NameToLayer("Gizmo");
        if (gizmoLayer < 0) return;
        if (!Physics.Raycast(ray, out var hit, 100f, 1 << gizmoLayer, QueryTriggerInteraction.Collide)) return;
        var handle = hit.collider.GetComponent<GizmoHandle>();
        if (handle == null) return;

        _draggingAxis = handle.axisIndex;
        _dragStartMouse = Input.mousePosition;
        if (_mode == GizmoMode.Scale)
        {
            _dragStartScale = transform.localScale;
        }
        else
        {
            _dragStartPosition = transform.position;
            _dragStartRotation = transform.rotation;
            _dragColliders = new List<Collider>(GetComponentsInChildren<Collider>());
            _lastTouching = null;
        }
    }

    void ContinueDrag()
    {
        var cam = Camera.main;
        if (cam == null) return;

        if (_mode == GizmoMode.Scale)
        {
            Vector3 worldAxis = transform.TransformDirection(Axes[_draggingAxis]);
            Vector3 screenCenter = cam.WorldToScreenPoint(transform.position);
            Vector3 screenAxisTip = cam.WorldToScreenPoint(transform.position + worldAxis);
            Vector2 screenDir = ((Vector2)screenAxisTip - (Vector2)screenCenter).normalized;

            Vector2 mouseDelta = (Vector2)Input.mousePosition - (Vector2)_dragStartMouse;
            float amount = Vector2.Dot(mouseDelta, screenDir) * ScaleSensitivity;

            Vector3 newScale = _dragStartScale;
            newScale[_draggingAxis] = Mathf.Max(MinScale, _dragStartScale[_draggingAxis] + amount);

            // Scaling this shape also scales anything physically attached to it (Unity's
            // normal parent-child inheritance) -- everything attached here is always a
            // DIRECT child (that's how PlacementPreview.Confirm() parents it: a Motor's
            // root, a Sensor's root, or another Shape), so protecting direct children only
            // -- rather than hunting for specific component types -- correctly covers the
            // whole attached assembly even when the interesting script lives deeper (e.g.
            // a Motor's own script is on MotorShaft, a grandchild of the MotorBody root
            // that's actually parented here) and works for any future component type
            // automatically. Each child's own lossyScale before/after already accounts for
            // its local scale plus everything above it, so restoring just the direct
            // child's world scale correctly preserves everything further down its subtree
            // too, without needing to touch those descendants separately.
            //
            // Position ALSO needs correcting here, alongside size -- these aren't
            // independent. Unity's default proportional position scaling is only exact
            // when the child's own size scales right along with it; a child's original
            // localPosition bakes in both "where on my surface it's anchored" AND "how
            // far out the child's own half-size pushes its center past that point", and
            // scaling the whole shape multiplies both parts together. Once the child's
            // size is frozen (above), only the first part should still track the scale
            // change -- naively leaving position alone (or shifting everything by a flat
            // per-axis amount, as an earlier version of this fix did) drifts the child
            // away from the surface, because not every attachment point sits at the
            // shape's extent along the dragged axis -- only true for something flush
            // against a face perpendicular to that axis (e.g. cube-on-cube). A cube
            // attached to a sphere is anchored at whatever point on the sphere it
            // actually touches, which generally is NOT at the pole along whichever axis
            // gets dragged, so scaling by the full extent overshoots.
            //
            // The general fix: find the point on THIS shape's own canonical surface
            // (sphere/box, see ClosestLocalMeshPoint) the child is anchored to, in pure
            // local/mesh-space -- captured once at attach-time and cached (see
            // _anchorLocalCache), not re-derived from the child's current position each
            // frame -- and see where that same fixed point ends up after the scale
            // change. Exact for a sphere or box at any attachment point, not just
            // cube-face centers. Then rigidly carry the child along by that same delta,
            // since its offset from its anchor point isn't changing (its size is
            // frozen).
            var attachedChildren = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
                attachedChildren.Add(transform.GetChild(i));

            var worldScalesBefore = new Vector3[attachedChildren.Count];
            var worldPositionsBefore = new Vector3[attachedChildren.Count];
            var localAnchors = new Vector3[attachedChildren.Count];
            var anchorWorldBefore = new Vector3[attachedChildren.Count];
            for (int p = 0; p < attachedChildren.Count; p++)
            {
                var t = attachedChildren[p];
                worldScalesBefore[p] = t.lossyScale;
                worldPositionsBefore[p] = t.position;

                // Reuse the anchor captured once at attach-time (see
                // _anchorLocalCache) rather than re-deriving it from the child's
                // current position -- a child newly parented here mid-session (not
                // seen in Awake) falls back to computing it fresh, same formula.
                if (!_anchorLocalCache.TryGetValue(t, out Vector3 anchorLocal))
                {
                    Vector3 childLocalPure = transform.InverseTransformPoint(t.position);
                    anchorLocal = ClosestLocalMeshPoint(childLocalPure);
                    _anchorLocalCache[t] = anchorLocal;
                }
                localAnchors[p] = anchorLocal;
                anchorWorldBefore[p] = transform.TransformPoint(localAnchors[p]);
            }

            transform.localScale = newScale;

            for (int p = 0; p < attachedChildren.Count; p++)
            {
                var t = attachedChildren[p];
                Vector3 lossyNow = t.lossyScale;
                Vector3 correction = new Vector3(
                    lossyNow.x != 0f ? worldScalesBefore[p].x / lossyNow.x : 1f,
                    lossyNow.y != 0f ? worldScalesBefore[p].y / lossyNow.y : 1f,
                    lossyNow.z != 0f ? worldScalesBefore[p].z / lossyNow.z : 1f);
                t.localScale = Vector3.Scale(t.localScale, correction);

                // anchorWorldAfter differs from anchorWorldBefore only because
                // transform.localScale changed above -- TransformPoint re-reads the
                // shape's current scale each time it's called, so the same pure local
                // point now lands wherever the (possibly non-uniformly stretched) mesh
                // surface actually moved it to.
                Vector3 anchorWorldAfter = transform.TransformPoint(localAnchors[p]);
                t.position = worldPositionsBefore[p] + (anchorWorldAfter - anchorWorldBefore[p]);
            }
        }
        else if (_mode == GizmoMode.Move)
        {
            Vector3 worldAxis = transform.TransformDirection(Axes[_draggingAxis]);
            Vector3 screenCenter = cam.WorldToScreenPoint(_dragStartPosition);
            Vector3 screenAxisTip = cam.WorldToScreenPoint(_dragStartPosition + worldAxis);
            Vector2 screenDir = ((Vector2)screenAxisTip - (Vector2)screenCenter).normalized;

            Vector2 mouseDelta = (Vector2)Input.mousePosition - (Vector2)_dragStartMouse;
            float pixelAmount = Vector2.Dot(mouseDelta, screenDir);

            // Scaled by distance-to-camera, like the handles' own on-screen sizing, so
            // a given mouse movement drags the part by a consistent WORLD distance
            // regardless of zoom level -- without this, the same drag would move a part
            // much further when zoomed out than when zoomed in.
            float distScale = Vector3.Distance(cam.transform.position, _dragStartPosition);
            float amount = pixelAmount * MoveSensitivity * distScale;

            transform.position = _dragStartPosition + worldAxis * amount;

            // Push back out of anything now overlapped (so the part reads as sliding
            // along a surface rather than clipping through it) and track what it's
            // resting against -- actual re-parenting only happens once the drag ends
            // (see EndMoveDrag), but a Component keeps re-orienting flush to whatever
            // it's currently touching the whole time it's dragged, the same continuous
            // snap PlacementPreview does during initial placement.
            _lastTouching = AttachmentUtil.ResolveOverlap(transform, _dragColliders, out Vector3 surfaceNormal);
            if (_isComponent && _lastTouching != null)
                transform.rotation = Quaternion.FromToRotation(Vector3.down, surfaceNormal);
        }
        else
        {
            // Per-frame screen-space delta rather than recomputing an absolute angle
            // from the ray/plane intersection each frame -- the latter gets jittery
            // near shallow viewing angles (small mouse moves swing the intersection
            // point a lot). Arc length / radius converts the tangential pixel delta
            // into degrees, self-normalizing for camera distance/zoom.
            //
            // The radius/tangent are measured from the actual ring-plane hit point
            // (not just raw mouse-to-object-center screen distance) so a ring that's
            // heavily foreshortened from the current camera angle (e.g. a horizontal
            // ring viewed from above) gets its own correctly-smaller effective radius,
            // instead of every ring sharing one generic distance and some ending up
            // over-sensitive.
            Vector2 mouseScreen = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(mouseScreen);
            if (PlanePoint(_draggingAxis, ray, out Vector3 worldHit))
            {
                Vector3 worldAxis = transform.TransformDirection(Axes[_draggingAxis]);
                Vector3 radiusVec = worldHit - transform.position;

                Vector2 hitScreen = cam.WorldToScreenPoint(worldHit);
                Vector2 centerScreen = cam.WorldToScreenPoint(transform.position);
                float screenRadius = (hitScreen - centerScreen).magnitude;

                if (screenRadius > 1f && radiusVec.sqrMagnitude > 0.0001f)
                {
                    // The screen-projected direction a point at the current hit location
                    // would move under a small POSITIVE rotation -- Cross(axis, radius)
                    // is the actual angular-velocity tangent, matching Quaternion.AngleAxis
                    // exactly for any axis. Deriving it fresh from real world geometry each
                    // frame (rather than a guessed screen-space rule + empirical per-axis
                    // sign flips) makes it correct for every axis and every camera angle
                    // automatically, including flipping correctly when viewing a ring from
                    // its far side.
                    Vector3 worldTangentDir = Vector3.Cross(worldAxis, radiusVec).normalized;
                    Vector2 tangentTipScreen = cam.WorldToScreenPoint(worldHit + worldTangentDir * 0.01f);
                    Vector2 screenTangent = (tangentTipScreen - hitScreen).normalized;

                    Vector2 mouseDelta = mouseScreen - _lastMouseScreen;
                    float tangentialPixels = Vector2.Dot(mouseDelta, screenTangent);
                    float deltaDegrees = (tangentialPixels / screenRadius) * Mathf.Rad2Deg;
                    transform.Rotate(worldAxis, deltaDegrees, Space.World);
                }
            }
            _lastMouseScreen = mouseScreen;
        }
    }

    // Finalizes a Move drag on mouse-release: re-parents to whatever ended up touched
    // (even if that's the same part it started on) so the object stays a physically
    // attached member of the robot rather than a loose, unparented sibling; reverts the
    // whole drag if it ends up touching nothing, rather than leaving a physically
    // attached part floating unattached in empty space.
    void EndMoveDrag()
    {
        if (_lastTouching != null)
        {
            Transform attachTo = _lastTouching.transform;
            transform.SetParent(attachTo, worldPositionStays: true);

            // A Motor keeps its own live Rigidbody + HingeJoint after placement (see
            // PlacementPreview.Confirm) rather than being made a plain rigid child --
            // moving it to a new attach point needs that joint re-wired to the new
            // mount's Rigidbody, the same way Confirm() wires it up initially, or it'd
            // still be physically anchored to wherever it USED to be mounted.
            var hinge = GetComponentInChildren<HingeJoint>();
            if (hinge != null)
                hinge.connectedBody = attachTo.GetComponentInParent<Rigidbody>();
        }
        else
        {
            transform.SetPositionAndRotation(_dragStartPosition, _dragStartRotation);
        }
        _dragColliders = null;
        _lastTouching = null;
    }

    // Casts ray against the plane through this shape's center, perpendicular to the
    // given local axis, returning the world-space intersection point.
    bool PlanePoint(int axis, Ray ray, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        Vector3 worldAxis = transform.TransformDirection(Axes[axis]).normalized;
        var plane = new Plane(worldAxis, transform.position);
        if (!plane.Raycast(ray, out float enter)) return false;
        worldPoint = ray.GetPoint(enter);
        return true;
    }

    // Only used at click-time now (to check whether the click landed near the ring's
    // radius) -- ContinueDrag uses PlanePoint directly for the actual drag tracking.
    bool PlaneAngle(int axis, Ray ray, out float angle, out float radiusHere)
    {
        angle = 0f;
        radiusHere = 0f;
        if (!PlanePoint(axis, ray, out Vector3 worldPoint)) return false;

        Vector3 worldOffset = worldPoint - transform.position;
        radiusHere = worldOffset.magnitude;

        Vector3 local = transform.InverseTransformDirection(worldOffset);
        angle = axis switch
        {
            0 => Mathf.Atan2(local.z, local.y),
            1 => Mathf.Atan2(local.x, local.z),
            _ => Mathf.Atan2(local.y, local.x),
        };
        return true;
    }
}
