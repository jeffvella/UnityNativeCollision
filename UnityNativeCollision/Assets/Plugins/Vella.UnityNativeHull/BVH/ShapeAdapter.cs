using System;
using System.Collections.Generic;
using SimpleScene;
using SimpleScene.Util.ssBVH;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

//public class TestShapeBVH : NativeBoundingHierarchy<TestShape>
//{
//    public TestShapeBVH(int maxPerLeaf = 1) : base(new ShapeAdapter(), new List<TestShape>(), maxPerLeaf)
//    {

//    }
//}

//public struct ShapeAdapter : IBoundingHierarchyAdapter<TestShape>
//{
//    public void SetBvH(NativeBoundingHierarchy<TestShape> bvh)
//    {        
//        //_map = new NativeHashMap<TestShape, Node>(NativeBoundingHierarchy<TestShape>.MaxNodes, Allocator.Persistent);
//        _bvh = bvh;
//    }

//    private NativeBoundingHierarchy<TestShape> _bvh;

//    public NativeBoundingHierarchy<TestShape> BVH => _bvh;

//    //public void setBVH(NativeBoundingHierarchy<TestShape> bvh)
//    //{
//    //    _bvh = bvh;
//    //}

//    public float3 Position(TestShape shape)
//    {
//        return shape.Position;
//    }

//    public float Radius(TestShape shape)
//    {
//        return shape.Radius;
//    }

//    //public void Contains(TestShape obj)
//    //{ 
//    //    if(!_bvh.Map.TryGetValue(obj, out Node item))
//    //    //if (!map.ContainsKey(sphere))
//    //    {
//    //        throw new Exception("missing map for a shuffled child");
//    //    }
//    //}

//    public bool CheckForChanges(TestShape obj)
//    {
//        return true;
//        //if (obj.HasChanged == 1)
//        //{
//        //var o = obj;
//        //if (_bvh.Map.TryGetValue(obj, out Node item))
//        //{
//        //    //var leafNode = item;

//        //    _bvh.Refit_ObjectChanged(item, ref obj);

//        //    //_bvh.Map[obj].Refit_ObjectChanged(this, ref obj);
//        //}
//        //}
//    }


//    //public void Dispose()
//    //{
//    //    if(_map.IsCreated)
//    //    {
//    //        _map.Dispose();
//    //    }
//    //}


//}

