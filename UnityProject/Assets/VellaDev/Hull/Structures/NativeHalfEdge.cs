using Unity.Mathematics;

namespace VellaDev.Hull
{
    public struct NativeHalfEdge
    {
        public int Prev;
        public int Next;
        public int Twin;
        public int Face;
        public int Origin;

        public ref NativeFace GetFace(NativeHull hull) => ref hull.GetFaceRef(Face);
        public ref NativeHalfEdge GetNext(NativeHull hull) => ref hull.GetEdgeRef(Next);
        public ref NativeHalfEdge GetPrev(NativeHull hull) => ref hull.GetEdgeRef(Prev);
        public ref float3 GetOrigin(NativeHull hull) => ref hull.GetVertexRef(Origin);
        public ref NativeHalfEdge GetTwin(NativeHull hull) => ref hull.GetEdgeRef(Twin);
        public ref float3 GetTwinOrigin(NativeHull hull) => ref GetTwin(hull).GetOrigin(hull);
        public ref float3 GetNextOrigin(NativeHull hull) => ref GetNext(hull).GetOrigin(hull);
        public ref NativeHalfEdge AsRef(NativeHull hull) => ref GetTwin(hull).GetTwin(hull);
    };
}
