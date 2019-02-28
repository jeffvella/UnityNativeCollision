using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
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

        public bool Raycast(UnityEngine.Ray ray, out float enter)
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

    };
}

//// Decompiled with JetBrains decompiler
//// Type: UnityEngine.Plane
//// Assembly: UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
//// MVID: E011EA43-0557-4995-8088-867190BE39D5
//// Assembly location: X:\UnityEditors\2019.2.0a4\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll

////using UnityEngine.Scripting;

//namespace UnityEngine1
//{
//    /// <summary>
//    ///   <para>Representation of a plane in 3D space.</para>
//    /// </summary>
//    //[UsedByNativeCode]
//    public struct Plane
//    {
//        internal const int size = 16;
//        private Vector3 m_Normal;
//        private float m_Distance;

//        /// <summary>
//        ///   <para>Creates a plane.</para>
//        /// </summary>
//        /// <param name="inNormal"></param>
//        /// <param name="inPoint"></param>
//        public Plane(Vector3 inNormal, Vector3 inPoint)
//        {
//            this.m_Normal = Vector3.Normalize(inNormal);
//            this.m_Distance = -Vector3.Dot(this.m_Normal, inPoint);
//        }

//        /// <summary>
//        ///   <para>Creates a plane.</para>
//        /// </summary>
//        /// <param name="inNormal"></param>
//        /// <param name="d"></param>
//        public Plane(Vector3 inNormal, float d)
//        {
//            this.m_Normal = Vector3.Normalize(inNormal);
//            this.m_Distance = d;
//        }

//        /// <summary>
//        ///   <para>Creates a plane.</para>
//        /// </summary>
//        /// <param name="a"></param>
//        /// <param name="b"></param>
//        /// <param name="c"></param>
//        public Plane(Vector3 a, Vector3 b, Vector3 c)
//        {
//            this.m_Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
//            this.m_Distance = -Vector3.Dot(this.m_Normal, a);
//        }

//        /// <summary>
//        ///   <para>Normal vector of the plane.</para>
//        /// </summary>
//        public Vector3 normal
//        {
//            get
//            {
//                return this.m_Normal;
//            }
//            set
//            {
//                this.m_Normal = value;
//            }
//        }

//        /// <summary>
//        ///   <para>Distance from the origin to the plane.</para>
//        /// </summary>
//        public float distance
//        {
//            get
//            {
//                return this.m_Distance;
//            }
//            set
//            {
//                this.m_Distance = value;
//            }
//        }

//        /// <summary>
//        ///   <para>Sets a plane using a point that lies within it along with a normal to orient it.</para>
//        /// </summary>
//        /// <param name="inNormal">The plane's normal vector.</param>
//        /// <param name="inPoint">A point that lies on the plane.</param>
//        public void SetNormalAndPosition(Vector3 inNormal, Vector3 inPoint)
//        {
//            this.m_Normal = Vector3.Normalize(inNormal);
//            this.m_Distance = -Vector3.Dot(inNormal, inPoint);
//        }

//        /// <summary>
//        ///   <para>Sets a plane using three points that lie within it.  The points go around clockwise as you look down on the top surface of the plane.</para>
//        /// </summary>
//        /// <param name="a">First point in clockwise order.</param>
//        /// <param name="b">Second point in clockwise order.</param>
//        /// <param name="c">Third point in clockwise order.</param>
//        public void Set3Points(Vector3 a, Vector3 b, Vector3 c)
//        {
//            this.m_Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
//            this.m_Distance = -Vector3.Dot(this.m_Normal, a);
//        }

//        /// <summary>
//        ///   <para>Makes the plane face in the opposite direction.</para>
//        /// </summary>
//        public void Flip()
//        {
//            this.m_Normal = -this.m_Normal;
//            this.m_Distance = -this.m_Distance;
//        }

//        /// <summary>
//        ///   <para>Returns a copy of the plane that faces in the opposite direction.</para>
//        /// </summary>
//        public Plane flipped
//        {
//            get
//            {
//                return new Plane(-this.m_Normal, -this.m_Distance);
//            }
//        }

//        /// <summary>
//        ///   <para>Moves the plane in space by the translation vector.</para>
//        /// </summary>
//        /// <param name="translation">The offset in space to move the plane with.</param>
//        public void Translate(Vector3 translation)
//        {
//            this.m_Distance += Vector3.Dot(this.m_Normal, translation);
//        }

//        /// <summary>
//        ///   <para>Returns a copy of the given plane that is moved in space by the given translation.</para>
//        /// </summary>
//        /// <param name="plane">The plane to move in space.</param>
//        /// <param name="translation">The offset in space to move the plane with.</param>
//        /// <returns>
//        ///   <para>The translated plane.</para>
//        /// </returns>
//        public static Plane Translate(Plane plane, Vector3 translation)
//        {
//            return new Plane(plane.m_Normal, plane.m_Distance += Vector3.Dot(plane.m_Normal, translation));
//        }

//        /// <summary>
//        ///   <para>For a given point returns the closest point on the plane.</para>
//        /// </summary>
//        /// <param name="point">The point to project onto the plane.</param>
//        /// <returns>
//        ///   <para>A point on the plane that is closest to point.</para>
//        /// </returns>
//        public Vector3 ClosestPointOnPlane(Vector3 point)
//        {
//            float num = Vector3.Dot(this.m_Normal, point) + this.m_Distance;
//            return point - this.m_Normal * num;
//        }

//        /// <summary>
//        ///   <para>Returns a signed distance from plane to point.</para>
//        /// </summary>
//        /// <param name="point"></param>
//        public float GetDistanceToPoint(Vector3 point)
//        {
//            return Vector3.Dot(this.m_Normal, point) + this.m_Distance;
//        }

//        /// <summary>
//        ///   <para>Is a point on the positive side of the plane?</para>
//        /// </summary>
//        /// <param name="point"></param>
//        public bool GetSide(Vector3 point)
//        {
//            return (double)Vector3.Dot(this.m_Normal, point) + m_Distance > 0.0;
//        }

//        /// <summary>
//        ///   <para>Are two points on the same side of the plane?</para>
//        /// </summary>
//        /// <param name="inPt0"></param>
//        /// <param name="inPt1"></param>
//        public bool SameSide(Vector3 inPt0, Vector3 inPt1)
//        {
//            float distanceToPoint1 = this.GetDistanceToPoint(inPt0);
//            float distanceToPoint2 = this.GetDistanceToPoint(inPt1);
//            return distanceToPoint1 > 0.0 && distanceToPoint2 > 0.0 || distanceToPoint1 <= 0.0 && distanceToPoint2 <= 0.0;
//        }

//        public bool Raycast(Ray ray, out float enter)
//        {
//            float a = Vector3.Dot(ray.direction, this.m_Normal);
//            float num = -Vector3.Dot(ray.origin, this.m_Normal) - this.m_Distance;
//            if (Mathf.Approximately(a, 0.0f))
//            {
//                enter = 0.0f;
//                return false;
//            }
//            enter = num / a;
//            return enter > 0.0;
//        }

//        public override string ToString()
//        {
//            return UnityString.Format("(normal:({0:F1}, {1:F1}, {2:F1}), distance:{3:F1})", (object)this.m_Normal.x, (object)this.m_Normal.y, (object)this.m_Normal.z, m_Distance);
//        }

//        public string ToString(string format)
//        {
//            return UnityString.Format("(normal:({0}, {1}, {2}), distance:{3})", (object)this.m_Normal.x.ToString(format), (object)this.m_Normal.y.ToString(format), (object)this.m_Normal.z.ToString(format), this.m_Distance.ToString(format));
//        }
//    }
//}
