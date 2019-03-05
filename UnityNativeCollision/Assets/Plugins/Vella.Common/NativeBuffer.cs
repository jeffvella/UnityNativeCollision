using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        NativeBuffer _buffer;
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

        public NativeBuffer(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            _buffer = NativeBuffer.Create<T>(length, allocator, options);
            _maxIndex = -1;
        }

        public unsafe ref T this[int i] => ref _buffer.AsRef<T>(i);

        public int Add(T item)
        { 
            _buffer.SetItem(++_maxIndex, item);
            return _maxIndex;
        }

        public bool RemoveAt(int index)
        {
            if (index > 0 && index < _maxIndex)
            {   
                // Shuffle forward every item after the removed index
                for (int i = index + 1; i < _maxIndex; i++)
                {
                    _buffer.SetItem(i - 1, _buffer.GetItem<T>(i));          
                }
                _maxIndex--;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Overwrite the item to be removed with the last item in the collection.
        /// Very fast operation but does not maintain the order of items.
        /// </summary>  
        public void RemoveAtSwapBack(int index)
        {
            _buffer.SetItem(index, _buffer.GetItem<T>(--_maxIndex));
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < Length; i++)
            {
                if (this[i].Equals(item))
                    return i;
            }
            return -1;
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

        public int Capacity => _buffer.Length;

        public int Length => _maxIndex + 1;

        public bool IsCreated => _buffer.IsCreated;

        public void Clear()
        {
            NativeBuffer.Clear<T>(_buffer);
            _maxIndex = -1;
        }

        public T[] ToArray() => _buffer.ToArray<T>(Length);

        public void Dispose() => _buffer.Dispose();

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

        public static unsafe NativeBuffer Assign<T>(void* ptr, int length) where T : struct
        {
            NativeBuffer buffer;
            buffer.m_Buffer = ptr;
            buffer.m_Length =
            buffer.itemSize = UnsafeUtility.SizeOf<T>();
            buffer.m_MinIndex = 0;
            buffer.m_MaxIndex = length - 1;
            buffer.m_AllocatorLabel = (int)Allocator.Invalid;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            buffer.m_Safety = AtomicSafetyHandle.Create();
#endif
            return buffer;
        }

        public static unsafe NativeBuffer Create<T>(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            Allocate<T>(length, allocator, out NativeBuffer buffer);

            if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return buffer;

            Clear<T>(buffer);
            return buffer;
        }

        public static unsafe void Clear<T>(NativeBuffer buffer) where T : struct
        {
            UnsafeUtility.MemClear(buffer.m_Buffer, (long)buffer.Length * (long)UnsafeUtility.SizeOf<T>());   
        }

        public static NativeBuffer Create<T>(T[] array, Allocator allocator) where T : struct
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            Allocate<T>(array.Length, allocator, out NativeBuffer buffer);
            Copy<T>(array, buffer);
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
            long size = (long)UnsafeUtility.SizeOf<T>() * (long)length;
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            IsBlittableAndThrow<T>();
            if (size > (long)int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length), string.Format("Length * sizeof(T) cannot exceed {0} bytes", (object)int.MaxValue));
            array.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), allocator);
            array.m_Length = length;
            array.m_AllocatorLabel = (int)allocator;
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            array.itemSize = UnsafeUtility.SizeOf<T>();
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

        public unsafe void SetItem<T>(int index, T value)
        {
            CheckElementWriteAccess(index);            
            UnsafeUtility.WriteArrayElement(m_Buffer, index, value);          
        }

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

            Copy<T>(src, 0, dst, 0, src.Length);
        }

        public static void Copy<T>(T[] src, NativeBuffer dst) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
#endif
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");
            Copy<T>(src, 0, dst, 0, src.Length);
        }

        public static void Copy<T>(NativeBuffer src, T[] dst) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
#endif
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");

            Copy<T>(src, 0, dst, 0, src.Length);
        }

        public static void Copy<T>(NativeBuffer src, NativeBuffer dst, int length) where T : struct
        {
            Copy<T>(src, 0, dst, 0, length);
        }

        public static void Copy<T>(T[] src, NativeBuffer dst, int length) where T : struct
        {
            Copy<T>(src, 0, dst, 0, length);
        }

        public static void Copy<T>(NativeBuffer src, T[] dst, int length) where T : struct
        {
            Copy<T>(src, 0, dst, 0, length);
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
