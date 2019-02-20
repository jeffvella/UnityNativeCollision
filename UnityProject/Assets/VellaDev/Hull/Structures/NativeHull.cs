using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;


namespace VellaDev.Hull
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
        public int vertexCount;
        public int* vertices;
        internal int highestIndex;
    };

    public unsafe struct NativeHullDef
    {
        public int faceCount;
        public int vertexCount;
        public NativeArray<float3> verticesNative;
        public NativeArray<NativeFaceDef> facesNative;
    };

    public unsafe struct NativeHull : IDisposable
    {  
        public int vertexCount;
        public int faceCount;
        public int edgeCount;

        // Native array is currently not Blittable because of DisposeSentinel being a class.
        // Which means you cannot place inside a NativeArray<T>, which batch operations require.

        public NativeArrayNoLeakDetection<float3> verticesNative;
        public NativeArrayNoLeakDetection<NativeFace> facesNative;
        public NativeArrayNoLeakDetection<NativePlane> facesPlanesNative;
        public NativeArrayNoLeakDetection<NativeHalfEdge> edgesNative;

        //public NativeArray<float3> verticesNative;
        //public NativeArray<NativeFace> facesNative;
        //public NativeArray<NativePlane> facesPlanesNative;
        //public NativeArray<NativeHalfEdge> edgesNative;

        [NativeDisableUnsafePtrRestriction]
        public float3* vertices;

        [NativeDisableUnsafePtrRestriction]
        public NativeFace* faces;

        [NativeDisableUnsafePtrRestriction]
        public NativePlane* facesPlanes;

        [NativeDisableUnsafePtrRestriction]
        public NativeHalfEdge* edges;

        public int _isCreated;
        public int _isDisposed;

        public bool IsValid => _isCreated == 1 && _isDisposed == 0 && (IntPtr)vertices != IntPtr.Zero;

        public void Dispose()
        {
            if (_isDisposed == 0)
            {
                if (verticesNative.IsCreated)
                    verticesNative.Dispose();

                if (facesNative.IsCreated)
                    facesNative.Dispose();

                if (facesPlanesNative.IsCreated)
                    facesPlanesNative.Dispose();

                if (edgesNative.IsCreated)
                    edgesNative.Dispose();

                vertices = null;
                faces = null;
                facesPlanes = null;
                edges = null;
            }
            _isDisposed = 1;
        }
    }

    public static class NativeHullExtensions
    {
        public static unsafe float3 GetVertex(this NativeHull hull, int index)
        {
            return hull.verticesNative[index];
        }

        public static unsafe ref float3 GetVertexRef(this NativeHull hull, int index)
        {
            return ref *(hull.vertices + index);
        }

        public static unsafe float3* GetVertexPtr(this NativeHull hull, int index)
        {
            return hull.vertices + index;
        }

        public static unsafe NativeHalfEdge GetEdge(this NativeHull hull, int index)
        {
            return hull.edgesNative[index];
        }

        public static unsafe ref NativeHalfEdge GetEdgeRef(this NativeHull hull, int index)
        {
            return ref *(hull.edges + index);
        }

        public static unsafe NativeHalfEdge* GetEdgePtr(this NativeHull hull, int index)
        {
            return hull.edges + index;
        }

        public static unsafe NativeFace GetFace(this NativeHull hull, int index)
        {
            return hull.facesNative[index];
        }

        public static unsafe ref NativeFace GetFaceRef(this NativeHull hull, int index)
        {
            return ref *(hull.faces + index);
        }

        public static unsafe NativeFace* GetFacePtr(this NativeHull hull, int index)
        {
            return hull.faces + index;
        }

        public static unsafe NativePlane GetPlane(this NativeHull hull, int index)
        {
            return hull.facesPlanesNative[index];
        }

        public static unsafe ref NativePlane GetPlaneRef(this NativeHull hull, int index)
        {
            return ref *(hull.facesPlanes + index);
        }

        public static unsafe NativePlane* GetPlanePtr(this NativeHull hull, int index)
        {
            return hull.facesPlanes + index;
        }

        public static unsafe float3 GetSupport(this NativeHull hull, float3 direction)
        {
            return hull.vertices[hull.GetSupportIndex(direction)];
        }

        public static unsafe int GetSupportIndex(this NativeHull hull, float3 direction)
        {
            int index = 0;
            float max = dot(direction, hull.vertices[index]);
            for (int i = 1; i < hull.vertexCount; ++i)
            {
                float dot = math.dot(direction, hull.vertices[i]);
                if (dot > max)
                {
                    index = i;
                    max = dot;
                }
            }
            return index;
        }

        public static IEnumerable<NativeHalfEdge> GetEdges(this NativeHull hull)
        {
            return new HullAllEdgesEnumerator(hull);
        }

        public static IEnumerable<NativeHalfEdge> GetEdges(this NativeHull hull, int faceIndex)
        {
            return new HullFaceEdgesEnumerator(hull, faceIndex);
        }

        public static IEnumerable<float3> GetVertices(this NativeHull hull)
        {
            return new HullAllEdgesEnumerator(hull).Select(v => v.GetOrigin(hull));
        }

        public static IEnumerable<float3> GetVertices(this NativeHull hull, int faceIndex)
        {
            return new HullFaceEdgesEnumerator(hull, faceIndex).Select(v => v.GetOrigin(hull));
        }

    }

}

