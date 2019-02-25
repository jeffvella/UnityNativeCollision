/*
* This software is provided 'as-is', without any express or implied
* warranty.  In no event will the authors be held liable for any damages
* arising from the use of this software.
* Permission is granted to anyone to use this software for any purpose,
* including commercial applications, and to alter it and redistribute it
* freely, subject to the following restrictions:
* 1. The origin of this software must not be misrepresented; you must not
* claim that you wrote the original software. If you use this software
* in a product, an acknowledgment in the product documentation would be
* appreciated but is not required.
* 2. Altered source versions must be plainly marked as such, and must not be
* misrepresented as being the original software.
* 3. This notice may not be removed or altered from any source distribution. 
* https://en.wikipedia.org/wiki/Zlib_License
*/

/* Acknowledgments:
 * This work is derived from BounceLite by Irlan Robson (zLib License): 
 * https://github.com/irlanrobson/bounce_lite 
 * The optimized SAT and clipping is based on the 2013 GDC presentation by Dirk Gregorius 
 * and his forum posts about Valve's Rubikon physics engine:
 * https://www.gdcvault.com/play/1017646/Physics-for-Game-Programmers-The
 * https://www.gamedev.net/forums/topic/692141-collision-detection-why-gjk/?do=findComment&comment=5356490 
 * http://www.gamedev.net/topic/667499-3d-sat-problem/ 
 */

using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace VellaDev.Hull
{
    public struct FaceQueryResult
    {
        public int Index;
        public float Distance;
    };

    public struct EdgeQueryResult
    {
        public int Index1;
        public int Index2;
        public float Distance;
    };

    public struct CollisionInfo
    {
        public bool IsColliding;
        public FaceQueryResult Face1;
        public FaceQueryResult Face2;
        public EdgeQueryResult Edge;
    }

    public class NativeCollision
    {
        public static bool IsCollision(RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            FaceQueryResult faceQuery;

            QueryFaceDistance(out faceQuery, transform1, hull1, transform2, hull2);
            if (faceQuery.Distance > 0)
                return false;

            QueryFaceDistance(out faceQuery, transform2, hull2, transform1, hull1);
            if (faceQuery.Distance > 0)
                return false;

            QueryEdgeDistance(out EdgeQueryResult edgeQuery, transform1, hull1, transform2, hull2);
            if (edgeQuery.Distance > 0)
                return false;

            return true;
        }

        public static CollisionInfo GetDebugCollisionInfo(RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            CollisionInfo result = default;
            QueryFaceDistance(out result.Face1, transform1, hull1, transform2, hull2);
            QueryFaceDistance(out result.Face2, transform2, hull2, transform1, hull1);
            QueryEdgeDistance(out result.Edge, transform1, hull1, transform2, hull2);
            result.IsColliding = result.Face1.Distance < 0 && result.Face2.Distance < 0 && result.Edge.Distance < 0;
            return result;
        }

        public static unsafe void QueryFaceDistance(out FaceQueryResult result, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            // Perform computations in the local space of the second hull.
            RigidTransform transform = math.mul(math.inverse(transform2), transform1);

            result.Distance = -float.MaxValue;
            result.Index = -1;

            for (int i = 0; i < hull1.FaceCount; ++i)
            {
                NativePlane plane = transform * hull1.GetPlane(i);
                float3 support = hull2.GetSupport(-plane.Normal);
                float distance = plane.Distance(support);

                if (distance > result.Distance)
                {
                    result.Distance = distance;
                    result.Index = i;
                }
            }
        }

        public static unsafe void QueryEdgeDistance(out EdgeQueryResult result, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            // Perform computations in the local space of the second hull.
            RigidTransform transform = math.mul(math.inverse(transform2), transform1);

            float3 C1 = transform.pos;

            result.Distance = -float.MaxValue;
            result.Index1 = -1;
            result.Index2 = -1;

            for (int i = 0; i < hull1.EdgeCount; i += 2)
            {
                NativeHalfEdge* edge1 = hull1.GetEdgePtr(i);
                NativeHalfEdge* twin1 = hull1.GetEdgePtr(i + 1);

                Debug.Assert(edge1->Twin == i + 1 && twin1->Twin == i);

                float3 P1 = math.transform(transform, hull1.GetVertex(edge1->Origin));
                float3 Q1 = math.transform(transform, hull1.GetVertex(twin1->Origin));
                float3 E1 = Q1 - P1;

                float3 U1 = math.rotate(transform, hull1.GetPlane(edge1->Face).Normal);
                float3 V1 = math.rotate(transform, hull1.GetPlane(twin1->Face).Normal);

                for (int j = 0; j < hull2.EdgeCount; j += 2)
                {
                    NativeHalfEdge* edge2 = hull2.GetEdgePtr(j);
                    NativeHalfEdge* twin2 = hull2.GetEdgePtr(j + 1);

                    Debug.Assert(edge2->Twin == j + 1 && twin2->Twin == j);

                    float3 P2 = hull2.GetVertex(edge2->Origin);
                    float3 Q2 = hull2.GetVertex(twin2->Origin);
                    float3 E2 = Q2 - P2;
                   
                    float3 U2 = hull2.GetPlane(edge2->Face).Normal;
                    float3 V2 = hull2.GetPlane(twin2->Face).Normal;

                    if (IsMinkowskiFace(U1, V1, -E1, -U2, -V2, -E2))
                    {
                        float distance = Project(P1, E1, P2, E2, C1);
                        if (distance > result.Distance)
                        {
                            result.Index1 = i;
                            result.Index2 = j;
                            result.Distance = distance;
                        }
                    }
                }
            }
        }

        
        public static bool IsMinkowskiFace(float3 A, float3 B, float3 B_x_A, float3 C, float3 D, float3 D_x_C)
        {
            // If an edge pair doesn't build a face on the MD then it isn't a supporting edge.

            // Test if arcs AB and CD intersect on the unit sphere 
            float CBA = math.dot(C, B_x_A);
            float DBA = math.dot(D, B_x_A);
            float ADC = math.dot(A, D_x_C);
            float BDC = math.dot(B, D_x_C);

            return CBA * DBA < 0 &&
                   ADC * BDC < 0 &&
                   CBA * BDC > 0;
        }

        public static float Project(float3 P1, float3 E1, float3 P2, float3 E2, float3 C1)
        {
            // The given edge pair must create a face on the MD.

            // Compute search direction.
            float3 E1_x_E2 = math.cross(E1, E2);

            // Skip if the edges are significantly parallel to each other.
            float kTol = 0.005f;
            float L = math.length(E1_x_E2);
            if (L < kTol * math.sqrt(math.lengthsq(E1) * math.lengthsq(E2)))
            {
                return -float.MaxValue;
            }

            // Assure the normal points from hull1 to hull2.
            float3 N = (1 / L) * E1_x_E2;
            if (math.dot(N, P1 - C1) < 0)
            {
                N = -N;
            }

            // Return the signed distance.
            return math.dot(N, P2 - P1);
        }
    }

}