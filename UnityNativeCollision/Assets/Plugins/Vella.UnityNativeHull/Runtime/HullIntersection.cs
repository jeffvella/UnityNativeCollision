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
using Vella.Common;

namespace Vella.UnityNativeHull
{
    public struct ClipVertex
    {
        public float3 position;
        public FeaturePair featurePair;
        public NativePlane plane;
        public float3 hull2local;
    };

    public struct ClipPlane
    {
        public Vector3 position;
        public NativePlane plane;
        public int edgeId;
    };

    public class HullIntersection
    {
        public static bool DrawNativeHullHullIntersection(RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            for (int i = 0; i < hull2.FaceCount; i++)
            {
                var tmp = new NativeManifold(Allocator.Temp);

                ClipFace(ref tmp, i, transform2, hull2, transform1, hull1);

                HullDrawingUtility.DebugDrawManifold(tmp);
                tmp.Dispose();
            }

            for (int i = 0; i < hull1.FaceCount; i++)
            {
                var tmp = new NativeManifold(Allocator.Temp);

                ClipFace(ref tmp, i, transform1, hull1, transform2, hull2);

                HullDrawingUtility.DebugDrawManifold(tmp);
                tmp.Dispose();
            }
            return true;
        }

        private static void ClipFace(ref NativeManifold tmp, int i, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            NativePlane plane = transform1 * hull1.GetPlane(i);
            var incidentFaceIndex = ComputeIncidentFaceIndex(plane, transform2, hull2);
            ClipFaceAgainstAnother(ref tmp, incidentFaceIndex, transform2, hull2, i, transform1, hull1);
        }

        public static int ClipFaceAgainstAnother(ref NativeManifold output, int referenceFaceIndex, RigidTransform transform1, NativeHull hull1, int incidentFaceIndex, RigidTransform transform2, NativeHull hull2)
        {
            Debug.Assert(output.IsCreated);

            var refPlane = hull1.GetPlane(referenceFaceIndex);
            NativePlane referencePlane = transform1 * refPlane;

            NativeBuffer<ClipPlane> clippingPlanes = new NativeBuffer<ClipPlane>(hull1.FaceCount, Allocator.Temp);

            // Get every plane on the other polygon
            GetClippingPlanes(ref clippingPlanes, referencePlane, referenceFaceIndex, transform1, hull1);

            // Create face polygon.
            NativeBuffer<ClipVertex> incidentPolygon = new NativeBuffer<ClipVertex>(hull1.VertexCount, Allocator.Temp);
            ComputeFaceClippingPolygon(ref incidentPolygon, incidentFaceIndex, transform2, hull2);

            // Clip face polygon against the clipping planes.
            for (int i = 0; i < clippingPlanes.Length; ++i)
            {
                NativeBuffer<ClipVertex> outputPolygon = new NativeBuffer<ClipVertex>(math.max(hull1.VertexCount, hull2.VertexCount), Allocator.Temp);

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
                output.Add(vertex.position, distance, new ContactID { FeaturePair = vertex.featurePair });
            }

            clippingPlanes.Dispose();
            incidentPolygon.Dispose();
       
            return incidentFaceIndex;
        }

        /// <summary>
        /// Perform the Sutherland-Hodgman polygon clipping. Since all side planes are pointing outwards the points that are *behind* the plane are kept.       
        /// </summary>
        public static void Clip(ClipPlane clipPlane, ref NativeBuffer<ClipVertex> input, ref NativeBuffer<ClipVertex> output)
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
                    vertex.featurePair.InEdge1 = -1;
                    vertex.featurePair.InEdge2 = vertex1.featurePair.OutEdge2;
                    vertex.featurePair.OutEdge1 = (sbyte)clipPlane.edgeId;
                    vertex.featurePair.OutEdge2 = -1;
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
                    vertex.featurePair.InEdge1 = (sbyte)clipPlane.edgeId;
                    vertex.featurePair.OutEdge1 = -1;
                    vertex.featurePair.InEdge2 = -1;
                    vertex.featurePair.OutEdge2 = vertex1.featurePair.OutEdge2;
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
            for (int i = 1; i < hull.FaceCount; ++i)
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
        public static unsafe void GetClippingPlanes(ref NativeBuffer<ClipPlane> output, NativePlane facePlane, int faceIndex, RigidTransform transform, NativeHull hull)
        {
            Debug.Assert(output.IsCreated);

            for (int i = 0; i < hull.FaceCount; i++)
            {
                var p = hull.GetPlane(i);
                output.Add(new ClipPlane
                {
                    plane = transform * p,
                });
            }
        }


        public static unsafe void GetFaceSidePlanes(ref NativeBuffer<ClipPlane> output, NativePlane facePlane, int faceIndex, RigidTransform transform, NativeHull hull)
        { 
            NativeHalfEdge* start = hull.GetEdgePtr(hull.GetFacePtr(faceIndex)->Edge);
            NativeHalfEdge* current = start;
	        do
            {
                NativeHalfEdge* twin = hull.GetEdgePtr(current->Twin);
                float3 P = math.transform(transform, hull.GetVertex(current->Origin));
                float3 Q = math.transform(transform, hull.GetVertex(twin->Origin));

                ClipPlane clipPlane = default;
                clipPlane.edgeId = twin->Twin; //edge ID.
		        clipPlane.plane.Normal = math.normalize(math.cross(Q - P, facePlane.Normal));
		        clipPlane.plane.Offset = math.dot(clipPlane.plane.Normal, P);            
                output.Add(clipPlane);

		        current = hull.GetEdgePtr(current->Next);
            }
            while (current != start);
        }

        /// <summary>
        /// Populates a list with transformed face vertices.
        /// </summary>
        public static unsafe void ComputeFaceClippingPolygon(ref NativeBuffer<ClipVertex> output, int faceIndex, RigidTransform t, NativeHull hull)
        {
            Debug.Assert(output.IsCreated);

            NativeFace* face = hull.GetFacePtr(faceIndex);
            NativePlane plane = hull.GetPlane(faceIndex);
            NativeHalfEdge* start = hull.GetEdgePtr(face->Edge);
            NativeHalfEdge* current = start;

            do
            {
                NativeHalfEdge* twin = hull.GetEdgePtr(current->Twin);
                float3 vertex = hull.GetVertex(current->Origin);
                float3 P = math.transform(t, vertex);

                ClipVertex clipVertex;
                clipVertex.featurePair.InEdge1 = -1;
                clipVertex.featurePair.OutEdge1 = -1;
                clipVertex.featurePair.InEdge2 = (sbyte)current->Next;
                clipVertex.featurePair.OutEdge2 = (sbyte)twin->Twin;
                clipVertex.position = P;
                clipVertex.hull2local = vertex;
                clipVertex.plane = plane;

                output.Add(clipVertex);

                current = hull.GetEdgePtr(current->Next);

            } while (current != start);
        }

        public static unsafe void CreateEdgeContact(ref NativeManifold output, EdgeQueryResult input, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            Debug.Assert(output.IsCreated);

            ContactPoint cp = default;

            if (input.Index1 < 0 || input.Index2 < 0)
                return;
         
            NativeHalfEdge* edge1 = hull1.GetEdgePtr(input.Index1);
            NativeHalfEdge* twin1 = hull1.GetEdgePtr(edge1->Twin);

            float3 P1 = math.transform(transform1, hull1.GetVertex(edge1->Origin));
            float3 Q1 = math.transform(transform1, hull1.GetVertex(twin1->Origin));
            float3 E1 = Q1 - P1;

            NativeHalfEdge* edge2 = hull2.GetEdgePtr(input.Index2);
            NativeHalfEdge* twin2 = hull2.GetEdgePtr(edge2->Twin);

            float3 P2 = math.transform(transform1, hull2.GetVertex(edge2->Origin));
            float3 Q2 = math.transform(transform1, hull2.GetVertex(twin2->Origin));
            float3 E2 = Q2 - P2;

            float3 normal = math.normalize(math.cross(Q1 - P1, Q2 - P2));
            float3 C2C1 = transform2.pos - transform1.pos;

            if (math.dot(normal, C2C1) < 0)
            {
                // Flip
                output.Normal = -normal;
                cp.Id.FeaturePair.InEdge1 = input.Index2;
                cp.Id.FeaturePair.OutEdge1 = input.Index2 + 1;

                cp.Id.FeaturePair.InEdge2 = input.Index1 + 1;
                cp.Id.FeaturePair.OutEdge2 = input.Index1;
            }
            else
            {
                output.Normal = normal;

                cp.Id.FeaturePair.InEdge1 = input.Index1;
                cp.Id.FeaturePair.OutEdge1 = input.Index1 + 1;

                cp.Id.FeaturePair.InEdge2 = input.Index2 + 1;
                cp.Id.FeaturePair.OutEdge2 = input.Index2;
            }

            // Compute the closest points between the two edges (center point of penetration)
            ClosestPointsSegmentSegment(P1, Q1, P2, Q2, out float3 C1, out float3 C2);

            float3 position = 0.5f * (C1 + C2);

            //// the closest points on each hull
            //cp.positionOnTarget = Math3d.ProjectPointOnLineSegment(P2, Q2, C2);
            //cp.positionOnSource = Math3d.ProjectPointOnLineSegment(P1, Q1, C1);

            cp.Penetration = C1 - C2;
            cp.Position = position;
            cp.Distance = input.Distance;

            output.Add(cp);
        }

        public static void ClosestPointsSegmentSegment(float3 P1, float3 Q1, float3 P2, float3 Q2, out float3 C1, out float3 C2)
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

        public static bool NativeHullHullContact(ref NativeManifold result, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            FaceQueryResult faceQuery1;
            HullCollision.QueryFaceDistance(out faceQuery1, transform1, hull1, transform2, hull2);
            if (faceQuery1.Distance > 0)
            {
                return false;
            }

            FaceQueryResult faceQuery2;
            HullCollision.QueryFaceDistance(out faceQuery2, transform2, hull2, transform1, hull1);
            if (faceQuery2.Distance > 0)
            {
                return false;
            }
         
            HullCollision.QueryEdgeDistance(out EdgeQueryResult edgeQuery, transform1, hull1, transform2, hull2);
            if (edgeQuery.Distance > 0)
            {
                return false;
            }

            float kRelEdgeTolerance = 0.90f; //90%
            float kRelFaceTolerance = 0.95f; //95%
            float kAbsTolerance = 0.5f * 0.005f;

            // Favor face contacts over edge contacts.
            float maxFaceSeparation = math.max(faceQuery1.Distance, faceQuery2.Distance);
            if (edgeQuery.Distance > kRelEdgeTolerance * maxFaceSeparation + kAbsTolerance)
            {
                CreateEdgeContact(ref result, edgeQuery, transform1, hull1, transform2, hull2);
            }
            else
            {
                // Favor first hull face to avoid face flip-flops. 
                if (faceQuery2.Distance > kRelFaceTolerance * faceQuery1.Distance + kAbsTolerance)
                {
                    // 2 = reference, 1 = incident.
                    CreateFaceContact(ref result, faceQuery2, transform2, hull2, transform1, hull1, true);
                }
                else
                {
                    // 1 = reference, 2 = incident.
                    CreateFaceContact(ref result, faceQuery1, transform1, hull1, transform2, hull2, false);
                }
            }

            return true;
        }


        public unsafe static void CreateFaceContact(ref NativeManifold output, FaceQueryResult input, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2, bool flipNormal)
        {
            var refPlane = hull1.GetPlane(input.Index);
            NativePlane referencePlane = transform1 * refPlane;

            var clippingPlanesStackPtr = stackalloc ClipPlane[hull1.FaceCount];
            var clippingPlanes = new NativeBuffer<ClipPlane>(clippingPlanesStackPtr, hull1.FaceCount);
            
            //NativeList<ClipPlane> clippingPlanes = new NativeList<ClipPlane>((int)hull1.FaceCount, Allocator.Temp);

            // Find only the side planes of the reference face
            GetFaceSidePlanes(ref clippingPlanes, referencePlane, input.Index, transform1, hull1);

            var incidentPolygonStackPtr = stackalloc ClipVertex[hull1.FaceCount];
            var incidentPolygon = new NativeBuffer<ClipVertex>(incidentPolygonStackPtr, hull1.VertexCount);

            var incidentFaceIndex = ComputeIncidentFaceIndex(referencePlane, transform2, hull2);
            ComputeFaceClippingPolygon(ref incidentPolygon, incidentFaceIndex, transform2, hull2);

            //HullDrawingUtility.DrawFaceWithOutline(incidentFaceIndex, transform2, hull2, Color.yellow.ToOpacity(0.3f));

            var outputPolygonStackPtr = stackalloc ClipVertex[hull1.FaceCount];

            // Clip face polygon against the clipping planes.
            for (int i = 0; i < clippingPlanes.Length; ++i)
            {
                var outputPolygon = new NativeBuffer<ClipVertex>(outputPolygonStackPtr, hull1.FaceCount);

                Clip(clippingPlanes[i], ref incidentPolygon, ref outputPolygon);

                if (outputPolygon.Length == 0)
                {
                    return;
                }
                
                incidentPolygon = outputPolygon;
            }

            // Get all contact points below reference face.
            for (int i = 0; i < incidentPolygon.Length; ++i)
            {
                ClipVertex vertex = incidentPolygon[i];
                float distance = referencePlane.Distance(vertex.position);

                if (distance <= 0)
                {
                    // Below reference plane -> position constraint violated.
                    ContactID id = default;
                    id.FeaturePair = vertex.featurePair;                 

                    if (flipNormal)
                    {
                        output.Normal = -referencePlane.Normal;
                        Swap(id.FeaturePair.InEdge1, id.FeaturePair.InEdge2);
                        Swap(id.FeaturePair.OutEdge1, id.FeaturePair.OutEdge2);
                    }
                    else
                    {
                        output.Normal = referencePlane.Normal;                        
                    }
                    
                    // Project clipped point onto reference plane.
                    float3 position = referencePlane.ClosestPoint(vertex.position);
                    // Add point and distance to the plane to the manifold.
                    output.Add(position, distance, id);
                }
            }

            //clippingPlanes.Dispose();
            //incidentPolygon.Dispose();
        }
      
        public static void Swap<T>(T a, T b)
        {
            T tmp = a;
            a = b;
            b = tmp;
        }

    }
}