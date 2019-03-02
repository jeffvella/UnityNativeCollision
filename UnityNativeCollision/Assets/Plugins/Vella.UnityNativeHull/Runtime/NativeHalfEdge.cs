using System.Diagnostics;
using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("NativeHalfEdge: Origin={Origin}, Face={Face}, Twin={Twin}, [Prev{Prev} Next={Next}]")]
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
    };

}
