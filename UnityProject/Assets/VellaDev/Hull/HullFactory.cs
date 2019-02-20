using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace VellaDev.Hull
{
    public class HullFactory
    {
        public static unsafe NativeHull CreateBox(float3 scale)
        {
            float3[] cubeVertices =
            {
                new float3(1, 1, -1),
                new float3(-1, 1, -1),
                new float3(-1, -1, -1),
                new float3(1, -1, -1),
                new float3(1, 1, 1),
                new float3(-1, 1, 1),
                new float3(-1, -1, 1),
                new float3(1, -1, 1),
            };

            for (int i = 0; i < 8; ++i)
            {
                cubeVertices[i].x *= scale.x;
                cubeVertices[i].y *= scale.y;
                cubeVertices[i].z *= scale.z;
            }

            int* left = stackalloc int[] { 1, 2, 6, 5 };
            int* right = stackalloc int[] { 4, 7, 3, 0 };
            int* down = stackalloc int[] { 3, 7, 6, 2 };
            int* up = stackalloc int[] { 0, 1, 5, 4 };
            int* back = stackalloc int[] { 4, 5, 6, 7 };
            int* front = stackalloc int[] { 0, 3, 2, 1 };

            NativeFaceDef[] boxFaces =
            {
                new NativeFaceDef {vertexCount = 4, vertices = left},
                new NativeFaceDef {vertexCount = 4, vertices = right},
                new NativeFaceDef {vertexCount = 4, vertices = down},
                new NativeFaceDef {vertexCount = 4, vertices = up},
                new NativeFaceDef {vertexCount = 4, vertices = back},
                new NativeFaceDef {vertexCount = 4, vertices = front},
            };

            var result = new NativeHull();

            using (var boxFacesNative = new NativeArray<NativeFaceDef>(boxFaces, Allocator.Temp))
            using (var cubeVertsNative = new NativeArray<float3>(cubeVertices, Allocator.Temp))
            {
                NativeHullDef hullDef;
                hullDef.vertexCount = 8;
                hullDef.verticesNative = cubeVertsNative;
                hullDef.faceCount = 6;
                hullDef.facesNative = boxFacesNative;
                SetFromFaces(ref result, hullDef);
            }

            result._isCreated = 1;
            return result;
        }

        public static unsafe NativeHull CreateFromMesh(Mesh mesh)
        {
            var faces = new List<DetailedFaceDef>();
            var verts = mesh.vertices.Select(RoundVertex).ToArray();
            var uniqueVerts = verts.Distinct().ToList();
            var indices = mesh.triangles;

            // Create faces from Triangles and collapse multiple vertices with same position into shared vertices.
            for (int i = 0; i < mesh.triangles.Length; i = i + 3)
            {
                var idx1 = i;
                var idx2 = i + 1;
                var idx3 = i + 2;

                Vector3 p1 = verts[indices[idx1]];
                Vector3 p2 = verts[indices[idx2]];
                Vector3 p3 = verts[indices[idx3]];

                var normal = normalize(cross(p3 - p2, p1 - p2));

                // Round normal so that faces with only slight variances can be grouped properly together.
                var roundedNormal = RoundVertex(normal);

                faces.Add(new DetailedFaceDef
                {
                    Center = ((p1 + p2 + p3) / 3),
                    Normal = roundedNormal,
                    Verts = new List<float3> { p1, p2, p3 },
                    Indices = new List<int>
                    {
                        uniqueVerts.IndexOf(p1),
                        uniqueVerts.IndexOf(p2),
                        uniqueVerts.IndexOf(p3)
                    }
                });
            }

            var faceDefs = new List<NativeFaceDef>();
            var orphanIndices = new HashSet<int>();

            // Merge all faces with the same normal and shared vertex         
            var mergedFaces = GroupBySharedVertex(GroupByNormal(faces));

            foreach (var faceGroup in mergedFaces)
            {
                var indicesFromMergedFaces = faceGroup.SelectMany(face => face.Indices).ToArray();

                // Collapse points inside the new combined face by using only the border vertices.
                var border = PolygonPerimeter.CalculatePerimeter(indicesFromMergedFaces, ref uniqueVerts);
                var borderIndices = border.Select(b => b.EndIndex).ToArray();

                foreach(var idx in indicesFromMergedFaces.Except(borderIndices))
                {
                    orphanIndices.Add(idx);
                }
  
                var v = stackalloc int[borderIndices.Length];
                int max = 0;     
                for (int i = 0; i < borderIndices.Length; i++)
                {
                    var idx = borderIndices[i];
                    if (idx > max)
                        max = idx;
                    v[i] = idx;
                }                

                faceDefs.Add(new NativeFaceDef
                {
                    highestIndex = max,
                    vertexCount = borderIndices.Length,
                    vertices = v,
                });
            }

            // Remove vertices with no edges connected to them and fix all impacted face vertex references.
            foreach (var orphanIdx in orphanIndices.OrderByDescending(i => i))
            {
                uniqueVerts.RemoveAt(orphanIdx);

                foreach(var face in faceDefs.Where(f => f.highestIndex >= orphanIdx))
                {
                    for (int i = 0; i < face.vertexCount; i++)
                    {
                        var faceVertIdx = face.vertices[i];
                        if (faceVertIdx >= orphanIdx)
                        {
                            face.vertices[i] = --faceVertIdx;
                        }
                    }
                }
            }

            var result = new NativeHull();

            using (var faceNative = new NativeArray<NativeFaceDef>(faceDefs.ToArray(), Allocator.Temp))
            using (var vertsNative = new NativeArray<float3>(uniqueVerts.ToArray(), Allocator.Temp))
            {

                NativeHullDef hullDef;
                hullDef.vertexCount = vertsNative.Length;
                hullDef.verticesNative = vertsNative;
                hullDef.faceCount = faceNative.Length;
                hullDef.facesNative = faceNative;
                SetFromFaces(ref result, hullDef);
            }

            result._isCreated = 1;
            return result;
        }


        public unsafe static void SetFromFaces(ref NativeHull hull, NativeHullDef def)
        {
            Debug.Assert(def.faceCount > 0);
            Debug.Assert(def.vertexCount > 0);

            hull.vertexCount = def.vertexCount;
            var arr = def.verticesNative.ToArray();

            hull.verticesNative = new NativeArrayNoLeakDetection<float3>(arr, Allocator.Persistent);
            hull.vertices = (float3*)hull.verticesNative.GetUnsafePtr();
            hull.faceCount = def.faceCount;
            hull.facesNative = new NativeArrayNoLeakDetection<NativeFace>(hull.faceCount, Allocator.Persistent);
            hull.faces = (NativeFace*)hull.facesNative.GetUnsafePtr();

            // Initialize all faces by assigning -1 to each edge reference.
            for (int k = 0; k < def.faceCount; ++k)
            {               
                NativeFace* f = hull.faces + k;                
                f->edge = -1;
            }

            CreateFacesPlanes(ref hull, ref def);

            var edgeMap = new Dictionary<(int v1, int v2), int>();
            var edgesList = new NativeHalfEdge[10000]; // todo lol

            // Loop through all faces.
            for (int i = 0; i < def.faceCount; ++i)
            {
                NativeFaceDef face = def.facesNative[i];
                int vertCount = face.vertexCount;

                Debug.Assert(vertCount >= 3);

                int* vertices = face.vertices;

                var faceHalfEdges = new List<int>();

                // Loop through all face edges.
                for (int j = 0; j < vertCount; ++j)
                {
                    int v1 = vertices[j];
                    int v2 = j + 1 < vertCount ? vertices[j + 1] : vertices[0];

                    bool edgeFound12 = edgeMap.TryGetValue((v1, v2), out int iter12);
                    bool edgeFound21 = edgeMap.ContainsKey((v2, v1));

                    Debug.Assert(edgeFound12 == edgeFound21);

                    if (edgeFound12)
                    {
                        // The edge is shared by two faces.
                        int e12 = iter12;

                        // Link adjacent face to edge.
                        if (edgesList[e12].face == -1)
                        {
                            edgesList[e12].face = i;
                        }
                        else
                        {
                            throw new Exception("Two shared edges can't have the same vertices in the same order");        
                        }

                        if (hull.faces[i].edge == -1)
                        {
                            hull.faces[i].edge = e12;
                        }

                        faceHalfEdges.Add(e12);
                    }
                    else
                    {
                        // The next edge of the current half edge in the array is the twin edge.
                        int e12 = hull.edgeCount++;
                        int e21 = hull.edgeCount++;

                        if (hull.faces[i].edge == -1)
                        {
                            hull.faces[i].edge = e12;
                        }

                        faceHalfEdges.Add(e12);

                        edgesList[e12].prev = -1;
                        edgesList[e12].next = -1;
                        edgesList[e12].twin = e21;
                        edgesList[e12].face = i;
                        edgesList[e12].origin = v1;

                        edgesList[e21].prev = -1;
                        edgesList[e21].next = -1;
                        edgesList[e21].twin = e12;
                        edgesList[e21].face = -1;
                        edgesList[e21].origin = v2;

                        // Add edges to map.
                        edgeMap[(v1, v2)] = e12;
                        edgeMap[(v2, v1)] = e21;
                    }
                }

                // Link the half-edges of the current face.
                for (int j = 0; j < faceHalfEdges.Count; ++j)
                {
                    int e1 = faceHalfEdges[j];
                    int e2 = j + 1 < faceHalfEdges.Count ? faceHalfEdges[j + 1] : faceHalfEdges[0];

                    edgesList[e1].next = e2;
                    edgesList[e2].prev = e1;
                }


            }

            hull.edgesNative = new NativeArrayNoLeakDetection<NativeHalfEdge>(hull.edgeCount, Allocator.Persistent);
            for (int j = 0; j < hull.edgeCount; j++)
            {
                hull.edgesNative[j] = edgesList[j];
            }

            hull.edges = (NativeHalfEdge*)hull.edgesNative.GetUnsafePtr();

            hull.Validate();
        }

        public unsafe static void CreateFacesPlanes(ref NativeHull hull, ref NativeHullDef def)
        {
            //Debug.Assert((IntPtr)hull.facesPlanes != IntPtr.Zero);
            //Debug.Assert(hull.faceCount > 0);

            hull.facesPlanesNative = new NativeArrayNoLeakDetection<NativePlane>(def.faceCount, Allocator.Persistent);
            hull.facesPlanes = (NativePlane*)hull.facesPlanesNative.GetUnsafePtr();

            for (int i = 0; i < def.faceCount; ++i)
            {
                NativeFaceDef face = def.facesNative[i];
                int vertCount = face.vertexCount;

                Debug.Assert(vertCount >= 3, "Input mesh must have at least 3 vertices");

                int* indices = face.vertices;

                float3 normal = default;
                float3 centroid = default;

                for (int j = 0; j < vertCount; ++j)
                {
                    int i1 = indices[j];
                    int i2 = j + 1 < vertCount ? indices[j + 1] : indices[0];

                    float3 v1;
                    float3 v2;
                    try
                    {
                        v1 = def.verticesNative[i1];
                        v2 = def.verticesNative[i2];
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    normal += Newell(v1, v2);
                    centroid += v1;
                }

                hull.facesPlanes[i].Normal = normalize(normal);
                hull.facesPlanes[i].Offset = dot(normalize(normal), centroid) / vertCount;
            }

            float3 Newell(float3 a, float3 b)
            {
                return new float3(
                    (a.y - b.y) * (a.z + b.z),
                    (a.z - b.z) * (a.x + b.x),
                    (a.x - b.x) * (a.y + b.y));
            }
        }

        public static Dictionary<float3, List<DetailedFaceDef>> GroupByNormal(IList<DetailedFaceDef> data)
        {
            var map = new Dictionary<float3, List<DetailedFaceDef>>();
            for (var i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (!map.TryGetValue(item.Normal, out List<DetailedFaceDef> value))
                {
                    map[item.Normal] = new List<DetailedFaceDef> { item };
                    continue;
                }
                value.Add(item);
            }
            return map;
        }

        public static List<List<DetailedFaceDef>> GroupBySharedVertex(Dictionary<float3, List<DetailedFaceDef>> groupedFaces)
        {
            var result = new List<List<DetailedFaceDef>>();
            foreach (var facesSharingNormal in groupedFaces)
            {
                var map = new List<(HashSet<int> Key, List<DetailedFaceDef> Value)>();
                foreach (var face in facesSharingNormal.Value)
                {
                    var group = map.FirstOrDefault(pair => face.Indices.Any(pair.Key.Contains));
                    if (group.Key != null)
                    {
                        foreach (var idx in face.Indices)
                        {
                            group.Key.Add(idx);
                        }
                        group.Value.Add(face);
                    }
                    else
                    {
                        map.Add((new HashSet<int>(face.Indices), new List<DetailedFaceDef> { face }));
                    }
                }
                result.AddRange(map.Select(group => group.Value));
            }
            return result;
        }

        public static float3 RoundVertex(Vector3 v)
        {
            return float3(
                (float)System.Math.Round(v.x, 3),
                (float)System.Math.Round(v.y, 3),
                (float)System.Math.Round(v.z, 3));
        }
    }
}
