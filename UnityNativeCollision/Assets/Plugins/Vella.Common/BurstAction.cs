using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Vella.Common
{
    public interface IBurstAction<T1, T2, T3> : IBurstOperation
    {
        void Execute(T1 arg1, T2 arg2, T3 arg3);
    }

    public interface IBurstAction<T1, T2> : IBurstOperation
    {
        void Execute(T1 arg1, T2 arg2);
    }

    public interface IBurstAction<T1> : IBurstOperation
    {
        void Execute(T1 arg1);
    }

    public interface IBurstAction : IBurstOperation
    {
        void Execute();
    }

    [BurstCompile]
    public struct BurstAction<TFunc, T1, T2, T3> : IJob
        where TFunc : struct, IBurstAction<T1, T2, T3>
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            func.Execute(arg1, arg2, arg3);
        }

        public static unsafe void Run(TFunc func, T1 arg1, T2 arg2, T3 arg3)
        {
            new BurstAction<TFunc, T1, T2, T3>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),

            }.Run();
        }
    }

    [BurstCompile]
    public struct BurstAction<TFunc, T1, T2> : IJob
        where TFunc : struct, IBurstAction<T1, T2>
        where T1 : struct
        where T2 : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            func.Execute(arg1, arg2);
        }

        public static unsafe void Run(TFunc func, T1 arg1, T2 arg2)
        {
            new BurstAction<TFunc, T1, T2>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),

            }.Run();
        }
    }

    [BurstCompile]
    public struct BurstAction<TFunc, T1> : IJob
        where TFunc : struct, IBurstAction<T1>
        where T1 : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            func.Execute(arg1);
        }

        public static unsafe void Run(TFunc func, T1 arg1)
        {
            new BurstAction<TFunc, T1>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),

            }.Run();
        }
    }

    [BurstCompile]
    public struct BurstAction<TFunc> : IJob
        where TFunc : struct, IBurstAction
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            func.Execute();
        }

        public static unsafe void Run(TFunc func)
        {
            new BurstAction<TFunc>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),

            }.Run();
        }
    }
}