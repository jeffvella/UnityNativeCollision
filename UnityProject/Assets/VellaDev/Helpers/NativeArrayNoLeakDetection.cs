// Decompiled with JetBrains decompiler
// Type: Unity.Collections.NativeArray2`1
// Assembly: UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 32BFBF54-11E9-4CC9-9520-34CE46722C3A
// Assembly location: X:\UnityEditors\2019.1.0a14\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Internal;

namespace Unity.Collections
{
    /// <summary>
    ///   <para>A NativeArray2 exposes a buffer of native memory to managed code, making it possible to share data between managed and native without marshalling costs.</para>
    /// </summary>
    [DebuggerTypeProxy(typeof(NativeArray2DebugView<>))]
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NativeContainerSupportsDeferredConvertListToArray]
    public struct NativeArrayNoLeakDetection<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArrayNoLeakDetection<T>>, IEnumerable
      where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal unsafe void* m_Buffer;
        internal int m_Length;
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;

        //[NativeSetClassTypeToNullOnSchedule]
        //internal DisposeSentinel m_DisposeSentinel;

        internal Allocator m_AllocatorLabel;

        public unsafe NativeArrayNoLeakDetection(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return;
            UnsafeUtility.MemClear(m_Buffer, (long)Length * (long)UnsafeUtility.SizeOf<T>());
        }

        public NativeArrayNoLeakDetection(T[] array, Allocator allocator)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            Allocate(array.Length, allocator, out this);
            Copy(array, this);
        }

        public NativeArrayNoLeakDetection(NativeArrayNoLeakDetection<T> array, Allocator allocator)
        {
            Allocate(array.Length, allocator, out this);
            Copy(array, this);
        }

        private static unsafe void Allocate(int length, Allocator allocator, out NativeArrayNoLeakDetection<T> array)
        {
            long size = (long)UnsafeUtility.SizeOf<T>() * (long)length;
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            IsBlittableAndThrow();
            if (size > (long)int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length), string.Format("Length * sizeof(T) cannot exceed {0} bytes", (object)int.MaxValue));
            array.m_Buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), allocator);
            array.m_Length = length;
            array.m_AllocatorLabel = allocator;
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            array.m_Safety = AtomicSafetyHandle.Create();
            //DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator);
        }

        public int Length
        {
            get
            {
                return m_Length;
            }
        }

        [BurstDiscard]
        internal static void IsBlittableAndThrow()
        {
            if (!UnsafeUtility.IsBlittable<T>())
                throw new InvalidOperationException(string.Format("{0} used in NativeArray2<{1}> must be blittable.", (object)typeof(T), (object)typeof(T)));
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementReadAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);

            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementWriteAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);

            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
        }

        public unsafe T this[int index]
        {
            get
            {
                CheckElementReadAccess(index);
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            }
            [WriteAccessRequired]
            set
            {
                CheckElementWriteAccess(index);
                UnsafeUtility.WriteArrayElement<T>(m_Buffer, index, value);
            }
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
            if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
                throw new InvalidOperationException("The NativeArray2 can not be Disposed because it was not allocated with a valid allocator.");

           // DisposeSentinel.Dispose(ref this.m_Safety, ref this.m_DisposeSentinel);

            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = (void*)null;
            m_Length = 0;
        }

        [WriteAccessRequired]
        public void CopyFrom(T[] array)
        {
            Copy(array, this);
        }

        [WriteAccessRequired]
        public void CopyFrom(NativeArrayNoLeakDetection<T> array)
        {
            Copy(array, this);
        }

        public void CopyTo(T[] array)
        {
            Copy(this, array);
        }

        public void CopyTo(NativeArrayNoLeakDetection<T> array)
        {
            Copy(this, array);
        }

        public T[] ToArray()
        {
            T[] dst = new T[Length];
            Copy(this, dst, Length);
            return dst;
        }

        private void FailOutOfRangeError(int index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                throw new IndexOutOfRangeException(string.Format("Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n", (object)index, (object)m_MinIndex, (object)m_MaxIndex) + "ReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.");
            throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", (object)index, (object)Length));
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return (IEnumerator<T>)new Enumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }

        public unsafe bool Equals(NativeArrayNoLeakDetection<T> other)
        {
            return m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals((object)null, obj))
                return false;
            return obj is NativeArrayNoLeakDetection<T> && Equals((NativeArrayNoLeakDetection<T>)obj);
        }

        public override unsafe int GetHashCode()
        {
            return (int)m_Buffer * 397 ^ m_Length;
        }

        public static bool operator ==(NativeArrayNoLeakDetection<T> left, NativeArrayNoLeakDetection<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativeArrayNoLeakDetection<T> left, NativeArrayNoLeakDetection<T> right)
        {
            return !left.Equals(right);
        }

        public static void Copy(NativeArrayNoLeakDetection<T> src, NativeArrayNoLeakDetection<T> dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(T[] src, NativeArrayNoLeakDetection<T> dst)
        {
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArrayNoLeakDetection<T> src, T[] dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            if (src.Length != dst.Length)
                throw new ArgumentException("source and destination length must be the same");
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArrayNoLeakDetection<T> src, NativeArrayNoLeakDetection<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(T[] src, NativeArrayNoLeakDetection<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(NativeArrayNoLeakDetection<T> src, T[] dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static unsafe void Copy(
          NativeArrayNoLeakDetection<T> src,
          int srcIndex,
          NativeArrayNoLeakDetection<T> dst,
          int dstIndex,
          int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
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

        public static unsafe void Copy(
          T[] src,
          int srcIndex,
          NativeArrayNoLeakDetection<T> dst,
          int dstIndex,
          int length)
        {
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
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

        public static unsafe void Copy(
          NativeArrayNoLeakDetection<T> src,
          int srcIndex,
          T[] dst,
          int dstIndex,
          int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
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

        [ExcludeFromDocs]
        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private NativeArrayNoLeakDetection<T> m_Array;
            private int m_Index;

            public Enumerator(ref NativeArrayNoLeakDetection<T> array)
            {
                m_Array = array;
                m_Index = -1;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                ++m_Index;
                return m_Index < m_Array.Length;
            }

            public void Reset()
            {
                m_Index = -1;
            }

            public T Current
            {
                get
                {
                    return m_Array[m_Index];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return (object)Current;
                }
            }
        }

        public AtomicSafetyHandle GetAtomicSafetyHandle(NativeArray<T> array) 
        {
            return m_Safety;
        }

        public void SetAtomicSafetyHandle(ref NativeArray<T> array, AtomicSafetyHandle safety)
        {
            m_Safety = safety;
        }

        public unsafe void* GetUnsafePtr()
        {
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            return m_Buffer;
        }

        public unsafe void* GetUnsafeReadOnlyPtr()
        {
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            return m_Buffer;
        }

        public unsafe void* GetUnsafeBufferPointerWithoutChecks()
        {
            return m_Buffer;
        }

    }

    internal sealed class NativeArray2DebugView<T> where T : struct
    {
        private NativeArrayNoLeakDetection<T> m_Array;

        public NativeArray2DebugView(NativeArrayNoLeakDetection<T> array)
        {
            m_Array = array;
        }

        public T[] Items
        {
            get
            {
                return m_Array.ToArray();
            }
        }
    }
}
