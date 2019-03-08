// Copyright(C) David W. Jeske, 2013
// Released to the public domain. 

using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace SimpleScene
{
    public struct BoundingBox : IEquatable<BoundingBox>
    {
        public float3 Min;
        public float3 Max;

        public BoundingBox(float min = float.PositiveInfinity, float max = float.NegativeInfinity)
        {
            Min = new float3(min);
            Max = new float3(max);
        }

        public BoundingBox(float3 min, float3 max)
        {
            Min = min;
            Max = max;
        }

        public void Combine(ref BoundingBox other)
        {
            Min = math.min(Min, other.Min);
            Max = math.max(Max, other.Max);
        }

        public bool IntersectsSphere(float3 origin, float radius)
        {
            if (
                (origin.x + radius < Min.x) ||
                (origin.y + radius < Min.y) ||
                (origin.z + radius < Min.z) ||
                (origin.x - radius > Max.x) ||
                (origin.y - radius > Max.y) ||
                (origin.z - radius > Max.z)
               )
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool IntersectsSphere(BoundingSphere sphere)
        {
            return IntersectsSphere(sphere.center, sphere.radius);
        }

        public bool IntersectsAABB(BoundingBox box)
        {
            return ((Max.x > box.Min.x) && (Min.x < box.Max.x) &&
                     (Max.y > box.Min.y) && (Min.y < box.Max.y) &&
                     (Max.z > box.Min.z) && (Min.z < box.Max.z));
        }

        public bool Equals(BoundingBox other)
        {
            return
                (Min.x == other.Min.x) &&
                (Min.y == other.Min.y) &&
                (Min.z == other.Min.z) &&
                (Max.x == other.Max.x) &&
                (Max.y == other.Max.y) &&
                (Max.z == other.Max.z);
        }

        public void UpdateMin(float3 localMin)
        {
            Min = math.min(Min, localMin);
        }

        public void UpdateMax(float3 localMax)
        {
            Max = math.max(Max, localMax);
        }

        public float3 Center()
        {
            return (Min + Max) / 2f;
        }

        public float3 Diff()
        {
            return Max - Min;
        }

        public BoundingSphere ToSphere()
        {
            float r = math.length(Diff() + 0.001f) / 2f;
            return new BoundingSphere(Center(), r);
        }

        public void Encapsulate(float3 b)
        {
            UpdateMin(b);
            UpdateMax(b);
        }

        internal void ExpandToFit(BoundingBox b)
        {
            if (b.Min.x < this.Min.x) { this.Min.x = b.Min.x; }
            if (b.Min.y < this.Min.y) { this.Min.y = b.Min.y; }
            if (b.Min.z < this.Min.z) { this.Min.z = b.Min.z; }

            if (b.Max.x > this.Max.x) { this.Max.x = b.Max.x; }
            if (b.Max.y > this.Max.y) { this.Max.y = b.Max.y; }
            if (b.Max.z > this.Max.z) { this.Max.z = b.Max.z; }
        }

        public BoundingBox ExpandedBy(BoundingBox b)
        {
            BoundingBox newbox = this;
            if (b.Min.x < newbox.Min.x) { newbox.Min.x = b.Min.x; }
            if (b.Min.y < newbox.Min.y) { newbox.Min.y = b.Min.y; }
            if (b.Min.z < newbox.Min.z) { newbox.Min.z = b.Min.z; }

            if (b.Max.x > newbox.Max.x) { newbox.Max.x = b.Max.x; }
            if (b.Max.y > newbox.Max.y) { newbox.Max.y = b.Max.y; }
            if (b.Max.z > newbox.Max.z) { newbox.Max.z = b.Max.z; }

            return newbox;
        }

        public void ExpandBy(BoundingBox b)
        {
            this = this.ExpandedBy(b);
        }

        public static BoundingBox FromSphere(float3 pos, float radius)
        {
            BoundingBox box;
            box.Min.x = pos.x - radius;
            box.Max.x = pos.x + radius;
            box.Min.y = pos.y - radius;
            box.Max.y = pos.y + radius;
            box.Min.z = pos.z - radius;
            box.Max.z = pos.z + radius;
            return box;
        }

        private static readonly float4[] c_homogenousCorners = {
            new float4(-1f, -1f, -1f, 1f),
            new float4(-1f, 1f, -1f, 1f),
            new float4(1f, 1f, -1f, 1f),
            new float4(1f, -1f, -1f, 1f),
            new float4(-1f, -1f, 1f, 1f),
            new float4(-1f, 1f, 1f, 1f),
            new float4(1f, 1f, 1f, 1f),
            new float4(1f, -1f, 1f, 1f),
        };

        public static BoundingBox FromFrustum(ref float4x4 axisTransform, ref float4x4 modelViewProj)
        {
            BoundingBox ret = new BoundingBox(float.PositiveInfinity, float.NegativeInfinity);
            float4x4 inverse = math.inverse(modelViewProj);
            for (int i = 0; i < c_homogenousCorners.Length; ++i)
            {
                float4 corner = math.mul(c_homogenousCorners[i], inverse);
                float3 transfPt = math.transform(axisTransform, corner.xyz / corner.w);
                ret.UpdateMin(transfPt);
                ret.UpdateMax(transfPt);
            }
            return ret;
        }

    }
}