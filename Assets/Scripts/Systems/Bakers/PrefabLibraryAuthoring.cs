using Unity.Entities;
using UnityEngine;

public class PrefabLibraryAuthoring : MonoBehaviour
{
    public GameObject[] prefabs; // assign in Inspector

    public class Baker : Baker<PrefabLibraryAuthoring>
    {
        public override void Bake(PrefabLibraryAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);
            var buffer = AddBuffer<PrefabRef>(e);
            foreach (var go in a.prefabs)
            {
                var prefabEntity = GetEntity(go, TransformUsageFlags.Dynamic);
                buffer.Add(new PrefabRef { Value = prefabEntity });
            }
        }
    }
}

public struct PrefabRef : IBufferElementData
{
    public Entity Value;
}
