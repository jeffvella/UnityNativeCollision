using SimpleScene;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Vella.UnityNativeHull;

public interface IBVHNode
{
    float3 Position { get; }
    float Radius { get; }
    ref int HasChanged { get; }
}

[DebuggerDisplay("TestShape: Id={TransformId}")]
public struct TestShape : IBVHNode, IEquatable<TestShape>
{
    //public Transform Transform;
    public int TransformId;



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

    private int _hasChanged;
    public unsafe ref int HasChanged => ref *(int*)UnsafeUtility.AddressOf(ref _hasChanged);

    public float3 Position => Transform.pos;
    public float Radius => BoundingSphere.radius;

    public NativeHull Hull;

    public SSAABB BoundingBox;

    public SimpleScene.BoundingSphere BoundingSphere;

    public void SetTransform(RigidTransform rigidTransform)
    {
        HasChanged = 1;
        Transform = rigidTransform;
    }

    public void SetTransform()
    {
        HasChanged = 0;
    }

    public RigidTransform Transform;

    public ulong LastUpdatedFrame;

    public bool CheckForChanges()
    {
        var frame = (ulong)Time.frameCount;
        if (LastUpdatedFrame != frame)
        {
            unchecked
            {
                LastUpdatedFrame = frame;
            }

            var t = UnityEngine.GameObject.Find(TransformId.ToString())?.transform ?? null;

            if (t != null && (t.position != (Vector3)Transform.pos || t.rotation != Transform.rot))
            {
                HasChanged = 1;
                Transform = new RigidTransform(t.rotation, t.position);
                return true;
            }
            else
            {
                HasChanged = 0;                
            }
        }
        return false;
    }

    public bool Equals(TestShape other)
    {
        return TransformId == other.TransformId; // other.Transform.pos == Transform.pos && other.Transform.rot == Transform.rot;
    }

    public override bool Equals(object obj)
    {
        return obj is TestShape cast && cast.Equals(this);     
    }

    public override int GetHashCode()
    {
        return TransformId;
    }

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



