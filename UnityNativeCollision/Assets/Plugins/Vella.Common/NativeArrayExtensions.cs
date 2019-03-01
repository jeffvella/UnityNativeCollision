using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Vella.Common
{
    public static class NativeArrayExtensions
    {
        public static unsafe ref T AsRef<T>(this NativeArray<T> arr, int index) where T : unmanaged
        {      
            return ref ((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(arr))[index];
        }

        public static T[] ToArray<T>(this NativeArray<T> arr, int length) where T : unmanaged
        {
            var dst = new T[length];
            NativeArray<T>.Copy(arr, dst, length);
            return dst;
        }
    }
}
