using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Vella.Common
{
    public interface IBurstOperation
    {

    }

    public interface IBurstFunction<T1, T2, T3, T4, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }

    public interface IBurstFunction<T1, T2, T3, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2, T3 arg3);
    }

    public interface IBurstFunction<T1, T2, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2);
    }

    public interface IBurstFunction<T1, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1);
    }

    public interface IBurstFunction<TResult> : IBurstOperation
    {
        TResult Execute();
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, T3, T4, TResult> : IJob
    where TFunc : struct, IBurstFunction<T1, T2, T3, T4, TResult>
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct
    where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument4Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);

            result = func.Execute(arg1, arg2, arg3, arg4);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, T3, T4, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, T3, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, T2, T3, TResult>
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);

            result = func.Execute(arg1, arg2, arg3);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2, T3 arg3)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, T3, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, T2, TResult>
        where T1 : struct
        where T2 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);

            result = func.Execute(arg1, arg2);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, TResult>
        where T1 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);

            result = func.Execute(arg1);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, TResult> : IJob
        where TFunc : struct, IBurstFunction<TResult>
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);

            result = func.Execute();
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func)
        {
            TResult result = default;
            new BurstFunction<TFunc, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),

            }.Run();
            return result;
        }
    }
}
