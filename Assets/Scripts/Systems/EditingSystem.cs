using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct EditingSystem : ISystem
{
    private EntityQuery _queueQ;
    private EntityQuery _libQ;

    public void OnCreate(ref SystemState state)
    {
        _queueQ = state.GetEntityQuery(ComponentType.ReadOnly<EditQueueTag>(), ComponentType.ReadWrite<EditRequest>());
        _libQ   = state.GetEntityQuery(ComponentType.ReadOnly<PrefabRef>());
        state.RequireForUpdate(_queueQ);
        state.RequireForUpdate(_libQ);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!Parameters.EDITING) return;

        var em = state.EntityManager;

        var queueEntity = _queueQ.GetSingletonEntity();
        var edits = em.GetBuffer<EditRequest>(queueEntity);

        // Read prefab library once
        var libEntity = _libQ.GetSingletonEntity();
        var lib = em.GetBuffer<PrefabRef>(libEntity);

        // Execute each request (simple, one-by-one)
        for (int i = 0; i < edits.Length; i++)
        {
            var r = edits[i];
            switch (r.Op)
            {
                case EditOp.Spawn:
                {
                    if ((uint)r.PrefabIndex >= (uint)lib.Length) break;
                    var prefab = lib[r.PrefabIndex].Value;
                    var e = em.Instantiate(prefab);
                    // Ensure it has LocalTransform (prefab should already have it if baked as Dynamic)
                    if (!em.HasComponent<LocalTransform>(e))
                        em.AddComponentData(e, LocalTransform.Identity);

                    var lt = em.GetComponentData<LocalTransform>(e);
                    lt.Position = r.P;
                    em.SetComponentData(e, lt);
                    break;
                }

                case EditOp.Move:
                {
                    if (!em.Exists(r.Target) || !em.HasComponent<LocalTransform>(r.Target)) break;
                    var lt = em.GetComponentData<LocalTransform>(r.Target);
                    lt.Position = r.P;
                    em.SetComponentData(r.Target, lt);
                    break;
                }

                case EditOp.Rotate:
                {
                    if (!em.Exists(r.Target) || !em.HasComponent<LocalTransform>(r.Target)) break;
                    var lt = em.GetComponentData<LocalTransform>(r.Target);
                    // Rotate around up axis by degrees in r.F (replace with your axis)
                    lt.Rotation = math.mul(lt.Rotation, quaternion.RotateY(math.radians(r.F)));
                    em.SetComponentData(r.Target, lt);
                    break;
                }

                case EditOp.ScaleUniform:
                {
                    if (!em.Exists(r.Target)) break;
                    // LocalTransform supports **uniform** only:
                    if (em.HasComponent<LocalTransform>(r.Target))
                    {
                        var lt = em.GetComponentData<LocalTransform>(r.Target);
                        lt.Scale = math.max(0.0001f, r.F);
                        em.SetComponentData(r.Target, lt);
                    }
                    // If entity has NonUniformScale too, you probably want to remove it here.
                    break;
                }

                case EditOp.ScaleXYZ:
                {
                    // if (!em.Exists(r.Target)) break;
                    // // Non-uniform scale needs the NonUniformScale component (Unity.Transforms)
                    // if (!em.HasComponent<NonUniformScale>(r.Target))
                    //     em.AddComponent<NonUniformScale>(r.Target);
                    // em.SetComponentData(r.Target, new NonUniformScale { Value = r.P });
                    break;
                }

                case EditOp.SetParent:
                {
                    if (!em.Exists(r.Target)) break;
                    // Add/Set Parent so r.Target becomes child of r.Parent
                    if (!em.HasComponent<Parent>(r.Target))
                        em.AddComponent<Parent>(r.Target);
                    em.SetComponentData(r.Target, new Parent { Value = r.Parent });
                    // Optional: also add LocalTransform if missing (child space pose)
                    if (!em.HasComponent<LocalTransform>(r.Target))
                        em.AddComponentData(r.Target, LocalTransform.Identity);
                    break;
                }

                case EditOp.ClearParent:
                {
                    if (!em.Exists(r.Target)) break;
                    if (em.HasComponent<Parent>(r.Target))
                        em.RemoveComponent<Parent>(r.Target);
                    break;
                }
            }
        }

        // Clear processed edits
        edits.Clear();
    }
}
