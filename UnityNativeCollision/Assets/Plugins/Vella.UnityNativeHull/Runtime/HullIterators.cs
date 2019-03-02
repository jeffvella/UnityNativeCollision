using System;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Collections;

namespace Vella.UnityNativeHull
{
    /// <summary>
    /// Loops through all edges in a hull or a specific hull face.
    /// Note: this can be used with foreach statements within Burst compiled code.
    /// </summary>
    public ref struct EdgeEnumerator
    {
        private int _offset;
        private int _edgeIndex;
        private int _currentIndex;
        private NativeHull _hull;

        public EdgeEnumerator(NativeHull hull) : this()
        {
            _hull = hull;
            _offset = -1;
            _currentIndex = -1;
        }

        public EdgeEnumerator(NativeHull hull, int faceIndex) : this()
        {
            _hull = hull;
            _offset = hull.GetFace(faceIndex).Edge;
            _currentIndex = -1;
        }

        public ref NativeHalfEdge Current => ref _hull.GetEdgeRef(_edgeIndex);

        public bool MoveNext()
        {
            if (_edgeIndex >= _hull.EdgeCount-1)
            {
                return false;
            }
            else if (_offset == -1)
            {
                _edgeIndex = _currentIndex;
            }
            else if (_currentIndex == -1)
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

        public EdgeEnumerator GetEnumerator() => this;
    }

}
