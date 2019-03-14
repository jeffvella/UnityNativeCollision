using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Vella.Common
{
    /// <summary>
    /// Wrapper on NativeBuffer allowing two objects to be stored sequentially per row
    /// </summary>
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeBufferDebugView<>))]
    public struct NativePairBuffer<THead,T> : IDisposable where THead : unmanaged, IEquatable<THead>, IComparable<THead> where T : struct, IEquatable<T>, IComparable<T>
    {
        private NativeBuffer<Pair> _buffer;
        private readonly int _headSize;

        public struct Pair
        {
            public THead Header;
            public T Value;
        }

        public unsafe NativePairBuffer(void* ptr, int elementCount)
        {
            _headSize = UnsafeUtility.SizeOf<THead>();  
            _buffer = new NativeBuffer<Pair>(ptr, elementCount);
        }

        public void CopyFrom(NativePairBuffer<THead, T> source)
        {            
            _buffer.CopyFrom(source._buffer);
        }

        public NativePairBuffer(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            _headSize = UnsafeUtility.SizeOf<THead>();
            _buffer = new NativeBuffer<Pair>(length, allocator, options);        
        }

        public ref Pair this[int i] => ref _buffer[i];

        public ref THead GetItemHeader(int i) => ref _buffer.AsRef<THead>(i);

        public ref T GetItemValue(int i) => ref _buffer.AsRef<T>(i, _headSize);

        public int Add(THead header, T item) => _buffer.Add(new Pair
        {
            Header = header,
            Value = item,
        }); 
 
        public bool RemoveAt(int index) => _buffer.RemoveAt(index);

        public void RemoveAtSwapBack(int index) =>  _buffer.RemoveAtSwapBack(index);

        public unsafe void Remove<TU>(TU* elementPtr) where TU : unmanaged => _buffer.Remove(elementPtr);

        public Pair Pop() =>  _buffer.Pop();
        
        public int IndexOf(T item) => _buffer.IndexOf(item);

        public int IndexOf(THead item) => _buffer.IndexOf(item, _headSize);

        public bool Contains(T item) => IndexOf(item) != -1;

        public bool Contains(THead item) => IndexOf(item) != -1;

        public void Reverse() => _buffer.Reverse();

        public int Capacity => _buffer.Length;

        public int CapacityBytes => _buffer.CapacityBytes;

        public int Length => _buffer.Length;

        public bool IsCreated => _buffer.IsCreated;

        public void Clear() => _buffer.Clear();

        public void SetLength(int itemCount) => _buffer.SetLength(itemCount);

        public Pair[] ToArray() => _buffer.ToArray();

        public void Dispose() => _buffer.Dispose();

        //public void Sort(IComparer<Pair> comparer = null) => _buffer.Sort(comparer);

        //public void SortDescending(IComparer<Pair> comparer = null) => _buffer.Sort(comparer);

        //public void Sort<TParam>(IComparer<Pair, TParam> comparer, TParam param) where TParam : unmanaged => _buffer.Sort(comparer, param);

        //public void SortDescending<TParam>(IComparer<Pair, TParam> comparer, TParam param) where TParam : unmanaged => _buffer.Sort(comparer, param);

        public NativeBuffer<Pair>.NativeBufferEnumerator GetEnumerator() => new NativeBuffer<Pair>.NativeBufferEnumerator(ref _buffer);
    }
}
