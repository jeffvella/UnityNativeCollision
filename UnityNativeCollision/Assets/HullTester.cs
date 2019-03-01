using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using System;
using Unity.Collections;
using Vella.Common;
using Vella.UnityNativeHull;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HullTester : MonoBehaviour
{
    public List<Transform> Transforms;

    public DebugHullFlags HullDrawingOptions = DebugHullFlags.Outline;

    [Header("Visualizations")]
    public bool DrawContact;
    public bool DrawIntersection;
    public bool DrawClosestFace;

    [Header("Console Logging")]
    public bool LogCollisions;
    public bool LogClosestPoint;

    private Dictionary<int, (Transform Transform, NativeHull Hull)> Hulls;

    unsafe void Update()
    {
        HandleTransformChanged();

        HandleHullCollisions();
    }

    private void HandleHullCollisions()
    {
        for (int i = 0; i < Transforms.Count; ++i)
        {
            var tA = Transforms[i];
            if (tA == null)
                continue;

            var hullA = Hulls[tA.GetInstanceID()].Hull;
            var transformA = new RigidTransform(tA.rotation, tA.position);

            HullDrawingUtility.DrawDebugHull(hullA, transformA, HullDrawingOptions);

            if (LogClosestPoint)
            {
                var sw3 = System.Diagnostics.Stopwatch.StartNew();
                var result3 = HullCollision.ClosestPoint(transformA, hullA, 0);
                sw3.Stop();

                var sw4 = System.Diagnostics.Stopwatch.StartNew();
                var result4 = HullBurstCollision.ClosestPoint.Invoke(transformA, hullA, 0);
                sw4.Stop();

                DebugDrawer.DrawSphere(result4, 0.1f, Color.blue);
                DebugDrawer.DrawLine(result4, Vector3.zero, Color.blue);
                Debug.Log($"ClosestPoint between '{tA.name}' and world zero took: {sw3.Elapsed.TotalMilliseconds:N4}ms (Normal), {sw4.Elapsed.TotalMilliseconds:N4}ms (Burst)");
            }

            for (int j = i + 1; j < Transforms.Count; j++)
            {
                var tB = Transforms[j];
                if (tB == null)
                    continue;

                if (!tA.hasChanged && !tB.hasChanged)
                    continue;
                
                var hullB = Hulls[tB.GetInstanceID()].Hull;
                var transformB = new RigidTransform(tB.rotation, tB.position);
                HullDrawingUtility.DrawDebugHull(hullB, transformB, HullDrawingOptions);

                DrawHullCollision(transformA, hullA, transformB, hullB);

                if (LogCollisions)
                {
                    var sw1 = System.Diagnostics.Stopwatch.StartNew();
                    var result1 = HullCollision.IsCollision(transformA, hullA, transformB, hullB);
                    sw1.Stop();

                    var sw2 = System.Diagnostics.Stopwatch.StartNew();
                    var result2 = HullBurstCollision.IsCollision.Invoke(transformA, hullA, transformB, hullB);
                    sw2.Stop();

                    Debug.Assert(result1 == result2);

                    Debug.Log($"Collisions between '{tA.name}'/'{tB.name}' took: {sw1.Elapsed.TotalMilliseconds:N4}ms (Normal), {sw2.Elapsed.TotalMilliseconds:N4}ms (Burst)");
                }
            }
        }

        if(LogCollisions)
        {
            TestBatchCollision();
        }
    }

    private void TestBatchCollision()
    {
        var batchInput = Hulls.Select(t => new BatchCollisionInput
        {
            Id = t.Key,
            Transform = new RigidTransform(t.Value.Transform.rotation, t.Value.Transform.position),
            Hull = t.Value.Hull,

        }).ToArray();

        using (var hulls = new NativeArray<BatchCollisionInput>(batchInput, Allocator.TempJob))
        using (var results = new NativeList<BatchCollisionResult>(batchInput.Length, Allocator.TempJob))
        {
            var sw3 = System.Diagnostics.Stopwatch.StartNew();
            var collisions = HullBurstCollision.CollisionBatch.Invoke(hulls, results);
            sw3.Stop();

            Debug.Log($"Batch Collisions took {sw3.Elapsed.TotalMilliseconds:N4}ms ({results.Length} collisions from {hulls.Length} hulls)");

            if (collisions)
            {
                foreach (var result in results.AsArray())
                {
                    Debug.Log($" > {Hulls[result.A.Id].Transform.gameObject.name} collided with {Hulls[result.B.Id].Transform.gameObject.name}");
                }
            }
        }
    }

    public void DrawHullCollision(RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
    {

        var collision = HullCollision.GetDebugCollisionInfo(t1, hull1, t2, hull2);
        if (collision.IsColliding)
        {
            if (DrawIntersection) // Visualize all faces of the intersection
            {
                NativeIntersection.DrawNativeHullHullIntersection(t1, hull1, t2, hull2);              
            }

            if (DrawContact)  // Visualize the minimal contact calcluation for physics
            {
                var result = new NativeManifold(Allocator.Persistent);
                if (NativeIntersection.NativeHullHullContact(ref result, t1, hull1, t2, hull2))
                {
                    // Do something with manifold

                    HullDrawingUtility.DebugDrawManifold(result);

                    for (int i = 0; i < result.Points.Length; i++)
                    {
                        var point = result.Points[i];
                        DebugDrawer.DrawSphere(point.Position, 0.02f);
                        DebugDrawer.DrawArrow(point.Position, result.Normal * 0.2f);
                        DebugDrawer.DrawLabel(point.Position, $"{point.Distance:N2}");

                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge1, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge1, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge2, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge2, t1, hull1);

                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge1, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge1, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge2, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge2, t2, hull2);

                        DebugDrawer.DrawDottedLine(point.Position, point.Position - math.normalize(-result.Normal) * point.Distance);
                    }              
                    result.Dispose();
                }
            }




            //if (NativeIntersection.NativeHullHullContact(out NativeManifold result, t1, hull1, t2, hull2))
            //{
            //    // Do something with manifold

            //    result.Dispose();
            //}

            DebugDrawer.DrawSphere(t1.pos, 0.1f, UnityColors.GhostDodgerBlue);
            DebugDrawer.DrawSphere(t2.pos, 0.1f, UnityColors.GhostDodgerBlue);
        }

        if(DrawClosestFace)
        {
            var color1 = collision.Face1.Distance > 0 ? UnityColors.Red.ToOpacity(0.3f) : UnityColors.Yellow.ToOpacity(0.3f);
            HullDrawingUtility.DrawFaceWithOutline(collision.Face1.Index, t1, hull1, color1, UnityColors.Black);

            var color2 = collision.Face2.Distance > 0 ? UnityColors.Red.ToOpacity(0.3f) : UnityColors.Yellow.ToOpacity(0.3f);
            HullDrawingUtility.DrawFaceWithOutline(collision.Face2.Index, t2, hull2, color2, UnityColors.Black);
        }
    }


    private void HandleTransformChanged()
    {
        var changed = Transforms.Where(t => t != null).Any(t => !Hulls?.ContainsKey(t.GetInstanceID()) ?? true);
        if (changed)
        {
            EnsureDestroyed();

            Hulls = Transforms.Where(t => t != null).ToDictionary(k => k.GetInstanceID(), v => (v, CreateHull(v)));
        }
    }

    private NativeHull CreateHull(Transform v)
    {
        var collider = v.GetComponent<Collider>();
        if (collider is BoxCollider boxCollider)
        {
            return HullFactory.CreateBox(boxCollider.size);
        }
        else if(collider is MeshCollider meshCollider)
        {
            return HullFactory.CreateFromMesh(meshCollider.sharedMesh);
        }
        else
        {
            var mf = v.GetComponent<MeshFilter>();
            if(mf != null)
            {
                return HullFactory.CreateFromMesh(mf.sharedMesh);
            }
        }
        throw new InvalidOperationException($"Unable to create a hull from the GameObject '{v?.name}'");
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
    }

#if UNITY_EDITOR
    private void EditorApplication_playModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
                EnsureDestroyed();
                break;
        }
    }
#endif

    void OnDestroy() => EnsureDestroyed();
    void OnDisable() => EnsureDestroyed();

    private void EnsureDestroyed()
    {
        if (Hulls == null)
            return;

        foreach(var kvp in Hulls)
        {
            if (kvp.Value.Hull.IsValid)
            {
                kvp.Value.Hull.Dispose();
            }
        }

        Hulls.Clear();
    }


}


