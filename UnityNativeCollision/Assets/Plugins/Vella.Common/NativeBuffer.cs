using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Internal;


namespace Vella.Common
{
    /// <summary>
    /// NativeBuffer<T> is an alternative to NativeArray<T>. 
    /// (NativeList<T> currently has issues being instantiated within a burst job)
    /// </summary>
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeBufferDebugView<>))]
    public struct NativeBuffer<T> : IDisposable where T : struct
    {
        private NativeBuffer _buffer;
        private int _maxIndex;

        /// <summary>
        /// Initialize the buffer with a preallocated memory space (e.g. stackalloc ptr)
        /// </summary>
        /// <param name="ptr">starting address of the allocated memory</param>
        /// <param name="elementCount">number of T sized elements in the allocation</param>
        public unsafe NativeBuffer(void* ptr, int elementCount)
        {
            _buffer = NativeBuffer.Assign<T>(ptr, elementCount);
            _maxIndex = -1;
        }

        public void CopyFrom(NativeBuffer<T> source)
        {
            source._buffer.CopyTo<T>(_buffer);
            _maxIndex = source._maxIndex;
        }

        public NativeBuffer(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            _buffer = NativeBuffer.Create<T>(length, allocator, options);
            _maxIndex = -1;
        }

        public ref T this[int i] => ref _buffer.AsRef<T>(i);

        public int Add(T item)
        { 
            _buffer.SetItem(++_maxIndex, item);
            return _maxIndex;
        }

        public bool RemoveAt(int index)
        {

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index > _maxIndex)
                return false;
#endif
            // Shuffle forward every item after the removed index
            for (int i = index + 1; i < _maxIndex; i++)
            {
                _buffer.SetItem(i - 1, _buffer.GetItem<T>(i));          
            }
            _maxIndex--;
            return true;
        }

        /// <summary>
        /// Overwrite the item to be removed with the last item in the collection.
        /// Very fast operation but does not maintain the order of items.
        /// </summary>  
        public void RemoveAtSwapBack(int index)
        {
            if (index == 0 && Length == 1)
            {
                _maxIndex--;
                return;
            }
            _buffer.SetItem(index, _buffer.GetItem<T>(--_maxIndex));
        }

        /// <summary>
        /// Returns the last element and decrements the element count without clearing
        /// the removed data (it may be overwritten by subsequent add operations)
        /// </summary> 
        public T Pop()
        {
            return _buffer.GetItem<T>(_maxIndex--);
        }

        public ref T Last => ref this[_maxIndex];
        public ref T First => ref this[0];

        //public int IndexOf(T item)
        //{
        //    for (int i = 0; i < Length; i++)
        //    {
        //        if (this[i].Equals(item)) // boxing
        //        {
        //            return i;
        //        }
        //    }
        //    return -1;
        //}

        public int IndexOf<T1>(T1 item, int offset = 0) where T1 : struct, IEquatable<T1>
        {
            for (int i = 0; i < Length; i++)
            {              
                //if (item.Equals()) // boxing instead of calling Equals(T obj) - fails on burst.             
                if (item.Equals(_buffer.AsRef<T1>(i, offset)))
                    return i;
            }
            return -1;
        }

        //public int IndexOf<T1>(T1 item) where T1 : struct, IComparable<T>
        //{
        //    for (int i = 0; i != Length; i++)
        //    {
        //        if (item.CompareTo(this[i]) == 0)
        //            return i;
        //    }
        //    return -1;
        //}

        public bool Contains<T1>(T1 item) where T1 : struct, IEquatable<T1>
        {
            return IndexOf(item) != -1;
        }

        public void Reverse()   
        {
            var index1 = 0;
            for (var index2 = _maxIndex; index1 < index2; --index2)
            {
                var obj = _buffer.GetItem<T>(index1);
                _buffer.SetItem(index1, _buffer.GetItem<T>(index2));
                _buffer.SetItem(index2, obj);   
                ++index1;
            }
        }

        public unsafe TU* GetItemPtr<TU>(int index) where TU : unmanaged
        {
            return _buffer.AsPtr<TU>(index);
        }

        public ref T1 AsRef<T1>(int index, int offset = 0) where T1 : struct
        {
            return ref _buffer.AsRef<T1>(index, offset);
        }

        public int Capacity => _buffer.Length;

        public int CapacityBytes => _buffer.Length * _buffer.itemSize;

        public int Length => _maxIndex + 1;

        public bool IsCreated => _buffer.IsCreated;

        public void Clear()
        {
            _buffer.Clear();
            _maxIndex = -1;
        }

        public void SetLength(int itemCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (itemCount < 0 || itemCount > Capacity)
            {
                throw new IndexOutOfRangeException($"{nameof(SetLength)} with itemCount '{itemCount}' failed. Range: 0-{Capacity-1}");
            }
#endif
            _maxIndex = itemCount - 1;
        }

        public T[] ToArray() => _buffer.ToArray<T>(Length);

        public void Dispose() => _buffer.Dispose();



        public void Sort(IComparer<T> comparer)
        {
            Sort(comparer, 0, _maxIndex, ListSortDirection.Ascending);
        }

        public void SortDescending(IComparer<T> comparer)
        {
            Sort(comparer, 0, _maxIndex, ListSortDirection.Descending);
        }

        private void Sort<TComparer>(TComparer comparer, int min, int max, ListSortDirection direction) where TComparer : IComparer<T>
        {
            int i = min, j = max;
            var pivot = this[(min + max) / 2];
            while (i <= j)
            {
                if (direction == ListSortDirection.Ascending)
                {
                    while (comparer.Compare(this[i], pivot) < 0) i++;
                    while (comparer.Compare(this[j], pivot) > 0) j--;
                }
                else
                {
                    while (comparer.Compare(this[i], pivot) > 0) i++;
                    while (comparer.Compare(this[j], pivot) < 0) j--;
                }
                if (i > j) continue;
                T tmp = this[i];
                this[i] = this[j];
                this[j] = tmp;
                i++;
                j--;
            }
            if (min < j)
            {
                Sort(comparer, min, j, direction);
            }
            if (i < max)
            {
                Sort(comparer, i, max, direction);
            }
        }

        public void Sort<TParam>(IComparer<T, TParam> comparer, TParam comparisonParameter) where TParam : unmanaged
        {
            Sort(comparer, comparisonParameter, 0, _maxIndex, ListSortDirection.Ascending);
        }

        public void SortDescending<TParam>(IComparer<T, TParam> comparer, TParam comparisonParameter) where TParam : unmanaged
        {
            Sort(comparer, comparisonParameter, 0, _maxIndex, ListSortDirection.Descending);
        }

        private void Sort<TParam>(IComparer<T, TParam> comparer, TParam param, int min, int max, ListSortDirection direction) where TParam : unmanaged
        {
            int i = min, j = max;
            var pivot = this[(min + max) / 2];
            while (i <= j)
            {
                if (direction == ListSortDirection.Ascending)
                {
                    while (comparer.Compare(this[i], pivot, param) < 0) i++;
                    while (comparer.Compare(this[j], pivot, param) > 0) j--;
                }
                else
                {
                    while (comparer.Compare(this[i], pivot, param) > 0) i++;
                    while (comparer.Compare(this[j], pivot, param) < 0) j--;
                }
                if (i > j) continue;
                T tmp = this[i];
                this[i] = this[j];
                this[j] = tmp;
                i++;
                j--;
            }
            if (min < j)
            {
                Sort(comparer, param, min, j, direction);
            }
            if (i < max)
            {
                Sort(comparer, param, i, max, direction);
            }
        }

        public NativeBufferEnumerator GetEnumerator() => new NativeBufferEnumerator(ref this);

        public ref struct NativeBufferEnumerator
        {
            private NativeBuffer<T> _source;
            private int _index;

            public NativeBufferEnumerator(ref NativeBuffer<T> buffer)
            {
                _source = buffer;
                _index = -1;
            }

            public bool MoveNext() => ++_index < _source.Length;

            public void Reset() => _index = -1;

            public ref T Current => ref _source._buffer.AsRef<T>(_index);
        }

        public unsafe void Remove<TU>(TU* elementPtr) where TU : unmanaged
        {
            var index = IndexOf(elementPtr);
            RemoveAtSwapBack(index);
        }

        /// <summary>
        /// Calculate an element index based on its memory address
        /// </summary>
        public unsafe int IndexOf(void* elementPtr)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (elementPtr == null)
                throw new ArgumentNullException();
#endif
            ulong ptr = (ulong)elementPtr;
            ulong bufferPtr = (ulong)_buffer.GetUnsafeBufferPointerWithoutChecks();
            ulong offset = ptr - bufferPtr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (offset > (ulong)CapacityBytes)
                throw new ArgumentOutOfRangeException($"Unable to to remove element with '{offset}' offset because it's out of range (0-{CapacityBytes})");
#endif
            int index = (int)(offset / (ulong)_buffer.itemSize);               
            if (index < _buffer.m_MinIndex || index > _buffer.m_MaxIndex)
                throw new ArgumentOutOfRangeException($"Unable to to remove element with '{offset}' offset because it's out of range (0-{CapacityBytes})");

            return index;
        }


        public unsafe void* GetUnsafePtr() => _buffer.GetUnsafePtr();
    }

    public static class NativeSortExtension2
    {
        //public struct DefaultComparer<T> : IComparer<T> where T : IComparable<T>
        //{
        //    public int Compare(T x, T y) => x?.CompareTo(y) ?? 0;
        //}

        //unsafe public static void Sort<T>(this NativeBuffer<T> array) where T : struct, IComparable<T>
        //{
        //    array.Sort(new DefaultComparer<T>());
        //}

        unsafe public static void Sort<T, U>(this NativeBuffer<T> array, U comp) where T : struct where U : IComparer<T>
        {

            IntroSort<T, U>(array.GetUnsafePtr(), 0, array.Length - 1, 2 * math_2.log2_floor(array.Length), comp);
        }

        //unsafe public static void Sort<T>(this NativeBuffer<T> slice) where T : struct, IComparable<T>
        //{
        //    slice.Sort(new DefaultComparer<T>());
        //}

        //        unsafe public static void Sort<T, U>(this NativeSlice<T> slice, U comp) where T : struct where U : IComparer<T>
        //        {
        //#if ENABLE_UNITY_COLLECTIONS_CHECKS
        //            if (slice.Stride != UnsafeUtility.SizeOf<T>())
        //                throw new InvalidOperationException("Sort requires that stride matches the size of the source type");
        //#endif

        //            IntroSort<T, U>(slice.GetUnsafePtr(), 0, slice.Length - 1, 2 * math_2.log2_floor(slice.Length), comp);
        //        }

        const int k_IntrosortSizeThreshold = 16;
        unsafe static void IntroSort<T, U>(void* array, int lo, int hi, int depth, U comp) where T : struct where U : IComparer<T>
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= k_IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreaterWithItems<T, U>(array, lo, hi - 1, comp);
                        SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
                        SwapIfGreaterWithItems<T, U>(array, hi - 1, hi, comp);
                        return;
                    }

                    InsertionSort<T, U>(array, lo, hi, comp);
                    return;
                }

                if (depth == 0)
                {
                    HeapSort<T, U>(array, lo, hi, comp);
                    return;
                }
                depth--;

                int p = Partition<T, U>(array, lo, hi, comp);
                IntroSort<T, U>(array, p + 1, hi, depth, comp);
                hi = p - 1;
            }
        }

        unsafe static void InsertionSort<T, U>(void* array, int lo, int hi, U comp) where T : struct where U : IComparer<T>
        {
            int i, j;
            T t;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = UnsafeUtility.ReadArrayElement<T>(array, i + 1);
                while (j >= lo && comp.Compare(t, UnsafeUtility.ReadArrayElement<T>(array, j)) < 0)
                {
                    UnsafeUtility.WriteArrayElement<T>(array, j + 1, UnsafeUtility.ReadArrayElement<T>(array, j));
                    j--;
                }
                UnsafeUtility.WriteArrayElement<T>(array, j + 1, t);
            }
        }

        unsafe static int Partition<T, U>(void* array, int lo, int hi, U comp) where T : struct where U : IComparer<T>
        {
            int mid = lo + ((hi - lo) / 2);
            SwapIfGreaterWithItems<T, U>(array, lo, mid, comp);
            SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
            SwapIfGreaterWithItems<T, U>(array, mid, hi, comp);

            T pivot = UnsafeUtility.ReadArrayElement<T>(array, mid);
            Swap<T>(array, mid, hi - 1);
            int left = lo, right = hi - 1;

            while (left < right)
            {
                while (comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, ++left)) > 0) ;
                while (comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, --right)) < 0) ;

                if (left >= right)
                    break;

                Swap<T>(array, left, right);
            }

            Swap<T>(array, left, (hi - 1));
            return left;
        }

        unsafe static void HeapSort<T, U>(void* array, int lo, int hi, U comp) where T : struct where U : IComparer<T>
        {
            int n = hi - lo + 1;

            for (int i = n / 2; i >= 1; i--)
            {
                Heapify<T, U>(array, i, n, lo, comp);
            }

            for (int i = n; i > 1; i--)
            {
                Swap<T>(array, lo, lo + i - 1);
                Heapify<T, U>(array, 1, i - 1, lo, comp);
            }
        }

        unsafe static void Heapify<T, U>(void* array, int i, int n, int lo, U comp) where T : struct where U : IComparer<T>
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lo + i - 1);
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;
                if (child < n && (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1), UnsafeUtility.ReadArrayElement<T>(array, (lo + child))) < 0))
                {
                    child++;
                }
                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, (lo + child - 1)), val) < 0)
                    break;

                UnsafeUtility.WriteArrayElement<T>(array, lo + i - 1, UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1));
                i = child;
            }
            UnsafeUtility.WriteArrayElement(array, lo + i - 1, val);
        }

        unsafe static void Swap<T>(void* array, int lhs, int rhs) where T : struct
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lhs);
            UnsafeUtility.WriteArrayElement<T>(array, lhs, UnsafeUtility.ReadArrayElement<T>(array, rhs));
            UnsafeUtility.WriteArrayElement<T>(array, rhs, val);
        }

        unsafe static void SwapIfGreaterWithItems<T, U>(void* array, int lhs, int rhs, U comp) where T : struct where U : IComparer<T>
        {
            if (lhs != rhs)
            {
                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lhs), UnsafeUtility.ReadArrayElement<T>(array, rhs)) > 0)
                {
                    Swap<T>(array, lhs, rhs);
                }
            }
        }
    }

    public static class StructComparer<T> where T : struct
    {
        public static readonly IComparer<T> Default;

        static StructComparer()
        {            
            if (typeof(T) == typeof(int))
                Default = new DefaultIntComparer() as IComparer<T>;            
            else if (typeof(T) == typeof(float))
                Default = new DefaultFloatComparer() as IComparer<T>;
            else if (typeof(T) == typeof(double))
                Default = new DefaultDoubleComparer() as IComparer<T>;
            else if (typeof(T) == typeof(bool))
                Default = new DefaultBoolComparer() as IComparer<T>;
            else
                throw new InvalidOperationException("Unsupported default StructComparer for " + typeof(T));
        }

        public struct DefaultComparer<T1> : IComparer<T1> where T1 : IComparable<T1>
        {
            public int Compare(T1 x, T1 y) => x.CompareTo(y);
        }

        public struct DefaultFloatComparer : IComparer<float>
        {            
            public int Compare(float a, float b) => a < b ? -1 : a > b ? 1 : 0;
        }

        public struct DefaultIntComparer : IComparer<int>
        {
            public int Compare(int a, int b) => a < b ? -1 : a > b ? 1 : 0;
        }

        public struct DefaultDoubleComparer : IComparer<double>
        {
            public int Compare(double a, double b) => a < b ? -1 : a > b ? 1 : 0;
        }

        public struct DefaultBoolComparer : IComparer<bool>
        {
            public int Compare(bool a, bool b) => a == b ? 0 : !a ? -1 : 1;
        }
    }

    public interface IComparer<in T, in TParam>
    {
        int Compare(T a, T b, TParam param);
    }

    internal sealed class NativeBufferDebugView<T> where T : struct
    {
        private NativeBuffer<T> Buffer;

        public NativeBufferDebugView(NativeBuffer<T> buffer)
        {
            Buffer = buffer;
        }

        public T[] Items => Buffer.ToArray();
    }

    /// <summary>
    /// NativeBuffer is a version of NativeArray<T> that satisfies the 'unmanaged' constraint
    /// </summary>
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct NativeBuffer : IDisposable //<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal unsafe void* m_Buffer;
        internal int m_Length;
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal int itemSize;


        public int Length => m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        //[NativeSetClassTypeToNullOnSchedule]
        //internal DisposeSentinel m_DisposeSentinel;

        internal int m_AllocatorLabel;

        public static unsafe NativeBuffer Assign(void* ptr, int itemSize, int length)
        {
            NativeBuffer buffer;
            buffer.m_Buffer = ptr;
            buffer.m_Length = length;
            buffer.itemSize = itemSize;
            buffer.m_MinIndex = 0;
            buffer.m_MaxIndex = length - 1;
            buffer.m_AllocatorLabel = (int)Allocator.Invalid;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            buffer.m_Safety = AtomicSafetyHandle.Create();
#endif            
            return buffer;
        }

        public static unsafe NativeBuffer Assign<T>(void* ptr, int length) where T : struct
        {
            return Assign(ptr, UnsafeUtility.SizeOf<T>(), length);
        }

        public static unsafe NativeBuffer Create<T>(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            Allocate<T>(length, allocator, out NativeBuffer buffer);

            if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return buffer;

            buffer.Clear();
            return buffer;
        }

        public static NativeBuffer Create(int length, int itemSize, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, itemSize, UnsafeUtility.AlignOf<int>(), allocator, out NativeBuffer buffer);

            if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return buffer;

            buffer.Clear();
            return buffer;
        }

        public unsafe void Clear()
        {
            UnsafeUtility.MemClear(m_Buffer, (long)Length * itemSize);
        }

        public static NativeBuffer Create<T>(T[] array, Allocator allocator) where T : struct
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            Allocate<T>(array.Length, allocator, out NativeBuffer buffer);
            Copy(array, buffer);
            return buffer;
        }

        public static NativeBuffer Create<T>(NativeBuffer array, Allocator allocator) where T : struct
        {
            Allocate<T>(array.Length, allocator, out NativeBuffer buffer);
            Copy<T>(array, buffer);
            return buffer;
        }

        private static unsafe void Allocate<T>(int length, Allocator allocator, out NativeBuffer array) where T : struct
        {
            IsBlittableAndThrow<T>();
            Allocate(length, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), allocator, out array);
        }

        private static unsafe void Allocate(int length, int itemSize, int align, Allocator allocator, out NativeBuffer array)
        {
            long size = itemSize * (long)length;
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");            
            if (size > (long)int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length), string.Format("Length * sizeof(T) cannot exceed {0} bytes", (object)int.MaxValue));

            array.m_Buffer = UnsafeUtility.Malloc(size, align, allocator);
            array.m_Length = length;
            array.m_AllocatorLabel = (int)allocator;
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            array.itemSize = itemSize;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_Safety = AtomicSafetyHandle.Create();
#endif
            //DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
        }

        [BurstDiscard]
        internal static void IsBlittableAndThrow<T>() where T : struct
        {            
            if (!UnsafeUtility.IsBlittable<T>())
                throw new InvalidOperationException(string.Format("{0} used in NativeArray2<{1}> must be blittable.", (object)typeof(T), (object)typeof(T)));
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementReadAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementWriteAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementWriteAccess(int index, int offset)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);

            if (offset >= itemSize)
                throw new ArgumentOutOfRangeException("Offset within an item cannot be larger than the item size");

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        //public unsafe T this[int index]
        //{
        //    get
        //    {
        //        CheckElementReadAccess(index);
        //        return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
        //    }
        //    [WriteAccessRequired]
        //    set
        //    {
        //        CheckElementWriteAccess(index);
        //        UnsafeUtility.WriteArrayElement<T>(m_Buffer, index, value);
        //    }
        //}

        public unsafe T GetItem<T>(int index)
        {     
            CheckElementReadAccess(index);
            return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
        }

        public unsafe ref T AsRef<T>(int index) where T : struct
        {     
            return ref UnsafeUtilityEx.AsRef<T>((void*)((IntPtr)m_Buffer + (UnsafeUtility.SizeOf<T>() * index)));
        }

        public unsafe ref T AsRef<T>(int index, int offset) where T : struct
        {
            return ref UnsafeUtilityEx.AsRef<T>((void*)((IntPtr)m_Buffer + (UnsafeUtility.SizeOf<T>() * index) + offset));
        }

        public unsafe T* AsPtr<T>(int index) where T : unmanaged
        {
            return (T*)((IntPtr)m_Buffer + UnsafeUtility.SizeOf<T>() * index);
        }

        public unsafe T* AsPtr<T>(int index, int offset) where T : unmanaged
        {
            return (T*)((IntPtr)m_Buffer + (UnsafeUtility.SizeOf<T>() * index) + offset);
        }

        public unsafe void* AsPtr(int index)
        {
            return (void*)((IntPtr)m_Buffer + itemSize * index);
        }

        public unsafe void SetItem<T>(int index, T value)
        {
            CheckElementWriteAccess(index);            
            UnsafeUtility.WriteArrayElement(m_Buffer, index, value);          
        }

        public unsafe void SetItem<T>(int index, T value, int offset)
        {
            CheckElementWriteAccess(index, offset);           
            UnsafeUtility.WriteArrayElement((void*)((IntPtr)m_Buffer + offset), index, value);
        }

        //private static unsafe void WriteArrayElementWithOffset<TU>(void* destination, int index, int stride, int offset, TU value) where TU : unmanaged
        //{
        //    *(TU*)((IntPtr)destination + index * stride + offset) = value;
        //}

        public unsafe bool IsCreated
        {
            get
            {
                return (IntPtr)m_Buffer != IntPtr.Zero;
            }
        }

        [WriteAccessRequired]
        public unsafe void Dispose()
        {
            if (!UnsafeUtility.IsValidAllocator((Allocator)m_AllocatorLabel))
            {
                throw new InvalidOperationException("The NativeArray2 can not be Disposed because it was not allocated with a valid allocator ("+ (Allocator)m_AllocatorLabel + ").");
            }

           // DisposeSentinel.Dispose(ref this.m_Safety, ref this.m_DisposeSentinel);

            UnsafeUtility.Free(m_Buffer, (Allocator)m_AllocatorLabel);
            m_Buffer = (void*)null;
            m_Length = 0;
        }

        //[WriteAccessRequired]
        //public void CopyFrom(T[] array)
        //{
        //    Copy(array, this);
        //}

        //[WriteAccessRequired]
        //public void CopyFrom(NativeArrayNoLeakDetection2<T> array)
        //{
        //    Copy(array, this);
        //}

        public void CopyTo<T>(T[] array) where T : struct
        {
            Copy(this, array);
        }

        public void CopyTo<T>(NativeArray<T> array) where T : struct
        {
            Copy(this, array);
        }

        //public void CopyTo<T>(NativeBuffer<T> buffer) where T : struct
        //{
        //    Copy(this, buffer);
        //}

        public void CopyTo<T>(NativeBuffer buffer) where T : struct
        {
            Copy<T>(this, buffer);
        }

        //public unsafe NativeArray<T> AsNativeArray<T>() where T : struct
        //{ 
        //    // todo why is this not working?
        //    return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_Buffer, Length, (Allocator)m_AllocatorLabel);
        //}

        //public unsafe NativeSlice<T> AsNativeSlice<T>(int start, int end) where T : struct
        //{
        //    return NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>(m_Buffer, UnsafeUtility.SizeOf<T>(), Length).Slice(start, end);
        //}

        public T[] ToArray<T>() where T : struct
        {
            T[] dst = new T[Length];
            Copy(this, dst);
            return dst;
        }

        public T[] ToArray<T>(int length) where T : struct
        {
            T[] dst = new T[length];
            Copy(this, dst, length);
            return dst;
        }

        private void FailOutOfRangeError(int index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                throw new IndexOutOfRangeException(string.Format("Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n", (object)index, (object)m_MinIndex, (object)m_MaxIndex) + "ReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.");
            throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", (object)index, (object)Length));
        }

        //public Enumerator GetEnumerator()
        //{
        //    return new Enumerator(ref this);
        //}

        //IEnumerator<T> IEnumerable<T>.GetEnumerator()
        //{
        //    return (IEnumerator<T>)new Enumerator(ref this);
        //}

        //IEnumerator IEnumerable.GetEnumerator()
        //{
        //    return (IEnumerator)GetEnumerator();
        //}

        //public unsafe bool Equals(NativeArrayNoLeakDetection2<T> other)
        //{
        //    return m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        //}

        //public override bool Equals(object obj)
        //{
        //    if (ReferenceEquals((object)null, obj))
        //        return false;
        //    return obj is NativeArrayNoLeakDetection2<T> && Equals((NativeArrayNoLeakDetection2<T>)obj);
        //}

        //public override unsafe int GetHashCode()
        //{
        //    return (int)m_Buffer * 397 ^ m_Length;
        //}

        //public static bool operator ==(NativeArrayNoLeakDetection2<T> left, NativeArrayNoLeakDetection2<T> right)
        //{
        //    return left.Equals(right);
        //}

        //public static bool operator !=(NativeArrayNoLeakDetection2<T> left, NativeArrayNoLeakDetection2<T> right)
        //{
        //    return !left.Equals(right);
        //}

        public static void Copy<T>(NativeBuffer src, NativeBuffer dst) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

            Copy<T>(src, 0, dst, 0, src.Length);
        }

        public static void Copy<T>(NativeBuffer src, NativeArray<T> dst) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dst));
#endif
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

            Copy(src, 0, dst, 0, src.Length);
        }

//        public static void Copy<T>(NativeBuffer src, NativeBuffer<T> dst) where T : struct
//        {
//#if ENABLE_UNITY_COLLECTIONS_CHECKS
//            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
//            AtomicSafetyHandle.CheckWriteAndThrow(dst._buffer.m_Safety);
//#endif
//            if (src.Length != dst.Length)
//                throw new ArgumentException("source and destination length must be the same");

//            Copy<T>(src, 0, dst._buffer, 0, src.Length);
//        }

        public static void Copy<T>(T[] src, NativeBuffer dst) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy<T>(NativeBuffer src, T[] dst) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
#endif
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy<T>(NativeBuffer src, NativeBuffer dst, int length) where T : struct
        {
            Copy<T>(src, 0, dst, 0, length);
        }

        public static void Copy<T>(T[] src, NativeBuffer dst, int length) where T : struct
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy<T>(NativeBuffer src, T[] dst, int length) where T : struct
        {
            Copy(src, 0, dst, 0, length);
        }

        public static unsafe void Copy<T>(
          NativeBuffer src,
          int srcIndex,
          NativeBuffer dst,
          int dstIndex,
          int length) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");
            if (srcIndex < 0 || srcIndex > src.Length || srcIndex == src.Length && src.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray2.");
            if (dstIndex < 0 || dstIndex > dst.Length || dstIndex == dst.Length && dst.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray2.");
            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray2.", nameof(length));
            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray2.", nameof(length));
            UnsafeUtility.MemCpy((void*)((IntPtr)dst.m_Buffer + (dstIndex * UnsafeUtility.SizeOf<T>())), (void*)((IntPtr)src.m_Buffer + (srcIndex * UnsafeUtility.SizeOf<T>())), (long)(length * UnsafeUtility.SizeOf<T>()));
        }

        public static unsafe void Copy<T>(NativeBuffer src, int srcIndex, NativeArray<T> dst, int dstIndex, int length) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dst));
#endif
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");
            if (srcIndex < 0 || srcIndex > src.Length || srcIndex == src.Length && src.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray2.");
            if (dstIndex < 0 || dstIndex > dst.Length || dstIndex == dst.Length && dst.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray2.");
            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray2.", nameof(length));
            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray2.", nameof(length));
            UnsafeUtility.MemCpy((void*)((IntPtr)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dst) + (dstIndex * UnsafeUtility.SizeOf<T>())), (void*)((IntPtr)src.m_Buffer + (srcIndex * UnsafeUtility.SizeOf<T>())), (long)(length * UnsafeUtility.SizeOf<T>()));
        }

        public static unsafe void Copy<T>(
          T[] src,
          int srcIndex,
          NativeBuffer dst,
          int dstIndex,
          int length) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");
            if (srcIndex < 0 || srcIndex > src.Length || srcIndex == src.Length && src.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source array.");
            if (dstIndex < 0 || dstIndex > dst.Length || dstIndex == dst.Length && dst.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray2.");
            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source array.", nameof(length));
            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray2.", nameof(length));
            GCHandle gcHandle = GCHandle.Alloc((object)src, GCHandleType.Pinned);
            IntPtr num = gcHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((void*)((IntPtr)dst.m_Buffer + (dstIndex * UnsafeUtility.SizeOf<T>())), (void*)(num + (srcIndex * UnsafeUtility.SizeOf<T>())), (long)(length * UnsafeUtility.SizeOf<T>()));
            gcHandle.Free();
        }

        public static unsafe void Copy<T>(
          NativeBuffer src,
          int srcIndex,
          T[] dst,
          int dstIndex,
          int length) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
#endif
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");
            if (srcIndex < 0 || srcIndex > src.Length || srcIndex == src.Length && src.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray2.");
            if (dstIndex < 0 || dstIndex > dst.Length || dstIndex == dst.Length && dst.Length > 0)
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination array.");
            if (srcIndex + length > src.Length)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray2.", nameof(length));
            if (dstIndex + length > dst.Length)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination array.", nameof(length));
            GCHandle gcHandle = GCHandle.Alloc((object)dst, GCHandleType.Pinned);
            UnsafeUtility.MemCpy((void*)((IntPtr)(void*)gcHandle.AddrOfPinnedObject() + (dstIndex * UnsafeUtility.SizeOf<T>())), (void*)((IntPtr)src.m_Buffer + (srcIndex * UnsafeUtility.SizeOf<T>())), (long)(length * UnsafeUtility.SizeOf<T>()));
            gcHandle.Free();
        }

        /// <summary>
        /// Calculate an element index based on its memory address
        /// </summary>
        public unsafe int IndexOf(void* elementPtr)
        {
            if (elementPtr == null)
                throw new ArgumentNullException(nameof(elementPtr));

            int offset = (int)elementPtr - (int)(IntPtr)m_Buffer;
            int index = offset / itemSize;

            if (index < m_MinIndex || index > m_MaxIndex)
                throw new ArgumentOutOfRangeException($"Index '{index}' is out of range ({m_MinIndex}-{m_MaxIndex})");

            return index;
        }

        //[ExcludeFromDocs]
        //public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        //{
        //    private NativeArrayNoLeakDetection2<T> m_Array;
        //    private int m_Index;

        //    public Enumerator(ref NativeArrayNoLeakDetection2<T> array)
        //    {
        //        m_Array = array;
        //        m_Index = -1;
        //    }

        //    public void Dispose()
        //    {
        //    }

        //    public bool MoveNext()
        //    {
        //        ++m_Index;
        //        return m_Index < m_Array.Length;
        //    }

        //    public void Reset()
        //    {
        //        m_Index = -1;
        //    }

        //    public T Current
        //    {
        //        get
        //        {
        //            return m_Array[m_Index];
        //        }
        //    }

        //    object IEnumerator.Current
        //    {
        //        get
        //        {
        //            return (object)Current;
        //        }
        //    }
        //}

        //#if ENABLE_UNITY_COLLECTIONS_CHECKS
        //        public AtomicSafetyHandle GetAtomicSafetyHandle(NativeArray<T> array) 
        //        {
        //            return m_Safety;
        //        }
        //#endif

        //#if ENABLE_UNITY_COLLECTIONS_CHECKS
        //        public void SetAtomicSafetyHandle(ref NativeArray<T> array, AtomicSafetyHandle safety)
        //        {
        //            m_Safety = safety;
        //        }
        //#endif


        public unsafe void* GetUnsafePtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return m_Buffer;
        }

        public unsafe void* GetUnsafeReadOnlyPtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return m_Buffer;
        }

        public unsafe void* GetUnsafeBufferPointerWithoutChecks()
        {
            return m_Buffer;
        }



    }


}
