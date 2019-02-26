namespace Vella.UnityNativeHull
{
    public struct NativeFace
    {
        public int Edge;

        public ref NativeHalfEdge GetFirstEdge(NativeHull hull) => ref hull.GetEdgeRef(Edge);
    };
}
