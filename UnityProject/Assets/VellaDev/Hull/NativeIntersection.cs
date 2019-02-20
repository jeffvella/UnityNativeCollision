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
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VellaDev.Hull
{
    public struct ClipVertex
    {
        public float3 position;
        public b3FeaturePair featurePair;
        public NativePlane plane;
        public float3 hull2local;
    };

    public struct ClipPlane
    {
        public Vector3 position;
        public NativePlane plane;
        public int edgeId;
    };

    public class NativeIntersection
    {
        public static bool NativeHullHullContact(out NativeManifold output, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            output = new NativeManifold(Allocator.Persistent);

            // todo: collect faces and put them in combined manifold

            for (int i = 0; i < hull2.faceCount; i++)
            {
                var tmp = new NativeManifold(Allocator.Temp);
                b3CreateFaceContact2(ref tmp, i, transform2, hull2, transform1, hull1);
                HullDrawingUtility.DebugDrawManifold(tmp);
                tmp.Dispose();
            }

            for (int i = 0; i < hull1.faceCount; i++)
            {
                var tmp = new NativeManifold(Allocator.Temp);
                b3CreateFaceContact2(ref tmp, i, transform1, hull1, transform2, hull2);
                HullDrawingUtility.DebugDrawManifold(tmp);
                tmp.Dispose();
            }

            return true;
        }

        public static int b3CreateFaceContact2(ref NativeManifold output, int referenceFaceIndex, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            // todo, clean this up, i'm reversing the args here so that closest face is the one clipped instead of incident.

            Debug.Assert(output.IsCreated);

            NativePlane referencePlane = transform1 * hull1.GetPlane(referenceFaceIndex);
            var incidentFaceIndex = ComputeIncidentFaceIndex(referencePlane, transform2, hull2);
            return CreateFaceContact(ref output, incidentFaceIndex, transform2, hull2, referenceFaceIndex, transform1, hull1);
        }

        public static int CreateFaceContact(ref NativeManifold output, int referenceFaceIndex, RigidTransform transform1, NativeHull hull1, int incidentFaceIndex, RigidTransform transform2, NativeHull hull2)
        {
            Debug.Assert(output.IsCreated);

            var refPlane = hull1.GetPlane(referenceFaceIndex);
            NativePlane referencePlane = transform1 * refPlane;

            NativeList<ClipPlane> clippingPlanes = new NativeList<ClipPlane>((int)hull1.faceCount, Allocator.Temp);
            GetClippingPlanes(ref clippingPlanes, referencePlane, referenceFaceIndex, transform1, hull1);

            // Create face polygon.
            NativeList<ClipVertex> incidentPolygon = new NativeList<ClipVertex>((int)hull1.vertexCount, Allocator.Temp);
            ComputeFaceClippingPolygon(ref incidentPolygon, incidentFaceIndex, transform2, hull2);

            // Clip face polygon against the clipping planes.
            for (int i = 0; i < clippingPlanes.Length; ++i)
            {    
                NativeList<ClipVertex> outputPolygon = new NativeList<ClipVertex>((int)hull1.vertexCount, Allocator.Temp);
                Clip(clippingPlanes[i], ref incidentPolygon, ref outputPolygon);

                if (outputPolygon.Length == 0)
                {
                    return -1;
                }

                incidentPolygon.Dispose();
                incidentPolygon = outputPolygon;           
            }

            for (int i = 0; i < incidentPolygon.Length; ++i)
            {
                ClipVertex vertex = incidentPolygon[i];
                float distance = referencePlane.Distance(vertex.position);
                output.Add(vertex.position, distance, new b3ContactID { featurePair = vertex.featurePair });
            }

            clippingPlanes.Dispose();
            incidentPolygon.Dispose();
       
            return incidentFaceIndex;
        }

        /// <summary>
        /// Perform the Sutherland-Hodgman polygon clipping. Since all side planes are pointing outwards the points that are *behind* the plane are kept.       
        /// </summary>
        public static void Clip(ClipPlane clipPlane, ref NativeList<ClipVertex> input, ref NativeList<ClipVertex> output)
        {
            //Debug.Assert(output.IsCreated && output.Length == 0);
            Debug.Assert(input.IsCreated && input.Length != 0);
            ClipVertex vertex1 = input[input.Length - 1];

            float distance1 = clipPlane.plane.Distance(vertex1.position);

            for (int i = 0; i < input.Length; ++i)
            {
                ClipVertex vertex2 = input[i];

                float distance2 = clipPlane.plane.Distance(vertex2.position);
                if (distance1 <= 0 && distance2 <= 0)
                {
                    // Both vertices are behind or lying on the plane -> keep vertex2
                    output.Add(vertex2);
                }
                else if (distance1 <= 0 && distance2 > 0)
                {
                    // vertex1 is behind the plane, vertex2 is in front -> intersection point
                    float fraction = distance1 / (distance1 - distance2);
                    float3 position = vertex1.position + fraction * (vertex2.position - vertex1.position);

                    // Keep intersection point 
                    ClipVertex vertex;
                    vertex.position = position;
                    vertex.featurePair.inEdge1 = -1;
                    vertex.featurePair.inEdge2 = vertex1.featurePair.outEdge2;
                    vertex.featurePair.outEdge1 = (sbyte)clipPlane.edgeId;
                    vertex.featurePair.outEdge2 = -1;
                    vertex.plane = clipPlane.plane;
                    vertex.hull2local = position;
                    output.Add(vertex);
                }
                else if (distance2 <= 0 && distance1 > 0)
                {
                    // vertex2 is behind of the plane, vertex1 is in front -> intersection point
                    float fraction = distance1 / (distance1 - distance2);
                    float3 position = vertex1.position + fraction * (vertex2.position - vertex1.position);

                    // Keep intersection point 
                    ClipVertex vertex;
                    vertex.position = position;
                    vertex.featurePair.inEdge1 = (sbyte)clipPlane.edgeId;
                    vertex.featurePair.outEdge1 = -1;
                    vertex.featurePair.inEdge2 = -1;
                    vertex.featurePair.outEdge2 = vertex1.featurePair.outEdge2;
                    vertex.plane = clipPlane.plane;
                    vertex.hull2local = position;
                    output.Add(vertex);

                    // Keep vertex2 as well
                    output.Add(vertex2);
                }

                // Keep vertex2 as starting vertex for next edge
                vertex1 = vertex2;
                distance1 = distance2;
            }
        }



        /// <summary>
        /// Finds the index to the least parallel face on the other hull.
        /// </summary>
        /// <param name="facePlane"></param>
        /// <param name="transform"></param>
        /// <param name="hull"></param>
        /// <returns></returns>
        public static unsafe int ComputeIncidentFaceIndex(NativePlane facePlane, RigidTransform transform, NativeHull hull)
        {
            int faceIndex = 0;
            float min = math.dot(facePlane.Normal, (transform * hull.GetPlane(faceIndex)).Normal);
            for (int i = 1; i < hull.faceCount; ++i)
            {
                float dot = math.dot(facePlane.Normal, (transform * hull.GetPlane(i)).Normal);
                if (dot < min)
                {
                    min = dot;
                    faceIndex = i;
                }
            }
            return faceIndex;
        }

        /// <summary>
        /// Populates a list with transformed face planes 
        /// </summary>
        public static unsafe void GetClippingPlanes(ref NativeList<ClipPlane> output, NativePlane facePlane, int faceIndex, RigidTransform transform, NativeHull hull)
        {
            Debug.Assert(output.IsCreated);
            for (int i = 0; i < hull.faceCount; i++)
            {
                var p = hull.GetPlane(i);
                output.Add(new ClipPlane
                {
                    plane = transform * p,
                });
            }
        }

        /// <summary>
        /// Populates a list with transformed face vertices.
        /// </summary>
        public static unsafe void ComputeFaceClippingPolygon(ref NativeList<ClipVertex> output, int faceIndex, RigidTransform t, NativeHull hull)
        {
            Debug.Assert(output.IsCreated);

            NativeFace* face = hull.GetFacePtr(faceIndex);
            NativePlane plane = hull.GetPlane(faceIndex);
            NativeHalfEdge* start = hull.GetEdgePtr(face->edge);
            NativeHalfEdge* current = start;

            do
            {
                NativeHalfEdge* twin = hull.GetEdgePtr(current->twin);
                float3 vertex = hull.GetVertex(current->origin);
                float3 P = math.transform(t, vertex);

                ClipVertex clipVertex;
                clipVertex.featurePair.inEdge1 = -1;
                clipVertex.featurePair.outEdge1 = -1;
                clipVertex.featurePair.inEdge2 = (sbyte)current->next;
                clipVertex.featurePair.outEdge2 = (sbyte)twin->twin;
                clipVertex.position = P;
                clipVertex.hull2local = vertex;
                clipVertex.plane = plane;

                output.Add(clipVertex);

                current = hull.GetEdgePtr(current->next);

            } while (current != start);
        }

        public static unsafe void b3CreateEdgeContact(ref NativeManifold output, EdgeQueryResult input, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            Debug.Assert(output.IsCreated);

            ContactPoint cp = default;

            if (input.index1 < 0 || input.index2 < 0)
                return;
         
            NativeHalfEdge* edge1 = hull1.GetEdgePtr(input.index1);
            NativeHalfEdge* twin1 = hull1.GetEdgePtr(edge1->twin);

            float3 P1 = math.transform(transform1, hull1.GetVertex(edge1->origin));
            float3 Q1 = math.transform(transform1, hull1.GetVertex(twin1->origin));
            float3 E1 = Q1 - P1;

            NativeHalfEdge* edge2 = hull2.GetEdgePtr(input.index2);
            NativeHalfEdge* twin2 = hull2.GetEdgePtr(edge2->twin);

            float3 P2 = math.transform(transform1, hull2.GetVertex(edge2->origin));
            float3 Q2 = math.transform(transform1, hull2.GetVertex(twin2->origin));
            float3 E2 = Q2 - P2;

            float3 normal = math.normalize(math.cross(Q1 - P1, Q2 - P2));
            float3 C2C1 = transform2.pos - transform1.pos;

            if (math.dot(normal, C2C1) < 0)
            {
                // Flip
                output.normal = -normal;
                cp.id.featurePair.inEdge1 = (sbyte)input.index2;
                cp.id.featurePair.outEdge1 = (sbyte)(input.index2 + 1);

                cp.id.featurePair.inEdge2 = (sbyte)(input.index1 + 1);
                cp.id.featurePair.outEdge2 = (sbyte)input.index1;
            }
            else
            {
                output.normal = normal;

                cp.id.featurePair.inEdge1 = (sbyte)input.index1;
                cp.id.featurePair.outEdge1 = (sbyte)(input.index1 + 1);

                cp.id.featurePair.inEdge2 = (sbyte)(input.index2 + 1);
                cp.id.featurePair.outEdge2 = (sbyte)input.index2;
            }

            // Compute the closest points between the two edges (center point of penetration)
            b3ClosestPointsSegmentSegment(P1, Q1, P2, Q2, out float3 C1, out float3 C2);

            float3 position = 0.5f * (C1 + C2);

            //// the closest points on each hull
            //cp.positionOnTarget = Math3d.ProjectPointOnLineSegment(P2, Q2, C2);
            //cp.positionOnSource = Math3d.ProjectPointOnLineSegment(P1, Q1, C1);

            cp.penetration = C1 - C2;
            cp.position = position;
            cp.distance = input.distance;

            output.Add(cp);
        }

        public static void b3ClosestPointsSegmentSegment(float3 P1, float3 Q1, float3 P2, float3 Q2, out float3 C1, out float3 C2)
        {
            float3 P2P1 = P1 - P2;

            float3 E1 = Q1 - P1;
            float3 E2 = Q2 - P2;

            float D1 = math.lengthsq(E1);
            float D2 = math.lengthsq(E2);

            float D12 = math.dot(E1, E2);
            float DE1P1 = math.dot(E1, P2P1);
            float DE2P1 = math.dot(E2, P2P1);

            float DNM = D1 * D2 - D12 * D12;

            // Get the two fractions.
            float F1 = (D12 * DE2P1 - DE1P1 * D2) / DNM;
            float F2 = (D12 * F1 + DE2P1) / D2;

            C1 = P1 + F1 * E1;
            C2 = P2 + F2 * E2;
        }

    }
}