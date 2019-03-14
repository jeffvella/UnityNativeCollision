using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Vella.Common;
using Vella.UnityNativeHull;
using BoundingSphere = Vella.Common.BoundingSphere;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class BvhTester : MonoBehaviour
{
    public List<Transform> Transforms;

    private Dictionary<int, (TestShape Shape, GameObject Go)> _currentShapes;

    private NativeBoundingHierarchy<TestShape> _bvh;

    public bool ShouldRemoveClones { get; set; } = false;

    void Update()
    {
        CheckInputForChanges(Transforms);

        UpdateChangedTransforms();

        DrawBvh();

        ProcessEditorActions();
    }

    private void ProcessEditorActions()
    {
        if (ShouldRemoveClones)
        {
            ShouldRemoveClones = false;
            RemoveClones();

        }
    }

    private void CheckInputForChanges(IList<Transform> transforms)
    {
        if (transforms.Count != _currentShapes?.Count || transforms.Any(t => !_currentShapes.ContainsKey(t.GetInstanceID())))
        {
            RebuildBvh();
        }
    }

    private void RebuildBvh()
    {
        EnsureDestroyed();

        _bvh?.Dispose();
        _bvh = new NativeBoundingHierarchy<TestShape>();
        _currentShapes = new Dictionary<int, (TestShape Shape, GameObject Go)>();

        foreach (var t in Transforms.Where(t => t != null))
        {
            var shape = CreateShape(t);
            var key = t.GetInstanceID();
            _currentShapes.Add(key, (shape, t.gameObject));
            _bvh.Add(shape);
        }

        Debug.Log("Rebuild Complete");
    }

    private void DrawBvh()
    {
        _bvh.TraverseNodes(node =>
        {
            DebugDrawer.DrawWireCube(node.Box.Center(), node.Box.Size(), UnityColors.LightSlateGray.ToOpacity(0.6f));
            return true;
        });       
    }

    private void UpdateChangedTransforms()
    {
        foreach (var t in Transforms)
        {
            if (t == null)
                continue;

            var id = t.GetInstanceID();

            for (int i = 0; i < _bvh.Buckets.Length; i++)
            {
                ref NativeBuffer<TestShape> bucket = ref _bvh.Buckets[i];
                if (!bucket.IsCreated)
                    continue;

                for (int j = 0; j < bucket.Length; j++)
                {                    
                    ref TestShape shape = ref bucket[j];
                    if (shape.Id == id)
                    {
                        if (t.hasChanged && (Vector3)shape.Transform.pos != t.position)
                        {
                            shape.Transform = new RigidTransform(t.rotation, t.position);

                            shape.OnTransformChanged();

                            _bvh.QueueForOptimize(shape);
                        }
                        shape.OnUpdate();
                    }
                }
            }
        }
        _bvh.Optimize();
    }

    private TestShape CreateShape(Transform t)
    {
        var c = t.GetComponent<Collider>();
        if (c == null)
            throw new ArgumentException("BvHTester requires GameObjects to have a collider");

        var bounds = new BoundingBox(c.bounds.min,c.bounds.max);
        var sphere = BoundingSphere.FromAABB(bounds);
        return new TestShape
        {
            BoundingBox = bounds,
            BoundingSphere = sphere,
            Id = t.GetInstanceID(),
            Transform = new RigidTransform(t.rotation, t.position),         
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
        _bvh?.Dispose();
    }

    public void AddRandomGameObject()
    {
        var first = Transforms.FirstOrDefault();
        if (first != null)
        {
            var go = Instantiate(first.gameObject);
            go.transform.position = UnityEngine.Random.insideUnitSphere * 10f;
            Transforms.Add(go.transform);
            var id = go.transform.GetInstanceID();
            var shape = CreateShape(go.transform);
            _currentShapes.Add(id, (shape, go));
            _bvh.Add(shape);
        }
    }

    //public void RemoveSingle()
    //{
    //    var first = Transforms.FirstOrDefault(t => t != null && t.name.ToLowerInvariant().Contains("clone"));
    //    if (first != null)
    //    {
    //        Transforms.Remove(first);
    //        DestroyImmediate(first.gameObject);
    //    }
    //}

    public void RemoveClones()
    {
        foreach (var t in FindObjectsOfType<Transform>().Where(t => t != null).ToList())
        {
            if (t.name.ToLowerInvariant().Contains("clone"))
            {
                RemoveShape(t.GetInstanceID());
                Transforms.Remove(t);
                DestroyImmediate(t.gameObject);
            }
        }
    }

    public void RemoveShape(int id)
    {
        if (_currentShapes.TryGetValue(id, out (TestShape Shape, GameObject Go) pair))
        {       
            Debug.Log($"Removing {pair.Go.name} from bvh");
            _bvh.Remove(pair.Shape);
            _currentShapes.Remove(id);
        }         
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(BvhTester))]
[CanEditMultipleObjects]
public class BvhTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        foreach (var targ in targets.Cast<BvhTester>())
        {
            if (GUILayout.Button("Add Clone"))
            {
                targ.AddRandomGameObject();                
            }
            else if (GUILayout.Button("Remove Clones"))
            {
                targ.RemoveClones();
            }
            SceneView.RepaintAll();
        }        
    }
}

#endif


