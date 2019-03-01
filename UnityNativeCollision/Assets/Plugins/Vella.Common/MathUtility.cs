using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;

namespace Vella.Common
{
    public static class MathUtility
    {
        /// <summary>
        /// Find point on a line with start/end positions
        /// </summary>
        public static float3 ProjectPointOnLineSegment(float3 linePoint1, float3 linePoint2, float3 point)
        {
            float3 vector = linePoint2 - linePoint1;
            float3 projectedPoint = ProjectPointOnLine(linePoint1, math.normalize(vector), point);
            int side = PointOnWhichSideOfLineSegment(linePoint1, linePoint2, projectedPoint);
            switch (side)
            {
                case 0:
                    return projectedPoint;
                case 1:
                    return linePoint1;
                case 2:
                    return linePoint2;
            }
            return default;
        }

        /// <summary>
        /// Find point on infinate line (result is not clipped to a line segment)
        /// </summary>
        public static float3 ProjectPointOnLine(float3 linePoint, float3 lineVec, float3 point)
        {
            return linePoint + lineVec * math.dot(point - linePoint, lineVec);
        }


        /// <summary>
        /// This function finds out on which side of a line segment the point is located.
        /// </summary>
        public static int PointOnWhichSideOfLineSegment(float3 linePoint1, float3 linePoint2, float3 point)
        {
            //Returns 0 if point is on the line segment.
            //Returns 1 if point is outside of the line segment and located on the side of linePoint1.
            //Returns 2 if point is outside of the line segment and located on the side of linePoint2.

            float3 lineVec = linePoint2 - linePoint1;
            float3 pointVec = point - linePoint1;
            return math.dot(pointVec, lineVec) <= 0 ? 1 : math.length(pointVec) <= math.length(lineVec) ? 0 : 2;
        }
    }
}
