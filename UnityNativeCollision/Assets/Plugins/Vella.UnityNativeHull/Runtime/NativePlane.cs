using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("NativePlane: {Normal}, {Offset}")]
    public unsafe struct NativePlane
    {
        /// <summary>
        /// Direction of the plane from hull origin
        /// </summary>
        public float3 Normal;
        
        /// <summary>
        /// Distance of the plane from hull origin.
        /// </summary>
        public float Offset;

        public float3 Position => Normal * Offset;

        public NativePlane(float3 normal, float offset)
        {
            Normal = normal;
            Offset = offset;    
        }

        public NativePlane(float3 a, float3 b, float3 c)
        {
            Normal = normalize(cross(b - a, c - a));
            Offset = dot(Normal, a);       
        }

        public float Distance(float3 point)
        {
            return dot(Normal, point) - Offset;
        }

        public float3 ClosestPoint(float3 point)
        {
            return point - Distance(point) * normalize(Normal);
        }

        public (float3 Position, float3 Rotation) Transform(RigidTransform t)
        {
            float3 tRot = mul(t.rot, Normal);
            return (t.pos + tRot * Offset, tRot);
        }

        public static NativePlane operator *(float4x4 m, NativePlane plane)
        {
            float3 tPos = transform(m, plane.Normal * plane.Offset);
            float3 tRot = rotate(m, plane.Normal);
            return new NativePlane(tRot, dot(tRot, tPos));
        }        

        public static NativePlane operator *(RigidTransform t, NativePlane plane)
        {
            float3 normal = mul(t.rot, plane.Normal);
            return new NativePlane(normal, plane.Offset + dot(normal, t.pos));
        }

        /// <summary>
        /// Is a point on the positive side of the plane
        /// </summary>
        public bool IsPositiveSide(float3 point)
        {
            return dot(Normal, point) + Offset > 0.0;
        }

        /// <summary>
        /// If two points on the same side of the plane
        /// </summary>
        public bool SameSide(float3 a, float3 b)
        {
            float distanceToPoint1 = Distance(a);
            float distanceToPoint2 = Distance(b);
            return distanceToPoint1 > 0.0 && distanceToPoint2 > 0.0 || distanceToPoint1 <= 0.0 && distanceToPoint2 <= 0.0;
        }

        public bool Raycast(Ray ray, out float enter)
        {
            float a = dot(ray.direction, Normal);
            float num = -dot(ray.origin, Normal) - Offset;
            if (Mathf.Approximately(a, 0.0f))
            {
                enter = 0.0f;
                return false;
            }
            enter = num / a;
            return enter > 0.0;
        }

        public Plane Flipped => new Plane(-Normal, -Offset);
    };
}