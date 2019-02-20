namespace VellaDev.Hull
{
    public struct NativeFace
    {
        public int edge;

        public ref NativeHalfEdge GetFirstEdge(NativeHull hull) => ref hull.GetEdgeRef(edge);
    };
}
