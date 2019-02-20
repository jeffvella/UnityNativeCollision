using Unity.Mathematics;

namespace VellaDev.Hull
{
    public struct NativeHalfEdge
    {
        public int prev;
        public int next;
        public int twin;
        public int face;
        public int origin;

        public ref NativeFace GetFace(NativeHull hull) => ref hull.GetFaceRef(face);
        public ref NativeHalfEdge GetNext(NativeHull hull) => ref hull.GetEdgeRef(next);
        public ref NativeHalfEdge GetPrev(NativeHull hull) => ref hull.GetEdgeRef(prev);
        public ref float3 GetOrigin(NativeHull hull) => ref hull.GetVertexRef(origin);
        public ref NativeHalfEdge GetTwin(NativeHull hull) => ref hull.GetEdgeRef(twin);
        public ref float3 GetTwinOrigin(NativeHull hull) => ref GetTwin(hull).GetOrigin(hull);
        public ref float3 GetNextOrigin(NativeHull hull) => ref GetNext(hull).GetOrigin(hull);
        public ref NativeHalfEdge AsRef(NativeHull hull) => ref GetTwin(hull).GetTwin(hull);
    };
}
