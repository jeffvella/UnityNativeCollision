using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Assets.VellaDev.NewCollision
{
    public interface IConvertible<T> where T : struct { }

    public static class StructConversion
    {
        public static unsafe void Convert<TInput, TOutput>(TInput input, out TOutput output)
            where TInput : struct, IConvertible<TOutput>
            where TOutput : struct, IConvertible<TInput>
        {
            if (UnsafeUtility.SizeOf<TInput>() != UnsafeUtility.SizeOf<TOutput>())
            {
                throw new InvalidCastException(string.Format("Cannot convert type of '{0}' because their size is different: '{1}' and '{2}' respectively",
                    input, UnsafeUtility.SizeOf<TInput>(), UnsafeUtility.SizeOf<TOutput>()));
            }

            UnsafeUtility.CopyPtrToStructure(UnsafeUtility.AddressOf(ref input), out output);
        }

        public static unsafe TOutput Convert<TInput, TOutput>(TInput input)
            where TInput : struct, IConvertible<TOutput>
            where TOutput : struct, IConvertible<TInput>
        {
            if (UnsafeUtility.SizeOf<TInput>() != UnsafeUtility.SizeOf<TOutput>())
            {
                throw new InvalidCastException(string.Format("Cannot convert type of '{0}' because their size is different: '{1}' and '{2}' respectively",
                    input, UnsafeUtility.SizeOf<TInput>(), UnsafeUtility.SizeOf<TOutput>()));
            } 
            UnsafeUtility.CopyPtrToStructure(UnsafeUtility.AddressOf(ref input), out TOutput output);
            return output;
        }

    }
}
