using System.Diagnostics;
using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("NativeFace: Edge={Edge}")]
    public struct NativeFace
    {
        /// <summary>
        /// Index of the starting edge on this face.
        /// </summary>
        public int Edge;
    };
}
