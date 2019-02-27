using System.Diagnostics;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("Plane: {Normal}, {Offset}")]
    public unsafe struct NativePlane
    {
        public float3 Normal;
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

    };
}