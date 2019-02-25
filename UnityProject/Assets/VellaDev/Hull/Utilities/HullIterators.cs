using System;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Collections;


namespace VellaDev.Hull
{
    public static class HullIterators
    {
        public delegate void ByRefEdgeAction(ref NativeHalfEdge edge);
        public delegate void ByRefFaceEdgeAction(ref NativePlane plane, ref NativeHalfEdge edge);
        public delegate void ByRefIndexedFaceEdgeAction(int i, ref NativePlane plane, ref NativeHalfEdge edge);

        public static void IterateFaceEdges(this NativeHull hull, int faceIndex, ByRefFaceEdgeAction action)
        {
            ref NativeFace face = ref hull.GetFaceRef(faceIndex);
            ref NativePlane plane = ref hull.GetPlaneRef(faceIndex);
            ref NativeHalfEdge start = ref hull.GetEdgeRef(face.Edge);
            ref NativeHalfEdge current = ref start;
            do
            {
                action?.Invoke(ref plane, ref current);
                current = ref hull.GetEdgeRef(current.Next);
            }
            while (current.Origin != start.Origin);
        }

        public static void IterateFaceTwins(this NativeHull hull, int faceIndex, ByRefFaceEdgeAction action)
        {
            ref NativeFace face = ref hull.GetFaceRef(faceIndex);            
            ref NativeHalfEdge start = ref hull.GetEdgeRef(face.Edge);
            ref NativeHalfEdge current = ref start;
            do
            {
                ref NativeHalfEdge twin = ref current.GetTwin(hull);
                ref NativePlane twinPlane = ref hull.GetPlaneRef(twin.Face);
                action?.Invoke(ref twinPlane, ref twin);
                current = ref hull.GetEdgeRef(current.Next);
            }
            while (current.Origin != start.Origin);
        }

        public static void IterateFaces(this NativeHull hull, ByRefFaceEdgeAction action)
        {
            for (int i = 0; i < hull.FaceCount; i++)
            {        
                ref var firstEdge = ref hull.GetFaceRef(i).GetFirstEdge(hull);
                action?.Invoke(ref hull.GetPlaneRef(i), ref firstEdge);
            }
        }

        public static void IterateFaces(this NativeHull hull, ByRefIndexedFaceEdgeAction action)
        {
            for (int i = 0; i < hull.FaceCount; i++)
            {
                ref var firstEdge = ref hull.GetFaceRef(i).GetFirstEdge(hull);
                action?.Invoke(i, ref hull.GetPlaneRef(i), ref firstEdge);
            }
        }


        public static void IterateEdges(this NativeHull hull, ByRefEdgeAction action)
        {
            for (int i = 0; i < hull.EdgeCount; i++)
            {
                action?.Invoke(ref hull.GetEdgeRef(i));
            }
        }

        public static void IterateVertices(this NativeHull hull, Action<float3> action)
        {
            for (int i = 0; i < hull.EdgeCount; i++)
            {
                action?.Invoke(hull.GetEdgeRef(i).GetOrigin(hull));
            }
        }
    }

    public class HullAllEdgesEnumerator : IEnumerator<NativeHalfEdge>, IEnumerable<NativeHalfEdge>
    {
        private int _currentIndex = -2;
        private NativeHull _hull;

        public HullAllEdgesEnumerator(NativeHull hull)
        {
            _hull = hull;
        }

        public bool MoveNext()
        {
            if (_currentIndex + 2 >= _hull.EdgeCount)
                return false;

            _currentIndex = _currentIndex + 2;
            return true;
        }

        public void Reset()
        {
            _currentIndex = -2;
        }

        NativeHalfEdge IEnumerator<NativeHalfEdge>.Current => _hull.GetEdge(_currentIndex);
        public object Current => _hull.GetEdge(_currentIndex);
        public void Dispose() { }
        public IEnumerator<NativeHalfEdge> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    public class HullFaceEdgesEnumerator : IEnumerator<NativeHalfEdge>, IEnumerable<NativeHalfEdge>
    {
        private int _offset;
        private NativeHull _hull;
        private int _edgeIndex;

        private int _currentIndex = -1;

        public HullFaceEdgesEnumerator(NativeHull hull, int faceIndex)
        {
            _hull = hull;
            _offset = hull.GetFace(faceIndex).Edge;
        }

        public bool MoveNext()
        {
            if (_currentIndex == -1)
            {
                _edgeIndex = _offset;
            }
            else
            {
                ref var edge = ref _hull.GetEdgeRef(_edgeIndex);
                if (edge.Next == _offset)
                {
                    return false;
                }
                _edgeIndex = edge.Next;
            }
            _currentIndex++;
            return true;
        }

        public void Reset()
        {
            _currentIndex = -1;
        }

        NativeHalfEdge IEnumerator<NativeHalfEdge>.Current
        {
            get
            {

                return _hull.GetEdge(_edgeIndex);
            }
        }

        public object Current => _hull.GetEdge(_edgeIndex);

        public void Dispose() { }
        public IEnumerator<NativeHalfEdge> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => this;
    }


}
