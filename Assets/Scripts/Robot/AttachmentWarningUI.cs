using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Periodically checks every placed Shape/Component against its own Transform parent to
// make sure it's still physically touching it -- not just structurally parented -- and
// surfaces a warning if not: a top-left on-screen list of detached part names, plus a
// temporary red tint on the detached part (and whatever's nested under it) so it's easy
// to spot in the scene too. This is a debug/validation aid, not something normal usage
// should ever trigger -- TransformGizmo's Move mode always reverts rather than leaving
// a part detached, so seeing this warning means either a bug in that system or some
// other code moved a part directly without going through it.
//
// Self-installs via RuntimeInitializeOnLoadMethod rather than needing to be manually
// added to a scene GameObject.
public class AttachmentWarningUI : MonoBehaviour
{
    const float CheckInterval = 0.5f;
    const float TouchTolerance = 0.05f;
    static readonly Color WarningTint = Color.red;

    float _timer;
    readonly List<string> _detachedNames = new();
    readonly Dictionary<Renderer, Color> _tintedRenderers = new();

    [RuntimeInitializeOnLoadMethod]
    static void Bootstrap()
    {
        var go = new GameObject("AttachmentWarningUI");
        go.AddComponent<AttachmentWarningUI>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < CheckInterval) return;
        _timer = 0f;
        RunCheck();
    }

    void RunCheck()
    {
        var stillDetached = new List<Transform>();
        _detachedNames.Clear();

        foreach (var part in FindObjectsByType<EditablePart>(FindObjectsSortMode.None))
        {
            Transform t = part.transform;
            if (t.parent == null) continue; // e.g. the robot's own root -- nothing to be attached to

            if (!IsAttachedToParent(t))
            {
                _detachedNames.Add(t.name);
                stillDetached.Add(t);
                Tint(t);
            }
        }

        // Restore anything that was tinted last check but isn't detached any more.
        foreach (var r in _tintedRenderers.Keys.ToList())
        {
            if (r == null || !stillDetached.Any(t => r.transform.IsChildOf(t)))
                Untint(r);
        }
    }

    // "Attached" means actually touching (overlapping, or resting flush with no real
    // gap) the parent's own geometry -- excluding the part's own subtree from the
    // parent's collider set, since a compound part is always a descendant of its own
    // parent and would otherwise trivially "touch" itself.
    static bool IsAttachedToParent(Transform part)
    {
        var mine = part.GetComponentsInChildren<Collider>();
        if (mine.Length == 0) return true; // nothing to check against, don't false-flag

        var parentColliders = part.parent.GetComponentsInChildren<Collider>()
            .Where(c => !c.transform.IsChildOf(part));

        foreach (var m in mine)
        {
            foreach (var o in parentColliders)
            {
                if (Physics.ComputePenetration(
                        m, m.transform.position, m.transform.rotation,
                        o, o.transform.position, o.transform.rotation,
                        out _, out _))
                    return true;

                Vector3 a = m.ClosestPoint(o.bounds.center);
                Vector3 b = o.ClosestPoint(a);
                if (Vector3.Distance(a, b) <= TouchTolerance) return true;
            }
        }
        return false;
    }

    void Tint(Transform part)
    {
        foreach (var r in part.GetComponentsInChildren<Renderer>())
        {
            if (_tintedRenderers.ContainsKey(r)) continue;
            // .material (not .sharedMaterial) clones it into a per-renderer instance on
            // first access, so this can't bleed the tint into other objects sharing the
            // same original material asset.
            _tintedRenderers[r] = r.material.color;
            r.material.color = WarningTint;
        }
    }

    void Untint(Renderer r)
    {
        if (r != null && _tintedRenderers.TryGetValue(r, out var original))
            r.material.color = original;
        _tintedRenderers.Remove(r);
    }

    void OnGUI()
    {
        if (_detachedNames.Count == 0) return;

        const int pad = 10;
        string message = "Detached from parent:\n" + string.Join("\n", _detachedNames);
        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 14,
            normal = { textColor = Color.white }
        };
        Vector2 size = style.CalcSize(new GUIContent(message));
        GUI.backgroundColor = new Color(0.6f, 0f, 0f, 0.85f);
        GUI.Box(new Rect(pad, pad, size.x + pad, size.y + pad), message, style);
    }
}
