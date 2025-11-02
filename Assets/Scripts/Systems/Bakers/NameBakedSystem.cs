using Unity.Entities;
using UnityEngine;

// Runs during baking; adds NameTag to anything with a Transform.
public class GlobalNameBaker : Baker<Transform>
{
    public override void Bake(Transform authoring)
    {
        var e = GetEntity(TransformUsageFlags.None);
        AddComponent(e, new Name { Value = authoring.name });
    }
}
