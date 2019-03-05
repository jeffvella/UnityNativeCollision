using System;
using System.Collections.Generic;
using SimpleScene;
using SimpleScene.Util.ssBVH;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class TestShapeBVH : NativeBoundingHierarchy<TestShape>
{
    public TestShapeBVH(int maxPerLeaf = 1) : base(new ShapeAdapter(), new List<TestShape>(), maxPerLeaf)
    {

    }
}

public struct ShapeAdapter : IBVHNodeAdapter<TestShape>, IDisposable
{
    public void Allocate(NativeBoundingHierarchy<TestShape> bvh)
    {        
        _map = new NativeHashMap<TestShape, Node>(NativeBoundingHierarchy<TestShape>.MaxNodes, Allocator.Persistent);
        _bvh = bvh;
    }

    private NativeBoundingHierarchy<TestShape> _bvh;

    private NativeHashMap<TestShape, Node> _map;

    public bool IsCreated => _map.IsCreated;

    // public Dictionary<TestShape, Node> map = new Dictionary<TestShape, Node>();

    public NativeBoundingHierarchy<TestShape> BVH { get { return _bvh; } }

    public void setBVH(NativeBoundingHierarchy<TestShape> bvh)
    {
        _bvh = bvh;
    }

    public float3 objectpos(TestShape shape)
    {
        return shape.Position;
    }

    public float radius(TestShape shape)
    {
        return shape.Radius;
    }

    public void checkMap(TestShape obj)
    { 
        if(!_map.TryGetValue(obj, out Node item))
        //if (!map.ContainsKey(sphere))
        {
            throw new Exception("missing map for a shuffled child");
        }
    }

    public void UnmapObject(TestShape obj)
    {
        _map.Remove(obj);
    }

    public void mapObjectToBVHLeaf(TestShape sphere, Node leaf)
    {
        // why no set/replace functionality?????????
        _map.Remove(sphere);
        _map.TryAdd(sphere, leaf);
    }

    public Node getLeaf(TestShape obj)
    {
        return _map.TryGetValue(obj, out Node item) ? _map[obj] : default;       
    }

    public void checkForChanges(TestShape obj)
    {
        //if (obj.HasChanged == 1)
        //{
            //var o = obj;
            if (_map.TryGetValue(obj, out Node item))
            {
                _map[obj].Refit_ObjectChanged(this, ref obj);
            }
        //}
    }

    public void Dispose()
    {
        if(_map.IsCreated)
        {
            _map.Dispose();
        }
    }


}

