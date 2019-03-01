using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections;

namespace Vella.Common
{
    public struct NativeArray2D<T> : IDisposable, IEnumerable<T> where T : unmanaged
    {
        public NativeArray<T> Internal;
        private readonly int _yLength;
        private readonly int _xLength;

        public NativeArray2D(int x, int y, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Internal = new NativeArray<T>(x * y, allocator);
            _yLength = y;
            _xLength = x;
        }

        public unsafe ref T this[int i] => ref Internal.AsRef(i);

        public ref T this[int x, int y] => ref this[(x * _yLength) + y];

        public int XLength => _xLength;

        public int YLength => _yLength;

        public int Length => Internal.Length;

        public void Dispose() => Internal.Dispose();

        public IEnumerator<T> GetEnumerator() => Internal.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}