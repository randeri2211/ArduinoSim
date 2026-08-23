using System.Collections.Generic;
using UnityEngine;

// Shared touch/overlap-resolution helper for anything that needs to reposition a
// physically-attached part and figure out what it ends up touching, one call at a time
// -- currently just TransformGizmo's Move mode. PlacementPreview needs the same kind of
// query but keeps its own persistent per-frame version (a running _touching set it
// updates incrementally) since it has to track things like which collider is "closest"
// across many frames of continuous movement; this is the simpler one-shot form of the
// same idea, used by callers that just want "push me back out, and tell me what I
// ended up against" for a single position.
public static class AttachmentUtil
{
    // Excludes the Gizmo layer (see TransformGizmo) so a part's own scale/move handles,
    // sitting right at its faces, never register as something it's touching.
    // LayerMask.NameToLayer returns -1 until that layer exists, and 1 << -1 is 0, so
    // this is just ~0 (everything) until the layer's actually been added.
    public static int TouchLayerMask()
    {
        int gizmoLayer = LayerMask.NameToLayer("Gizmo");
        return gizmoLayer >= 0 ? ~(1 << gizmoLayer) : ~0;
    }

    // How close (world units) counts as "touching" when there's no actual geometric
    // overlap to detect -- see the ComputePenetration fallback below.
    const float TouchTolerance = 0.05f;

    // Finds whatever myColliders is currently overlapping or resting against
    // (excluding anything in myColliders itself, e.g. a compound part's own pieces
    // touching each other), pushes selfTransform back out of any actual overlap found,
    // and returns whichever touched collider ends up closest -- or null if nothing's
    // touched. surfaceNormal is the push-out direction (or, for a non-overlapping but
    // close contact, the direction between the two closest points) for that closest
    // collider, doubling as an approximate surface normal (the same trick
    // PlacementPreview uses for Components' flush-orientation).
    public static Collider ResolveOverlap(Transform selfTransform, IReadOnlyList<Collider> myColliders, out Vector3 surfaceNormal)
    {
        surfaceNormal = Vector3.down;

        Collider closest = null;
        float bestDist = float.MaxValue;
        int layerMask = TouchLayerMask();

        foreach (var mine in myColliders)
        {
            var bounds = mine.bounds;
            float radius = bounds.extents.magnitude + TouchTolerance;
            foreach (var candidate in Physics.OverlapSphere(bounds.center, radius, layerMask, QueryTriggerInteraction.Collide))
            {
                if (candidate == null || Contains(myColliders, candidate)) continue;

                if (Physics.ComputePenetration(
                        mine, mine.transform.position, mine.transform.rotation,
                        candidate, candidate.transform.position, candidate.transform.rotation,
                        out var direction, out var distance))
                {
                    // Genuinely overlapping -- push straight back out so the part
                    // doesn't visually clip through what it's touching, and treat this
                    // as the strongest possible signal (effectively zero distance).
                    selfTransform.position += direction * distance;
                    if (bestDist > 0f) { bestDist = 0f; closest = candidate; surfaceNormal = direction; }
                    continue;
                }

                // Not actually overlapping -- a part dropped in a single static spot
                // (unlike Placement, which continuously drifts forward and so is
                // always drifting slightly into things) often lands exactly flush
                // against a surface with zero interpenetration, which ComputePenetration
                // alone wouldn't catch. Fall back to plain proximity between the two
                // closest points instead.
                Vector3 mineClosest = mine.ClosestPoint(candidate.bounds.center);
                Vector3 otherClosest = candidate.ClosestPoint(mineClosest);
                float dist = Vector3.Distance(mineClosest, otherClosest);
                if (dist <= TouchTolerance && dist < bestDist)
                {
                    bestDist = dist;
                    closest = candidate;
                    Vector3 diff = mineClosest - otherClosest;
                    surfaceNormal = diff.sqrMagnitude > 0.0001f ? diff.normalized : Vector3.down;
                }
            }
        }
        return closest;
    }

    static bool Contains(IReadOnlyList<Collider> list, Collider c)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == c) return true;
        return false;
    }
}
