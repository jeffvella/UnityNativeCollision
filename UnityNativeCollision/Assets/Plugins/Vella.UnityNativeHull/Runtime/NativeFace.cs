using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    public struct NativeFace
    {
        /// <summary>
        /// Index of the starting edge on this face.
        /// </summary>
        public int Edge;

        public ref NativeHalfEdge GetFirstEdge(NativeHull hull) => ref hull.GetEdgeRef(Edge);

        public float3 CalculateFaceCentroid(NativeHull hull)
        {
            float3 centroid = 0;
            int edgeCount = 0;
            ref NativeHalfEdge start = ref hull.GetEdgeRef(Edge);
            ref NativeHalfEdge current = ref start;
            do
            {
                edgeCount++;
                centroid += current.GetOrigin(hull);
                current = ref hull.GetEdgeRef(current.Next);
            }
            while (current.Origin != start.Origin);
            return centroid / edgeCount;
        }

    };
}
