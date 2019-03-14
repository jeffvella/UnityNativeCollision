using System;
using Unity.Mathematics;

namespace Vella.Common
{
    public struct BoundingSphere : IEquatable<BoundingSphere>
    {
        public float3 center;
        public float radius;

        public BoundingSphere(float3 _center, float _radius)
        {
            center = _center;
            radius = _radius;
        }

        public bool Equals(BoundingSphere other)
        {
            return this.center.x == other.center.x 
                && this.center.y == other.center.y
                && this.center.z == other.center.z
                && this.radius == other.radius;
        }

        public bool IntersectsSphere(BoundingSphere other)
        {
            float addedR = this.radius + other.radius;
            float addedRSq = addedR * addedR;
            float distSq = math.lengthsq(other.center - this.center);
            return addedRSq >= distSq;
        }

        //public bool IntersectsRay(ref SSRay worldSpaceRay, out float distanceAlongRay)
        //{
        //    float distanceToSphereOrigin = OpenTKHelper.DistanceToLine(
        //        worldSpaceRay, this.center, out distanceAlongRay);
        //    return distanceToSphereOrigin <= this.radius;
        //}

        public bool IntersectsAABB(BoundingBox aabb)
        {
            return aabb.IntersectsSphere(this);
        }

        public BoundingBox ToAABB()
        {
            float3 rvec = new float3(radius);
            return new BoundingBox(center - rvec, center + rvec);
        }

        public static BoundingSphere FromAABB(BoundingBox box)
        {
            BoundingSphere result;
            result.center = (box.Min + box.Max) * .5f;
            result.radius = math.distance(result.center, box.Max);
            return result;
        }
    }
}
