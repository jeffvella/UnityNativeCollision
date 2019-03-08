using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using System;
using Unity.Collections;
using Vella.Common;
using Vella.UnityNativeHull;
using SimpleScene;
using SimpleScene.Util.ssBVH;
using TMPro;
using Random = Unity.Mathematics.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HullTester : MonoBehaviour
{
    public List<Transform> Transforms;

    public DebugHullFlags HullDrawingOptions = DebugHullFlags.Outline;

    [Header("Visualizations")]
    public bool DrawIsCollided;
    public bool DrawContact;
    public bool DrawIntersection;
    public bool DrawClosestFace;
    public bool DrawClosestPoint;
    public bool DrawBVH;

    [Header("Console Logging")]
    public bool LogCollisions;
    public bool LogClosestPoint;
    public bool LogContact;

    private Dictionary<int, TestShape> Hulls;
    private Dictionary<int, GameObject> GameObjects;
    private NativeBoundingHierarchy<TestShape> _bvh;
    private bool _removeClones;
    public bool ForceRebuild { get; set; }

    void Update()
    {
        HandleTransformChanged();

        foreach (var t in Transforms)
        {
            var id = t.GetInstanceID();

            for (int i = 0; i < _bvh._buckets.Length; i++)
            {
                for (int j = 0; j < _bvh._buckets[i].Length; j++)
                {
                    ref var shape = ref _bvh._buckets[i][j];

                    if (shape.TransformId == id && t.hasChanged && (Vector3)shape.Transform.pos != t.position)
                    {
                        shape.Transform = new RigidTransform(t.rotation, t.position);                        
                    }
                    _bvh.QueueForUpdate(shape);
                }
            }
        }

        _bvh.Optimize();

        DrawBvh();

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
                var result4 = HullOperations.ClosestPoint.Invoke(transformA, hullA, 0);
                sw4.Stop();

                if (DrawClosestPoint)
                {
                    DebugDrawer.DrawSphere(result4, 0.1f, Color.blue);
                    DebugDrawer.DrawLine(result4, Vector3.zero, Color.blue);
                }

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

                DrawHullCollision(tA.gameObject, tB.gameObject, transformA, hullA, transformB, hullB);

                if (LogCollisions)
                {
                    var sw1 = System.Diagnostics.Stopwatch.StartNew();
                    var result1 = HullCollision.IsColliding(transformA, hullA, transformB, hullB);
                    sw1.Stop();

                    var sw2 = System.Diagnostics.Stopwatch.StartNew();
                    var result2 = HullOperations.IsColliding.Invoke(transformA, hullA, transformB, hullB);
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
            Transform = new RigidTransform(t.Value.Transform.rot, t.Value.Transform.pos),
            Hull = t.Value.Hull,

        }).ToArray();

        using (var hulls = new NativeArray<BatchCollisionInput>(batchInput, Allocator.TempJob))
        using (var results = new NativeList<BatchCollisionResult>(batchInput.Length, Allocator.TempJob))
        {
            var sw3 = System.Diagnostics.Stopwatch.StartNew();
            var collisions = HullOperations.CollisionBatch.Invoke(hulls, results);
            sw3.Stop();

            Debug.Log($"Batch Collisions took {sw3.Elapsed.TotalMilliseconds:N4}ms ({results.Length} collisions from {hulls.Length} hulls)");

            if (collisions)
            {
                foreach (var result in results.AsArray())
                {
                    Debug.Log($" > {GameObjects[result.A.Id].name} collided with {GameObjects[result.B.Id].name}");
                }
            }
        }
    }

    public void DrawHullCollision(GameObject a, GameObject b, RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
    {

        var collision = HullCollision.GetDebugCollisionInfo(t1, hull1, t2, hull2);
        if (collision.IsColliding)
        {
            if (DrawIntersection) // Visualize all faces of the intersection
            {
                HullIntersection.DrawNativeHullHullIntersection(t1, hull1, t2, hull2);              
            }

            if (DrawContact || LogContact)  // Visualize the minimal contact calcluation for physics
            {
                //var manifold = HullOperations.GetContact.Invoke(t1, hull1, t2, hull2);
                
                var sw1 = System.Diagnostics.Stopwatch.StartNew();
                var tmp = new NativeManifold(Allocator.Persistent);
                var normalResult = HullIntersection.NativeHullHullContact(ref tmp, t1, hull1, t2, hull2);
                sw1.Stop();
                tmp.Dispose();

                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                var burstResult = HullOperations.TryGetContact.Invoke(out NativeManifold manifold, t1, hull1, t2, hull2);
                sw2.Stop();

                if(LogContact)
                {
                    Debug.Log($"GetContact between '{a.name}'/'{b.name}' took: {sw1.Elapsed.TotalMilliseconds:N4}ms (Normal), {sw2.Elapsed.TotalMilliseconds:N4}ms (Burst)");
                }

                if (DrawContact && burstResult)
                {
                    // Do something with manifold

                    HullDrawingUtility.DebugDrawManifold(manifold);

                    //var points = manifold.Points;

                    for (int i = 0; i < manifold.Length; i++)
                    {
                        var point = manifold[i];
                        DebugDrawer.DrawSphere(point.Position, 0.02f);
                        DebugDrawer.DrawArrow(point.Position, manifold.Normal * 0.2f);

                        var penentrationPoint = point.Position + manifold.Normal * point.Distance;
                        DebugDrawer.DrawLabel(penentrationPoint, $"{point.Distance:N2}");

                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge1, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge1, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge2, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge2, t1, hull1);

                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge1, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge1, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge2, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge2, t2, hull2);

                        DebugDrawer.DrawDottedLine(point.Position, penentrationPoint);
                    }

                    manifold.Dispose();
                }
                
            }

            if(DrawIsCollided)
            {
                DebugDrawer.DrawSphere(t1.pos, 0.1f, UnityColors.GhostDodgerBlue);
                DebugDrawer.DrawSphere(t2.pos, 0.1f, UnityColors.GhostDodgerBlue);
            }
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
        var transforms = Transforms.Distinct().Where(t => t.gameObject.activeSelf).ToList();
        var newTransformFound = false;
        var transformCount = 0;

        if (!ForceRebuild && Hulls != null)
        {
            for (var i = 0; i < transforms.Count; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;

                transformCount++;

                var foundNewHull = !Hulls.ContainsKey(t.GetInstanceID());
                if (foundNewHull)
                {
                    newTransformFound = true;
                    break;
                }
            }

            if (!newTransformFound && transformCount == Hulls.Count)
                return;
        }

        Debug.Log("Rebuilding Objects");

        EnsureDestroyed();

        if (_bvh != null && _bvh.IsCreated)
        {
            _bvh.Dispose();
        }

        _bvh = new NativeBoundingHierarchy<TestShape>();

        Hulls = transforms.Where(t => t != null).ToDictionary(k => k.GetInstanceID(), CreateShape);

        GameObjects = transforms.Where(t => t != null).ToDictionary(k => k.GetInstanceID(), t => t.gameObject);

        foreach (var shape in Hulls.Values)
        {
            _bvh.Add(shape);
        }

        ForceRebuild = false;
        SceneView.RepaintAll();
    }

    private void DrawBvh()
    {
        if (DrawBVH)
        {
            _bvh.TraverseNode(node =>
            {
                DebugDrawer.DrawDottedWireCube(node.Box.Center(), node.Box.Diff(), UnityColors.LightSlateGray.ToOpacity(0.6f));
                return true;
            });
        }
    }

    private TestShape CreateShape(Transform t)
    {        
        var bounds = new BoundingBox();
        var hull = CreateHull(t);

        for (int i = 0; i < hull.VertexCount; i++)
        {
            var v = hull.GetVertex(i);
            bounds.Encapsulate(v);
        }

        var sphere = SimpleScene.BoundingSphere.FromAABB(bounds);

        return new TestShape
        {
            BoundingBox = bounds,
            BoundingSphere = sphere,
            TransformId = t.GetInstanceID(),
            Transform = new RigidTransform(t.rotation, t.position),
            Hull = hull,
        };
    }

    private NativeHull CreateHull(Transform v)
    {
        var collider = v.GetComponent<Collider>();
        if (collider is BoxCollider boxCollider)
        {
            return HullFactory.CreateBox(boxCollider.size);
        }
        if(collider is MeshCollider meshCollider)
        {
            return HullFactory.CreateFromMesh(meshCollider.sharedMesh);
        }
        var mf = v.GetComponent<MeshFilter>();
        if(mf != null && mf.sharedMesh != null)
        {
            return HullFactory.CreateFromMesh(mf.sharedMesh);
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

        _bvh.Dispose();
        Hulls.Clear();
    }

    public void OptimizeBVH()
    {
        _bvh.Optimize();
    }

    public void AddSingle()
    {        
        var first = Transforms.FirstOrDefault();
        if (first != null)
        {
            var transforms = Transforms.Where(t => t != null && t != first).ToList();

            var go = Instantiate(first.gameObject);
            var i1 = UnityEngine.Random.Range(0, transforms.Count);
            var i2 = UnityEngine.Random.Range(0, transforms.Count);

            var t1 = transforms.ElementAt(i1);
            var t2 = transforms.ElementAt(i2);

            var dir = t2.position - t1.position;
            var rand = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0, dir.magnitude);
            var pos = t1.position + rand + dir.normalized * (dir.magnitude/2f);

            go.transform.position = pos;

            var id = go.transform.GetInstanceID();
            var shape = CreateShape(go.transform);

            Hulls.Add(id, shape);
            GameObjects.Add(id, go);
            Transforms.Add(go.transform);

            _bvh.Add(shape);
        }
    }

    public void RemoveSingle()
    {
        var first = Transforms.FirstOrDefault(t => t != null && t.name.ToLowerInvariant().Contains("clone"));
        if (first != null)
        {
            Transforms.Remove(first);
            DestroyImmediate(first.gameObject);
        }
    }

    public void RemoveFromBvh()
    {
        foreach (var t in Transforms)
        {
            if (t != null && t.name.ToLowerInvariant().Contains("clone"))
            {
                var shape = Hulls[t.GetInstanceID()];
                if (_bvh.TryGetLeaf(shape, out Node node))
                {
                    Debug.Log($"Removing {t.name} from bvh");
                    _bvh.Remove(shape);
                    break;
                }
            }
        }
    }

    public void RemoveClones()
    {
        var result = new List<Transform>();
        foreach (var t in Transforms.ToList())
        {
            if (t.name.ToLowerInvariant().Contains("clone"))
            {
                DestroyImmediate(t.gameObject);                
            }
            else
            {
                result.Add(t);
            }
        }
        Transforms = result;
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(HullTester))]
[CanEditMultipleObjects]
public class HullTester_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        foreach (var targ in targets.Cast<HullTester>())
        {
            if (GUILayout.Button("Optimize BVH"))
            {
                targ.OptimizeBVH();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Rebuild BVH"))
            {
                targ.ForceRebuild = true;
            }
            if (GUILayout.Button("Add Clone"))
            {
                targ.AddSingle();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Remove from Bvh"))
            {
                targ.RemoveFromBvh();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Remove Clones (Rebuild)"))
            {
                targ.RemoveClones();
                SceneView.RepaintAll();
            }
        }

        
    }
}

#endif


