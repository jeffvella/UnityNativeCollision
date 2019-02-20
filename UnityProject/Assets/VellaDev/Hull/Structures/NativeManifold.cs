using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VellaDev.Hull
{
    public unsafe struct NativeManifold : IDisposable
    {
        public NativeManifold(Allocator allocator)
        {
            Points = new NativeList<ContactPoint>(MaxPoints, Allocator.Persistent);
            normal = 0;
        }

        public const int MaxPoints = 24;

        public float3 normal; //A -> B.

        public NativeList<ContactPoint> Points;
        public bool IsCreated => Points.IsCreated;

        public void Add(ContactPoint cp)
        {
            Points.Add(cp);
        }

        public void Add(float3 position, float distance, b3ContactID id)
        {
            Points.Add(new ContactPoint
            {
                id = id,
                position = position,
                distance = distance,           
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
        public b3ContactID id;

        /// <summary>
        /// The contact point, at the point in time when the shapes first collide.
        /// (center of the line between points on each shape).
        /// </summary>
        public float3 position;

        public float normalImpulse;
        public float3 tangent1;
        public float3 tangent2;
        public float tangentImpulse1;
        public float tangentImpulse2;
        public float distance;

        public float3 penetration;

        /// <summary>
        /// Closest position clamped to the edge
        /// </summary>
        public float3 positionOnTarget;

        /// <summary>
        /// Closest position clamped to the edge
        /// </summary>
        public float3 positionOnSource;
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct b3ContactID
    {
        [FieldOffset(0)]
        public b3FeaturePair featurePair;
        [FieldOffset(0)]
        public int key;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct b3FeaturePair
    {
        public sbyte inEdge1;
        public sbyte outEdge1;
        public sbyte inEdge2;
        public sbyte outEdge2;
    }

}
