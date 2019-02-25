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
    public unsafe struct NativeHull : IDisposable
    {
        public int VertexCount;
        public int FaceCount;
        public int EdgeCount;

        // Native array is currently not Blittable because of DisposeSentinel being a class.
        // Which means you cannot place inside a NativeArray<T>, which batch operations require.

        public NativeArrayNoLeakDetection<float3> VerticesNative;
        public NativeArrayNoLeakDetection<NativeFace> FacesNative;
        public NativeArrayNoLeakDetection<NativePlane> FacesPlanesNative;
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
        public NativePlane* FacesPlanes;

        [NativeDisableUnsafePtrRestriction]
        public NativeHalfEdge* edges;

        private int _isCreated;
        private int _isDisposed;

        public bool IsCreated
        {
            get => _isCreated == 1;
            set => _isCreated = value ? 1 : 0;
        }

        public bool IsValid => IsCreated && _isDisposed == 0 && (IntPtr)Vertices != IntPtr.Zero;

        public void Dispose()
        {
            if (_isDisposed == 0)
            {
                if (VerticesNative.IsCreated)
                    VerticesNative.Dispose();

                if (FacesNative.IsCreated)
                    FacesNative.Dispose();

                if (FacesPlanesNative.IsCreated)
                    FacesPlanesNative.Dispose();

                if (EdgesNative.IsCreated)
                    EdgesNative.Dispose();

                Vertices = null;
                Faces = null;
                FacesPlanes = null;
                edges = null;
            }
            _isDisposed = 1;
        }

        public unsafe float3 GetVertex(int index) => VerticesNative[index];
        public unsafe ref float3 GetVertexRef(int index) => ref *(Vertices + index);
        public unsafe float3* GetVertexPtr(int index) => Vertices + index;

        public unsafe NativeHalfEdge GetEdge(int index) => EdgesNative[index];
        public unsafe ref NativeHalfEdge GetEdgeRef(int index) => ref *(edges + index);
        public unsafe NativeHalfEdge* GetEdgePtr(int index) => edges + index;

        public unsafe NativeFace GetFace(int index) => FacesNative[index];
        public unsafe ref NativeFace GetFaceRef(int index) => ref *(Faces + index);
        public unsafe NativeFace* GetFacePtr(int index) => Faces + index;


        public unsafe NativePlane GetPlane(int index) => FacesPlanesNative[index];
        public unsafe ref NativePlane GetPlaneRef(int index) => ref *(FacesPlanes + index);
        public unsafe NativePlane* GetPlanePtr(int index) => FacesPlanes + index;

        public unsafe float3 GetSupport(float3 direction)
        {
            return Vertices[GetSupportIndex(direction)];
        }

        public unsafe int GetSupportIndex(float3 direction)
        {
            int index = 0;
            float max = dot(direction, Vertices[index]);
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

