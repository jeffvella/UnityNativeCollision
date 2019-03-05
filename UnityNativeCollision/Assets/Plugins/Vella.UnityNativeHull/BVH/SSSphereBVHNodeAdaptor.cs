using System;
using System.Collections.Generic;
using SimpleScene;
using SimpleScene.Util.ssBVH;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class TestShapeBVH : ssBVH<TestShape>
{
    public TestShapeBVH(int maxSpheresPerLeaf = 1) : base(new TestShapeNodeAdapter(), new List<TestShape>(), maxSpheresPerLeaf)
    {

    }
}

public class TestShapeNodeAdapter : IBVHNodeAdapter<TestShape>
{
    protected ssBVH<TestShape> _bvh;

    //public NativeHashMap<TestShape, Node<TestShape>> map = new NativeHashMap<TestShape, Node<TestShape>>();

    public Dictionary<TestShape, Node<TestShape>> map = new Dictionary<TestShape, Node<TestShape>>();

    public ssBVH<TestShape> BVH { get { return _bvh; } }

    public void setBVH(ssBVH<TestShape> bvh)
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

    public void checkMap(TestShape sphere)
    {
        if (!map.ContainsKey(sphere))
        {
            throw new Exception("missing map for a shuffled child");
        }
    }

    public void UnmapObject(TestShape sphere)
    {
        map.Remove(sphere);
    }

    public void mapObjectToBVHLeaf(TestShape sphere, Node<TestShape> leaf)
    {
        map[sphere] = leaf;
    }

    public Node<TestShape> getLeaf(TestShape sphere)
    {
        return map.ContainsKey(sphere) ? map[sphere] : default;       
    }

    public void checkForChanges(TestShape obj)
    {
        //if (obj.HasChanged == 1)
        //{
            //var o = obj;
            if (map.ContainsKey(obj))
            {
                map[obj].Refit_ObjectChanged(this, ref obj);
            }
        //}
    }
}