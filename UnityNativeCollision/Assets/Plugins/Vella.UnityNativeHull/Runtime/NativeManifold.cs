using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    public unsafe struct NativeManifold : IDisposable
    {
        public NativeManifold(Allocator allocator)
        {
            Points = new NativeList<ContactPoint>(MaxPoints, Allocator.Persistent);
            Normal = 0;
        }

        public const int MaxPoints = 24;

        public float3 Normal; //A -> B.

        public NativeList<ContactPoint> Points;
        public bool IsCreated => Points.IsCreated;

        public void Add(ContactPoint cp)
        {
            Points.Add(cp);
        }

        public void Add(float3 position, float distance, ContactID id)
        {
            Points.Add(new ContactPoint
            {
                Id = id,
                Position = position,
                Distance = distance,           
            });
        }

        public void Dispose()
        {
            if (Points.IsCreated)
            {
                Points.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ContactPoint
    {        
        public ContactID Id;

        /// <summary>
        /// The contact point, at the point in time when the shapes first collide.
        /// (center of the line between points on each shape).
        /// </summary>
        public float3 Position;

        public float NormalImpulse;
        public float3 Tangent1;
        public float3 Tangent2;
        public float TangentImpulse1;
        public float TangentImpulse2;
        public float Distance;

        public float3 Penetration;

        /// <summary>
        /// Closest position clamped to the edge
        /// </summary>
        public float3 PositionOnTarget;

        /// <summary>
        /// Closest position clamped to the edge
        /// </summary>
        public float3 PositionOnSource;
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct ContactID
    {
        [FieldOffset(0)]
        public FeaturePair FeaturePair;
        [FieldOffset(0)]
        public int Key;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FeaturePair
    {
        public sbyte InEdge1;
        public sbyte OutEdge1;
        public sbyte InEdge2;
        public sbyte OutEdge2;
    }

}
