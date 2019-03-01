using System.Diagnostics;
using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("Edge: Origin={Origin}, Face={Face}, Twin={Twin}, [Prev{Prev} Next={Next}]")]
    public struct NativeHalfEdge
    {
        /// <summary>
        /// The previous edge index in face loop
        /// </summary>
        public int Prev;

        /// <summary>
        /// The next edge index in face loop
        /// </summary>
        public int Next;
        
        /// <summary>
        /// The edge on the other side of this edge (in a different face loop)
        /// </summary>
        public int Twin;

        /// <summary>
        /// The face index of this face loop
        /// </summary>
        public int Face;

        /// <summary>
        /// The index of the vertex at the start of this edge.
        /// </summary>
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
