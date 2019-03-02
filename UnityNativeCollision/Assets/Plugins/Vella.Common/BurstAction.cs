using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.Bindings;

namespace Vella.Common
{

    public interface IBurstRefAction<T1, T2, T3, T4, T5> : IBurstOperation
    {
        void Execute(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    }

    public interface IBurstAction<T1, T2, T3, T4, T5> : IBurstOperation
    {
        void Execute(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    }

    public interface IBurstAction<T1, T2, T3, T4> : IBurstOperation
    {
        void Execute(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }

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
    public struct BurstRefAction<TFunc, T1, T2, T3, T4, T5> : IJob
        where TFunc : struct, IBurstRefAction<T1, T2, T3, T4, T5>
        where T1 : unmanaged
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe T1* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument4Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument5Ptr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);
            UnsafeUtility.CopyPtrToStructure(Argument5Ptr, out T5 arg5);

            func.Execute(ref *Argument1Ptr, arg2, arg3, arg4, arg5);
        }

        public static unsafe void Run(TFunc func, ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {   
            new BurstRefAction<TFunc, T1, T2, T3, T4, T5>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = (T1*)UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),
                Argument5Ptr = UnsafeUtility.AddressOf(ref arg5),

            }.Run();
        }
    }

    [BurstCompile]
    public struct BurstAction<TFunc, T1, T2, T3, T4, T5> : IJob
    where TFunc : struct, IBurstAction<T1, T2, T3, T4, T5>
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct
    where T5 : struct
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
        public unsafe void* Argument5Ptr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);
            UnsafeUtility.CopyPtrToStructure(Argument5Ptr, out T5 arg5);
            func.Execute(arg1, arg2, arg3, arg4, arg5);
        }

        public static unsafe void Run(TFunc func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            new BurstAction<TFunc, T1, T2, T3, T4, T5>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),
                Argument5Ptr = UnsafeUtility.AddressOf(ref arg5),

            }.Run();
        }
    }

    [BurstCompile]
    public struct BurstAction<TFunc, T1, T2, T3, T4> : IJob
    where TFunc : struct, IBurstAction<T1, T2, T3, T4>
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct
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

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);
            func.Execute(arg1, arg2, arg3, arg4);
        }

        public static unsafe void Run(TFunc func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            new BurstAction<TFunc, T1, T2, T3, T4>
            {
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),

            }.Run();
        }
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
