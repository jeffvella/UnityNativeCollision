using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Vella.Common;

namespace Vella.UnityNativeHull
{

    public struct BatchCollisionInput
    {
        public int Id;
        public RigidTransform Transform;
        public NativeHull Hull;
    }

    public struct BatchCollisionResult
    {
        public BatchCollisionInput A;
        public BatchCollisionInput B;
    }

    public static class HullOperations
    {
        [BurstCompile]
        public struct IsColliding : IBurstFunction<RigidTransform, NativeHull, RigidTransform, NativeHull, bool>
        {

            public bool Execute(RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
            {
                return HullCollision.IsColliding(t1, hull1, t2, hull2);
            }

            public static bool Invoke(RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
            {
                return BurstFunction<IsColliding, RigidTransform, NativeHull, RigidTransform, NativeHull, bool>.Run(Instance, t1, hull1, t2, hull2);
            }

            public static IsColliding Instance { get; } = new IsColliding();
        }

        [BurstCompile]
        public struct ContainsPoint : IBurstFunction<RigidTransform, NativeHull, float3, bool>
        {
            public bool Execute(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return HullCollision.Contains(t1, hull1, point);
            }

            public static bool Invoke(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return BurstFunction<ContainsPoint, RigidTransform, NativeHull, float3, bool>.Run(Instance, t1, hull1, point);
            }

            public static ContainsPoint Instance { get; } = new ContainsPoint();
        }

        [BurstCompile]
        public struct ClosestPoint : IBurstFunction<RigidTransform, NativeHull, float3, float3>
        {
            public float3 Execute(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return HullCollision.ClosestPoint(t1, hull1, point);
            }

            public static float3 Invoke(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return BurstFunction<ClosestPoint, RigidTransform, NativeHull, float3, float3>.Run(Instance, t1, hull1, point);
            }

            public static ClosestPoint Instance { get; } = new ClosestPoint();
        }

        [BurstCompile]
        public struct TryGetContact : IBurstRefAction<NativeManifold, RigidTransform, NativeHull, RigidTransform, NativeHull>
        {
            public void Execute(ref NativeManifold manifold, RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
            {
                HullIntersection.NativeHullHullContact(ref manifold, t1, hull1, t2, hull2);
            }

            public static bool Invoke(out NativeManifold result, RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
            {
                // Burst Jobs can only allocate as 'temp'
                result = new NativeManifold(Allocator.Persistent); 

                BurstRefAction<TryGetContact, NativeManifold, RigidTransform, NativeHull, RigidTransform, NativeHull>.Run(Instance, ref result, t1, hull1, t2, hull2);
                return result.Length > 0;
            }

            public static TryGetContact Instance { get; } = new TryGetContact();
        }

        [BurstCompile]
        public struct CollisionBatch : IBurstFunction<NativeArray<BatchCollisionInput>, NativeList<BatchCollisionResult>, bool>
        {
            public bool Execute(NativeArray<BatchCollisionInput> hulls, NativeList<BatchCollisionResult> results)
            {
                var isCollision = false;
                for (int i = 0; i < hulls.Length; ++i)
                {
                    for (int j = i + 1; j < hulls.Length; j++)
                    {
                        var a = hulls[i];
                        var b = hulls[j];

                        if (HullCollision.IsColliding(a.Transform, a.Hull, b.Transform, b.Hull))
                        {
                            isCollision = true;
                            results.Add(new BatchCollisionResult
                            {
                                A = a,
                                B = b,
                            });
                        }
                    }
                }
                return isCollision;
            }

            public static bool Invoke(NativeArray<BatchCollisionInput> hulls, NativeList<BatchCollisionResult> results)
            {
                return BurstFunction<CollisionBatch, NativeArray<BatchCollisionInput>, NativeList<BatchCollisionResult>, bool>.Run(Instance, hulls, results);
            }

            public static CollisionBatch Instance { get; } = new CollisionBatch();
        }

    }
}