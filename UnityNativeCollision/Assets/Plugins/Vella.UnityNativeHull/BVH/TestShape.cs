using SimpleScene;
using SimpleScene.Util.ssBVH;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Vella.UnityNativeHull;
using BoundingSphere = SimpleScene.BoundingSphere;


[DebuggerDisplay("TestShape: Id={TransformId}")]
public struct TestShape : IBoundingHierarchyNode, IEquatable<TestShape>
{
    //public Transform Transform;
    public int TransformId;

    public bool HasChanged => true;

    public float3 Position => Transform.pos;

    public float Radius => BoundingSphere.radius;

    public RigidTransform Transform;
    public NativeHull Hull;
    public BoundingBox BoundingBox;
    public BoundingSphere BoundingSphere;

    public bool Equals(TestShape other)
    {
        return TransformId == other.TransformId;
    }

    public override bool Equals(object obj)
    {
        return obj is TestShape cast && cast.Equals(this);
    }

    public override int GetHashCode()
    {
        return TransformId;
    }

    public bool Equals(IBoundingHierarchyNode other)
    {
        return Equals((TestShape)other);
    }

    //public void SetTransform(RigidTransform rigidTransform)
    //{

    //    Transform = rigidTransform;
    //}

    //public void SetTransform()
    //{
    //    HasChanged = 0;
    //}

    //public ulong LastUpdatedFrame;

    //public bool CheckForChanges()
    //{
    //    var frame = (ulong)Time.frameCount;
    //    if (LastUpdatedFrame != frame)
    //    {
    //        unchecked
    //        {
    //            LastUpdatedFrame = frame;
    //        }

    //        var t = UnityEngine.GameObject.Find(TransformId.ToString())?.transform ?? null;

    //        if (t != null && (t.position != (Vector3)Transform.pos || t.rotation != Transform.rot))
    //        {
    //            HasChanged = 1;
    //            Transform = new RigidTransform(t.rotation, t.position);
    //            return true;
    //        }
    //        else
    //        {
    //            HasChanged = 0;                
    //        }
    //    }
    //    return false;
    //}



    //public bool HasChanged()
    //{
    //    //var frame = (ulong)Time.frameCount;
    //    //if (LastUpdatedFrame != frame)
    //    //{
    //    //    unchecked
    //    //    {
    //    //        LastUpdatedFrame = frame;
    //    //    }
    //    //}
    //}

    //public bool HasChanged => UnityEngine.GameObject.Find(TransformId.ToString())?.transform.hasChanged ?? false;

    //private int _hasChanged;

    //public unsafe int HasChanged => ref *(int*)UnsafeUtility.AddressOf(ref _hasChanged);


    //public float3 CalculateMinMax()
    //{
    //    float3 v = Hull.GetVertex(0);
    //    float3 min = v;
    //    float3 max = v;
    //    for (int i = 1; i < Hull.VertexCount; ++i)
    //    {
    //        v = Hull.GetVertex(i);
    //        if (min.x < v.x) min.x = v.x;
    //        if (min.y < v.y) min.y = v.y;
    //        if (min.z < v.z) min.z = v.z;
    //        if (max.x < v.x) max.x = v.x;
    //        if (max.y < v.y) max.y = v.y;
    //        if (max.z < v.z) max.z = v.z;
    //    }
    //    return max - min;
    //}


}



