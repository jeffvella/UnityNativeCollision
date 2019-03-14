using System;
using System.Diagnostics;
using Unity.Mathematics;
using Vella.Common;
using Vella.UnityNativeHull;

[DebuggerDisplay("TestShape: Id={Id}")]
public struct TestShape : IBoundingHierarchyNode, IEquatable<TestShape>, IComparable<TestShape>
{    
    public int Id;

    public RigidTransform Transform;
    public NativeHull Hull;

    public BoundingBox BoundingBox;
    public BoundingSphere BoundingSphere;

    public bool HasChanged => true;

    public float3 Position => Transform.pos;

    public float Radius => BoundingSphere.radius;

    public bool Equals(TestShape other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is TestShape shape && shape.Equals(this);
    }

    public int CompareTo(TestShape other)
    {
        return Id.CompareTo(other.Id);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public void OnUpdate()
    {
            
    }

    public void OnTransformChanged()
    {
            
    }

}



