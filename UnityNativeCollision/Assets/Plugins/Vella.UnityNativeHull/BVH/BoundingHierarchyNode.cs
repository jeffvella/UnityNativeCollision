// Copyright(C) David W. Jeske, 2014, and released to the public domain. 
//
// Dynamic BVH (Bounding Volume Hierarchy) using incremental refit and tree-rotations
//
// initial BVH build based on: Bounding Volume Hierarchies (BVH) – A brief tutorial on what they are and how to implement them
//              http://www.3dmuve.com/3dmblog/?p=182
//
// Dynamic Updates based on: "Fast, Effective BVH Updates for Animated Scenes" (Kopta, Ize, Spjut, Brunvand, David, Kensler)
//              http://www.cs.utah.edu/~thiago/papers/rotations.pdf
//
// see also:  Space Partitioning: Octree vs. BVH
//            http://thomasdiewald.com/blog/?p=1488
//
// TODO: pick the best axis to split based on SAH, instead of the biggest
// TODO: Switch SAH comparisons to use (SAH(A) * itemCount(A)) currently it just uses SAH(A)
// TODO: when inserting, compare parent node SAH(A) * itemCount to sum of children, to see if it is better to not split at all
// TODO: implement node merge/split, to handle updates when LEAF_OBJ_MAX > 1
// 
// TODO: implement SBVH spacial splits
//        http://www.nvidia.com/docs/IO/77714/sbvh.pdf

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vella.Common;

namespace SimpleScene.Util.ssBVH
{
    public enum Rot
    {
        None,
        LRl,
        LRr,
        RLl,
        RLr,
        LlRr,
        LlRl
    }

    internal struct RotOpt //: IComparable<RotOpt>
    {
        public Rot Rot; // rotation option
        public float Sah;

        internal RotOpt(float sah, Rot rot)
        {
            Sah = sah;
            Rot = rot;
        }

        //public int CompareTo(RotOpt other)
        //{
        //    return Sah.CompareTo(other.Sah);
        //}
    }

    public interface IBoundingHierarchyNode
    {
        float3 Position { get; }
        float Radius { get; }
        ref int HasChanged { get; }
    }

    [DebuggerDisplay("Node<{0}>:{1}")]
    public unsafe partial struct Node
    {
        public BoundingBox Box;
        public Node* Ptr;
        public Node* Left;
        public Node* Right;
        public Node* Parent;
        public int Depth;
        public int NodeNumber;
        public int BucketIndex;

        public bool IsLeaf => BucketIndex != -1 && Left != null && Right != null;

        public bool IsValid => (IntPtr)Ptr != IntPtr.Zero;

    }

    [DebuggerDisplay("Node<{0}>:{1}")]
    public unsafe partial struct Node
    {
        //public ref BoundingHierarchyNode AsRef() => ref *BaseAddress;





        //private Axis NextAxis(Axis cur)
        //{
        //    switch (cur)
        //    {
        //        case Axis.X: return Axis.Y;
        //        case Axis.Y: return Axis.Z;
        //        case Axis.Z: return Axis.X;
        //        default: throw new NotSupportedException();
        //    }
        //}




        //public ref NativeBuffer<T> Bucket<T>(IBoundingHierarchyAdapter<T> adapter) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    return ref adapter.BVH.GetBucket(BucketIndex);
        //}








        //internal static float SAofList<T>(IBoundingHierarchyAdapter<T> nAda, List<T> list) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    var box = AabBofObj(nAda, list[0]);
        //    for (var i = 1; i < list.Count; i++)
        //    {       
        //        var newbox = AabBofObj(nAda, list[i]);
        //        box.ExpandBy(newbox);
        //    }
        //    return Sa(box);
        //}



        //// The list of all candidate rotations, from "Fast, Effective BVH Updates for Animated Scenes", Figure 1.
        //internal enum Rot
        //{
        //    None,
        //    LRl,
        //    LRr,
        //    RLl,
        //    RLr,
        //    LlRr,
        //    LlRl
        //}




        //[DebuggerBrowsable(DebuggerBrowsableState.Never)]
        //private static List<Rot> EachRot => new List<Rot>((Rot[])Enum.GetValues(typeof(Rot)));










        //private static IndexedAxes _axes;

        //public struct IndexedAxes : IReadIndexed<Axis>
        //{
        //    public const int ItemCount = 3;

        //    private fixed int _values[ItemCount];

        //    public Axis this[int index] => (Axis)_values[index];

        //    public int Length => ItemCount;
        //}

        //public static TSource Min<TSource>(ref IReadIndexed<TSource> source) //where TSource : INativeComparable<TSource>
        //{            
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));

        //    Comparer<TSource> comparer = Comparer<TSource>.Default;
        //    TSource y = default;
        //    if (y == null)
        //    {
        //        for (int i = 0; i < source.Length; i++)
        //        {
        //            TSource x = source[i];
        //            if (x != null && (y == null || comparer.Compare(x, y) < 0))
        //                y = x;
        //        }
        //        return y;
        //    }
        //    var flag = false;
        //    for (int i = 0; i < source.Length; i++)
        //    {
        //        TSource x = source[i];
        //        if (flag)
        //        {
        //            if (comparer.Compare(x, y) < 0)
        //                y = x;
        //        }
        //        else
        //        {
        //            y = x;
        //            flag = true;
        //        }
        //    }
        //    if (flag)
        //        return y;

        //    throw new InvalidOperationException("No Elements");
        //    //throw Error.NoElements();
        //}

        /*

                int IComparer.Compare(object x, object y) {
                    if (x == null) return y == null ? 0 : -1;
                    if (y == null) return 1;
                    if (x is T && y is T) return Compare((T)x, (T)y);
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArgumentForComparison);
                    return 0;
                }
            }

            [Serializable]
            internal class GenericComparer<T> : Comparer<T> where T: IComparable<T>
            {    
                public override int Compare(T x, T y) {
                    if (x != null) {
                        if (y != null) return x.CompareTo(y);
                        return 1;
                    }
                    if (y != null) return -1;
                    return 0;
                }

                // Equals method for the comparer itself. 
                public override bool Equals(Object obj){
                    GenericComparer<T> comparer = obj as GenericComparer<T>;
                    return comparer != null;
                }        

                public override int GetHashCode() {
                    return this.GetType().Name.GetHashCode();
                }
            }             


         */


        //[DebuggerBrowsable(DebuggerBrowsableState.Never)]
        //private static List<Axis> EachAxis => new List<Axis>((Axis[])Enum.GetValues(typeof(Axis)));



 

        //private static SplitAxisOpt<T> MinSplitAxisOpt<T>(SplitAxisOpt<T> x, SplitAxisOpt<T> y, SplitAxisOpt<T> z) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    return x.Sah < y.Sah ? x.Sah < z.Sah ? x : z : z.Sah < y.Sah ? z : y;
        //}









    }


}