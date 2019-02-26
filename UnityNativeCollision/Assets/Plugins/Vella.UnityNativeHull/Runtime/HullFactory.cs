using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;

namespace Vella.UnityNativeHull
{
    public class HullFactory
    {
        public struct DetailedFaceDef
        {
            public Vector3 Center;
            public Vector3 Normal;
            public List<float3> Verts;
            public List<int> Indices;
        }

        public unsafe struct NativeFaceDef
        {
            public int VertexCount;
            public int* Vertices;
            public int HighestIndex;
        };

        public unsafe struct NativeHullDef
        {
            public int FaceCount;
            public int VertexCount;
            public NativeArray<float3> VerticesNative;
            public NativeArray<NativeFaceDef> FacesNative;
        };

        public static unsafe NativeHull CreateBox(float3 scale)
        {
            float3[] cubeVertices =
            {
                new float3(0.5f, 0.5f, -0.5f),
                new float3(-0.5f, 0.5f, -0.5f),
                new float3(-0.5f, -0.5f, -0.5f),
                new float3(0.5f, -0.5f, -0.5f),
                new float3(0.5f, 0.5f, 0.5f),
                new float3(-0.5f, 0.5f, 0.5f),
                new float3(-0.5f, -0.5f, 0.5f),
                new float3(0.5f, -0.5f, 0.5f),
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
                new NativeFaceDef {VertexCount = 4, Vertices = left},
                new NativeFaceDef {VertexCount = 4, Vertices = right},
                new NativeFaceDef {VertexCount = 4, Vertices = down},
                new NativeFaceDef {VertexCount = 4, Vertices = up},
                new NativeFaceDef {VertexCount = 4, Vertices = back},
                new NativeFaceDef {VertexCount = 4, Vertices = front},
            };

            var result = new NativeHull();

            using (var boxFacesNative = new NativeArray<NativeFaceDef>(boxFaces, Allocator.Temp))
            using (var cubeVertsNative = new NativeArray<float3>(cubeVertices, Allocator.Temp))
            {
                NativeHullDef hullDef;
                hullDef.VertexCount = 8;
                hullDef.VerticesNative = cubeVertsNative;
                hullDef.FaceCount = 6;
                hullDef.FacesNative = boxFacesNative;
                SetFromFaces(ref result, hullDef);
            }

            result.IsCreated = true;
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

                var normal = math.normalize(math.cross(p3 - p2, p1 - p2));

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
                    HighestIndex = max,
                    VertexCount = borderIndices.Length,
                    Vertices = v,
                });
            }

            // Remove vertices with no edges connected to them and fix all impacted face vertex references.
            foreach (var orphanIdx in orphanIndices.OrderByDescending(i => i))
            {
                uniqueVerts.RemoveAt(orphanIdx);

                foreach(var face in faceDefs.Where(f => f.HighestIndex >= orphanIdx))
                {
                    for (int i = 0; i < face.VertexCount; i++)
                    {
                        var faceVertIdx = face.Vertices[i];
                        if (faceVertIdx >= orphanIdx)
                        {
                            face.Vertices[i] = --faceVertIdx;
                        }
                    }
                }
            }

            var result = new NativeHull();

            using (var faceNative = new NativeArray<NativeFaceDef>(faceDefs.ToArray(), Allocator.Temp))
            using (var vertsNative = new NativeArray<float3>(uniqueVerts.ToArray(), Allocator.Temp))
            {

                NativeHullDef hullDef;
                hullDef.VertexCount = vertsNative.Length;
                hullDef.VerticesNative = vertsNative;
                hullDef.FaceCount = faceNative.Length;
                hullDef.FacesNative = faceNative;
                SetFromFaces(ref result, hullDef);
            }

            result.IsCreated = true;
            return result;
        }


        public unsafe static void SetFromFaces(ref NativeHull hull, NativeHullDef def)
        {
            Debug.Assert(def.FaceCount > 0);
            Debug.Assert(def.VertexCount > 0);

            hull.VertexCount = def.VertexCount;
            var arr = def.VerticesNative.ToArray();

            hull.VerticesNative = new NativeArrayNoLeakDetection<float3>(arr, Allocator.Persistent);
            hull.Vertices = (float3*)hull.VerticesNative.GetUnsafePtr();
            hull.FaceCount = def.FaceCount;
            hull.FacesNative = new NativeArrayNoLeakDetection<NativeFace>(hull.FaceCount, Allocator.Persistent);
            hull.Faces = (NativeFace*)hull.FacesNative.GetUnsafePtr();

            // Initialize all faces by assigning -1 to each edge reference.
            for (int k = 0; k < def.FaceCount; ++k)
            {               
                NativeFace* f = hull.Faces + k;                
                f->Edge = -1;
            }

            CreateFacesPlanes(ref hull, ref def);

            var edgeMap = new Dictionary<(int v1, int v2), int>();
            var edgesList = new NativeHalfEdge[10000]; // todo lol

            // Loop through all faces.
            for (int i = 0; i < def.FaceCount; ++i)
            {
                NativeFaceDef face = def.FacesNative[i];
                int vertCount = face.VertexCount;

                Debug.Assert(vertCount >= 3);

                int* vertices = face.Vertices;

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
                        if (edgesList[e12].Face == -1)
                        {
                            edgesList[e12].Face = i;
                        }
                        else
                        {
                            throw new Exception("Two shared edges can't have the same vertices in the same order");        
                        }

                        if (hull.Faces[i].Edge == -1)
                        {
                            hull.Faces[i].Edge = e12;
                        }

                        faceHalfEdges.Add(e12);
                    }
                    else
                    {
                        // The next edge of the current half edge in the array is the twin edge.
                        int e12 = hull.EdgeCount++;
                        int e21 = hull.EdgeCount++;

                        if (hull.Faces[i].Edge == -1)
                        {
                            hull.Faces[i].Edge = e12;
                        }

                        faceHalfEdges.Add(e12);

                        edgesList[e12].Prev = -1;
                        edgesList[e12].Next = -1;
                        edgesList[e12].Twin = e21;
                        edgesList[e12].Face = i;
                        edgesList[e12].Origin = v1;

                        edgesList[e21].Prev = -1;
                        edgesList[e21].Next = -1;
                        edgesList[e21].Twin = e12;
                        edgesList[e21].Face = -1;
                        edgesList[e21].Origin = v2;

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

                    edgesList[e1].Next = e2;
                    edgesList[e2].Prev = e1;
                }


            }

            hull.EdgesNative = new NativeArrayNoLeakDetection<NativeHalfEdge>(hull.EdgeCount, Allocator.Persistent);
            for (int j = 0; j < hull.EdgeCount; j++)
            {
                hull.EdgesNative[j] = edgesList[j];
            }

            hull.Edges = (NativeHalfEdge*)hull.EdgesNative.GetUnsafePtr();
        }

        public unsafe static void CreateFacesPlanes(ref NativeHull hull, ref NativeHullDef def)
        {
            //Debug.Assert((IntPtr)hull.facesPlanes != IntPtr.Zero);
            //Debug.Assert(hull.faceCount > 0);

            hull.PlanesNative = new NativeArrayNoLeakDetection<NativePlane>(def.FaceCount, Allocator.Persistent);
            hull.Planes = (NativePlane*)hull.PlanesNative.GetUnsafePtr();

            for (int i = 0; i < def.FaceCount; ++i)
            {
                NativeFaceDef face = def.FacesNative[i];
                int vertCount = face.VertexCount;

                Debug.Assert(vertCount >= 3, "Input mesh must have at least 3 vertices");

                int* indices = face.Vertices;

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
                        v1 = def.VerticesNative[i1];
                        v2 = def.VerticesNative[i2];
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    normal += Newell(v1, v2);
                    centroid += v1;
                }

                hull.Planes[i].Normal = math.normalize(normal);
                hull.Planes[i].Offset = math.dot(math.normalize(normal), centroid) / vertCount;
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
            return new float3(
                (float)System.Math.Round(v.x, 3),
                (float)System.Math.Round(v.y, 3),
                (float)System.Math.Round(v.z, 3));
        }

        public struct PolygonPerimeter
        {
            public struct Edge
            {
                public int StartIndex;
                public int EndIndex;
            }

            private static readonly List<Edge> OutsideEdges = new List<Edge>();

            public static List<Edge> CalculatePerimeter(int[] indices, ref List<float3> verts)
            {
                OutsideEdges.Clear();

                for (int i = 0; i < indices.Length - 1; i += 3)
                {
                    int v3;
                    int v2;
                    int v1;

                    v1 = indices[i];
                    v2 = indices[i + 1];
                    v3 = indices[i + 2];

                    AddOutsideEdge(v1, v2);
                    AddOutsideEdge(v2, v3);
                    AddOutsideEdge(v3, v1);
                }

                // Check for crossed edges.
                for (int i = 0; i < OutsideEdges.Count; i++)
                {
                    var edge = OutsideEdges[i];
                    var nextIdx = i + 1 > OutsideEdges.Count - 1 ? 0 : i + 1;
                    var next = OutsideEdges[nextIdx];
                    if (edge.EndIndex != next.StartIndex)
                    {
                        return Rebuild();
                    }
                }

                return OutsideEdges;
            }

            private static void AddOutsideEdge(int i1, int i2)
            {
                foreach (var edge in OutsideEdges)
                {
                    // If each edge was already added, then it's a shared edge with another triangle - exclude them both.
                    if (edge.StartIndex == i1 & edge.EndIndex == i2 || edge.StartIndex == i2 & edge.EndIndex == i1)
                    {
                        OutsideEdges.Remove(edge);
                        return;
                    }
                }

                OutsideEdges.Add(new Edge { StartIndex = i1, EndIndex = i2 });
            }

            private static List<Edge> Rebuild()
            {
                var result = new List<Edge>();
                var map = OutsideEdges.ToDictionary(k => k.StartIndex, v => v.EndIndex);
                var cur = OutsideEdges.First().StartIndex;
                for (int i = 0; i < OutsideEdges.Count; i++)
                {
                    var edge = new Edge
                    {
                        StartIndex = cur,
                        EndIndex = map[cur]
                    };
                    result.Add(edge);
                    cur = edge.EndIndex;
                }

                return result;
            }
        }
    }
}
