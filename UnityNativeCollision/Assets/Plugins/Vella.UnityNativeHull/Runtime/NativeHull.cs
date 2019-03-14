using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;

namespace Vella.UnityNativeHull
{
    public unsafe struct NativeHull : IDisposable
    {
        public int VertexCount;
        public int FaceCount;
        public int EdgeCount;

        // Native array is currently not Blittable because of DisposeSentinel being a class.
        // Which means you cannot place inside a NativeArray<T>, which batch operations require.

        public NativeArrayNoLeakDetection<float3> VerticesNative;
        public NativeArrayNoLeakDetection<NativeFace> FacesNative;
        public NativeArrayNoLeakDetection<NativePlane> PlanesNative;
        public NativeArrayNoLeakDetection<NativeHalfEdge> EdgesNative;

        //public NativeArray<float3> verticesNative;
        //public NativeArray<NativeFace> facesNative;
        //public NativeArray<NativePlane> facesPlanesNative;
        //public NativeArray<NativeHalfEdge> edgesNative;

        [NativeDisableUnsafePtrRestriction]
        public float3* Vertices;

        [NativeDisableUnsafePtrRestriction]
        public NativeFace* Faces;

        [NativeDisableUnsafePtrRestriction]
        public NativePlane* Planes;

        [NativeDisableUnsafePtrRestriction]
        public NativeHalfEdge* Edges;

        private int _isCreated;
        private int _isDisposed;

        public bool IsCreated
        {
            get => _isCreated == 1;
            set => _isCreated = value ? 1 : 0;
        }

        public bool IsDisposed
        {
            get => _isDisposed == 1;
            set => _isDisposed = value ? 1 : 0;
        }

        public bool IsValid => IsCreated && !IsDisposed;

        public void Dispose()
        {
            if (_isDisposed == 0)
            {
                _isDisposed = 1;

                if (VerticesNative.IsCreated)
                    VerticesNative.Dispose();

                if (FacesNative.IsCreated)
                    FacesNative.Dispose();

                if (PlanesNative.IsCreated)
                    PlanesNative.Dispose();

                if (EdgesNative.IsCreated)
                    EdgesNative.Dispose();

                Vertices = null;
                Faces = null;
                Planes = null;
                Edges = null;
            }

        }

        public unsafe float3 GetVertex(int index) => VerticesNative[index];
        public unsafe ref float3 GetVertexRef(int index) => ref *(Vertices + index);
        public unsafe float3* GetVertexPtr(int index) => Vertices + index;

        public unsafe NativeHalfEdge GetEdge(int index) => EdgesNative[index];
        public unsafe ref NativeHalfEdge GetEdgeRef(int index) => ref *(Edges + index);
        public unsafe NativeHalfEdge* GetEdgePtr(int index) => Edges + index;

        public unsafe NativeFace GetFace(int index) => FacesNative[index];
        public unsafe ref NativeFace GetFaceRef(int index) => ref *(Faces + index);
        public unsafe NativeFace* GetFacePtr(int index) => Faces + index;

        public unsafe NativePlane GetPlane(int index) => PlanesNative[index];
        public unsafe ref NativePlane GetPlaneRef(int index) => ref *(Planes + index);
        public unsafe NativePlane* GetPlanePtr(int index) => Planes + index;

        public unsafe float3 GetSupport(float3 direction)
        {
            return Vertices[GetSupportIndex(direction)];
        }

        public unsafe int GetSupportIndex(float3 direction)
        {
            int index = 0;
            float max = math.dot(direction, Vertices[index]);
            for (int i = 1; i < VertexCount; ++i)
            {
                float dot = math.dot(direction, Vertices[i]);
                if (dot > max)
                {
                    index = i;
                    max = dot;
                }
            }
            return index;
        }

    }

    public static class NativeHullExtensions
    {
        public static EdgeEnumerator GetEdges(this NativeHull hull)
        {
            return new EdgeEnumerator(hull);
        }

        public static EdgeEnumerator GetEdges(this NativeHull hull, int faceIndex)
        {
            return new EdgeEnumerator(hull, faceIndex);
        }

        public static IList<float3> GetVertices(this NativeHull hull, int faceIndex)
        {
            var result = new List<float3>();
            foreach(var edge in GetEdges(hull, faceIndex))
            {
                result.Add(hull.GetVertex(edge.Origin));
            }
            return result;
        }

        public static float3 CalculateFaceCentroid(this NativeHull hull, NativeFace face)
        {
            float3 centroid = 0;
            int edgeCount = 0;
            ref NativeHalfEdge start = ref hull.GetEdgeRef(face.Edge);
            ref NativeHalfEdge current = ref start;
            do
            {
                edgeCount++;
                centroid += hull.GetVertex(current.Origin);
                current = ref hull.GetEdgeRef(current.Next);
            }
            while (current.Origin != start.Origin);
            return centroid / edgeCount;
        }

    }

}

