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
        public Node* BaseAddress;
        public Node* Left;
        public Node* Right;
        public Node* Parent;
        public int Depth;
        public int NodeNumber;
        public int BucketIndex;
    }

    [DebuggerDisplay("Node<{0}>:{1}")]
    public unsafe partial struct Node
    {
        //public ref BoundingHierarchyNode AsRef() => ref *BaseAddress;

        public bool IsValid => (IntPtr)BaseAddress != IntPtr.Zero;

        internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var node = bvh.CreateNode();
            node->BucketIndex = bvh.CreateBucket();
            node->NodeNumber = bvh.nodeCount++;
            return node;
        }

        internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh, List<T> gobjectlist) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            return CreateNode(bvh, null, gobjectlist, Axis.X, 0);
        }

        internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh, Node* lparent, List<T> gobjectlist, Axis lastSplitAxis, int curdepth, int bucketIndex = -1) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var node = bvh.CreateNode();
            var nAda = bvh.Adapter;

            node->NodeNumber = bvh.nodeCount++;
            node->Parent = lparent; // save off the parent BVHGObj Node
            node->Depth = curdepth;

            if (bvh.maxDepth < curdepth) bvh.maxDepth = curdepth;

            // Early out check due to bad data
            // If the list is empty then we have no BVHGObj, or invalid parameters are passed in
            if (gobjectlist == null || gobjectlist.Count < 1) throw new Exception("ssBVHNode constructed with invalid paramaters");


            if (bucketIndex < 0)
            {
                // new bucket
                node->BucketIndex = bvh.CreateBucket();
                ref var bucket = ref bvh.GetBucket(node->BucketIndex);

                for (var i = 0; i < gobjectlist.Count; i++)
                {
                    bucket.Add(gobjectlist[i]);
                    nAda.MapLeaf(gobjectlist[i], *node);
                }
            }
            else
            {
                node->BucketIndex = bucketIndex;
                ref var bucket = ref bvh.GetBucket(node->BucketIndex);
                bucket.Clear();

                for (var i = 0; i < gobjectlist.Count; i++)
                {
                    bucket.Add(gobjectlist[i]);
                    nAda.MapLeaf(gobjectlist[i], *node);
                }
            }


            // Check if we’re at our LEAF node, and if so, save the objects and stop recursing.  Also store the min/max for the leaf node and update the parent appropriately
            if (gobjectlist.Count <= bvh.LEAF_OBJ_MAX)
            {
                // once we reach the leaf node, we must set prev/next to null to signify the end
                node->Left = null;
                node->Right = null;

                bvh.ComputeVolume(node);
                //node->ComputeVolume(nAda);
                bvh.SplitIfNecessary(node);
                //node->SplitIfNecessary(nAda);
            }
            else
            {
                // --------------------------------------------------------------------------------------------
                // if we have more than (bvh.LEAF_OBJECT_COUNT) objects, then compute the volume and split
                //Items = gobjectlist;

                //node->ComputeVolume(nAda);
                nAda.BVH.ComputeVolume(node);
                node->SplitNode(nAda);
                node->ChildRefit(nAda, false);
            }

            return node;
        }

        public bool IsLeaf => BucketIndex != -1 && Left != null && Right != null;


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

        public void Refit_ObjectChanged<T>(IBoundingHierarchyAdapter<T> nAda, ref T obj) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            if (!IsLeaf) throw new Exception("dangling leaf!");
            if (RefitVolume(nAda))
            {
                if (Parent != null)
                {
                    nAda.BVH.refitNodes.Add(*Parent);
                }
            }
        }



        public ref NativeBuffer<T> Bucket<T>(IBoundingHierarchyAdapter<T> adapter) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            return ref adapter.BVH.GetBucket(BucketIndex);
        }

        public bool IsEmpty<T>(IBoundingHierarchyAdapter<T> nAda) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            return !IsLeaf || Bucket(nAda).Length == 0;
        }

        public int ItemCount<T>(IBoundingHierarchyAdapter<T> nAda) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            return BucketIndex >= 0 ? Bucket(nAda).Length : 0;
        }

        internal bool RefitVolume<T>(IBoundingHierarchyAdapter<T> nAda) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            //if (Items.Count == 0) { throw new NotImplementedException(); }  // TODO: fix this... we should never get called in this case...

            var oldbox = Box;

            //ComputeVolume(nAda);
            nAda.BVH.ComputeVolume(BaseAddress);
            if (!Box.Equals(oldbox))
            {
                if (Parent != null) Parent->ChildRefit(nAda);
                return true;
            }

            return false;
        }

        internal static float Sa(BoundingBox box)
        {
            var xSize = box.Max.x - box.Min.x;
            var ySize = box.Max.y - box.Min.y;
            var zSize = box.Max.z - box.Min.z;
            return 2.0f * (xSize * ySize + xSize * zSize + ySize * zSize);
        }

        internal static float Sa(ref BoundingBox box)
        {
            var xSize = box.Max.x - box.Min.x;
            var ySize = box.Max.y - box.Min.y;
            var zSize = box.Max.z - box.Min.z;
            return 2.0f * (xSize * ySize + xSize * zSize + ySize * zSize);
        }

        internal static float Sa(Node* node)
        {
            var xSize = node->Box.Max.x - node->Box.Min.x;
            var ySize = node->Box.Max.y - node->Box.Min.y;
            var zSize = node->Box.Max.z - node->Box.Min.z;
            return 2.0f * (xSize * ySize + xSize * zSize + ySize * zSize);
        }

        internal static float Sa<T>(IBoundingHierarchyAdapter<T> nAda, T obj) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var radius = nAda.Radius(obj);
            var size = radius * 2;
            return 6.0f * (size * size);
        }

        internal static BoundingBox AabBofPair(Node* nodea, Node* nodeb)
        {
            var box = nodea->Box;
            box.ExpandToFit(nodeb->Box);
            return box;
        }

        internal float SAofPair(Node nodea, Node nodeb)
        {
            var box = nodea.Box;
            box.ExpandToFit(nodeb.Box);
            return Sa(ref box);
        }

        internal float SAofPair(BoundingBox boxa, BoundingBox boxb)
        {
            var pairbox = boxa;
            pairbox.ExpandToFit(boxb);
            return Sa(ref pairbox);
        }

        internal static BoundingBox AabBofObj<T>(IBoundingHierarchyAdapter<T> nAda, T obj) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var radius = nAda.Radius(obj);
            BoundingBox box;
            box.Min.x = -radius;
            box.Max.x = radius;
            box.Min.y = -radius;
            box.Max.y = radius;
            box.Min.z = -radius;
            box.Max.z = radius;
            return box;
        }

        internal static float SAofList<T>(IBoundingHierarchyAdapter<T> nAda, List<T> list) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var box = AabBofObj(nAda, list[0]);
            for (var i = 1; i < list.Count; i++)
            {       
                var newbox = AabBofObj(nAda, list[i]);
                box.ExpandBy(newbox);
            }
            return Sa(box);
        }

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

        //[DebuggerBrowsable(DebuggerBrowsableState.Never)]
        //private static List<Rot> EachRot => new List<Rot>((Rot[])Enum.GetValues(typeof(Rot)));


        /// <summary>
        ///     tryRotate looks at all candidate rotations, and executes the rotation with the best resulting SAH (if any)
        /// </summary>
        /// <param name="bvh"></param>
        internal static void TryRotate<T>(NativeBoundingHierarchy<T> bvh, Node* node) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var nAda = bvh.Adapter;

            // if we are not a grandparent, then we can't rotate, so queue our parent and bail out
            if (node->Left->IsLeaf && node->Right->IsLeaf)
                if (node->Parent != null)
                {
                    bvh.refitNodes.Add(*node->Parent);
                    return;
                }

            // for each rotation, check that there are grandchildren as necessary (aka not a leaf)
            // then compute total SAH cost of our branches after the rotation.

            var mySa = Sa(node->Left) + Sa(node->Right);

            var bestRot = new RotOpt(float.MaxValue, Rot.None);
            for (var i = 0; i < _rotations.Length; i++)
            {
                var rot = RotOpt2(node, _rotations[i], mySa);
                if (rot.Sah < bestRot.Sah)
                    bestRot = rot;
            }

            //var bestRot = EachRot.Min(rot => { return RotOpt(node, rot, mySa); });

            // perform the best rotation...            
            if (bestRot.Rot != Rot.None)
            {
                // if the best rotation is no-rotation... we check our parents anyhow..                
                if (node->Parent != null)
                    if (DateTime.Now.Ticks % 100 < 2)
                        bvh.refitNodes.Add(*node->Parent);
            }
            else
            {
                if (node->Parent != null) bvh.refitNodes.Add(*node->Parent);

                if ((mySa - bestRot.Sah) / mySa < 0.3f) return; // the benefit is not worth the cost
                Console.WriteLine("BVH swap {0} from {1} to {2}", bestRot.Rot.ToString(), mySa, bestRot.Sah);

                // in order to swap we need to:
                //  1. swap the node locations
                //  2. update the depth (if child-to-grandchild)
                //  3. update the parent pointers
                //  4. refit the boundary box
                Node* swap = null;
                switch (bestRot.Rot)
                {
                    case Rot.None: break;
                    // child to grandchild rotations
                    case Rot.LRl:
                        swap = node->Left;
                        node->Left = node->Right->Left;
                        node->Left->Parent = node;
                        node->Right->Left = swap;
                        swap->Parent = node->Right;
                        node->Right->ChildRefit(nAda, false);
                        break;

                    case Rot.LRr:
                        swap = node->Left;
                        node->Left = node->Right->Right;
                        node->Left->Parent = node;
                        node->Right->Right = swap;
                        swap->Parent = node->Right;
                        node->Right->ChildRefit(nAda, false);
                        break;

                    case Rot.RLl:
                        swap = node->Right;
                        node->Right = node->Left->Left;
                        node->Right->Parent = node;
                        node->Left->Left = swap;
                        swap->Parent = node->Left;
                        node->Left->ChildRefit(nAda, false);
                        break;

                    case Rot.RLr:
                        swap = node->Right;
                        node->Right = node->Left->Right;
                        node->Right->Parent = node;
                        node->Left->Right = swap;
                        swap->Parent = node->Left;
                        node->Left->ChildRefit(nAda, false);
                        break;

                    // grandchild to grandchild rotations
                    case Rot.LlRr:
                        swap = node->Left->Left;
                        node->Left->Left = node->Right->Right;
                        node->Right->Right = swap;
                        node->Left->Left->Parent = node->Left;
                        swap->Parent = node->Right;
                        node->Left->ChildRefit(nAda, false);
                        node->Right->ChildRefit(nAda, false);
                        break;

                    case Rot.LlRl:
                        swap = node->Left->Left;
                        node->Left->Left = node->Right->Left;
                        node->Right->Left = swap;
                        node->Left->Left->Parent = node->Left;
                        swap->Parent = node->Right;
                        node->Left->ChildRefit(nAda, false);
                        node->Right->ChildRefit(nAda, false);
                        break;

                    // unknown...
                    default: throw new NotImplementedException("missing implementation for BVH Rotation .. " + bestRot.Rot);
                }

                // fix the depths if necessary....
                switch (bestRot.Rot)
                {
                    case Rot.LRl:
                    case Rot.LRr:
                    case Rot.RLl:
                    case Rot.RLr:
                        node->SetDepth(nAda, node->Depth);
                        break;
                }
            }
        }

        private static RotOpt RotOpt2(Node* node, Rot rot, float mySa)
        {
            switch (rot)
            {
                case Rot.None: return new RotOpt(mySa, Rot.None);
                // child to grandchild rotations
                case Rot.LRl:
                    if (node->Right->IsLeaf)
                        return new RotOpt(float.MaxValue, Rot.None);
                    else
                        return new RotOpt(Sa(node->Right->Left) + Sa(AabBofPair(node->Left, node->Right->Right)), rot);
                case Rot.LRr:
                    if (node->Right->IsLeaf)
                        return new RotOpt(float.MaxValue, Rot.None);
                    else
                        return new RotOpt(Sa(node->Right->Right) + Sa(AabBofPair(node->Left, node->Right->Left)), rot);
                case Rot.RLl:
                    if (node->Left->IsLeaf)
                        return new RotOpt(float.MaxValue, Rot.None);
                    else
                        return new RotOpt(Sa(AabBofPair(node->Right, node->Left->Right)) + Sa(node->Left->Left), rot);
                case Rot.RLr:
                    if (node->Left->IsLeaf)
                        return new RotOpt(float.MaxValue, Rot.None);
                    else
                        return new RotOpt(Sa(AabBofPair(node->Right, node->Left->Left)) + Sa(node->Left->Right), rot);
                // grandchild to grandchild rotations
                case Rot.LlRr:
                    if (node->Left->IsLeaf || node->Right->IsLeaf)
                        return new RotOpt(float.MaxValue, Rot.None);
                    else
                        return new RotOpt(Sa(AabBofPair(node->Right->Right, node->Left->Right)) + Sa(AabBofPair(node->Right->Left, node->Left->Left)), rot);
                case Rot.LlRl:
                    if (node->Left->IsLeaf || node->Right->IsLeaf)
                        return new RotOpt(float.MaxValue, Rot.None);
                    else
                        return new RotOpt(Sa(AabBofPair(node->Right->Left, node->Left->Right)) + Sa(AabBofPair(node->Left->Left, node->Right->Right)), rot);
                // unknown...
                default: throw new NotImplementedException("missing implementation for BVH Rotation SAH Computation .. " + rot);
            }
        }

        public interface IReadIndexed<out T>
        {
            T this[int index] { get; }
            int Length { get; }
        }

        private static IndexedRotations _rotations;

        public struct IndexedRotations : IReadIndexed<Rot>
        {
            public const int ItemCount = 7;

            private fixed int _values[ItemCount];

            public Rot this[int index] => (Rot)_values[index];

            public int Length => ItemCount;
        }

        private static IndexedAxes _axes;

        public struct IndexedAxes : IReadIndexed<Axis>
        {
            public const int ItemCount = 3;

            private fixed int _values[ItemCount];

            public Axis this[int index] => (Axis)_values[index];

            public int Length => ItemCount;
        }

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

        internal class SplitAxisOpt<T> // : IComparable<SplitAxisOpt<T>>
        {
            public Axis Axis;

            public List<T> Left, Right;

            // split Axis option
            public float Sah;

            internal SplitAxisOpt(float sah, Axis axis, List<T> left, List<T> right)
            {
                Sah = sah;
                Axis = axis;
                Left = left;
                Right = right;
            }

            //public int CompareTo(SplitAxisOpt<T> other)
            //{
            //    return Sah.CompareTo(other.Sah);
            //}
        }

        internal void SplitNode<T>(IBoundingHierarchyAdapter<T> adapter) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            // second, decide which axis to split on, and sort..
            //List<T> splitlist = Items;

            ref var bucket = ref Bucket(adapter);
            foreach (ref var item in bucket)
            {
                adapter.Unmap(item);
            }

            var splitlist = bucket.ToArray().ToList();

            //splitlist.ForEach(o => adapter.UnmapObject(o));


            var center = splitlist.Count / 2; // find the center object

            SplitAxisOpt<T> bestSplit = default; //new RotOpt(float.MaxValue, Rot.None);
            for (int i = 0; i < _axes.Length; i++)
            {
                var opt = SortMin(adapter, splitlist, _axes[i], center);
                if (bestSplit == null || opt.Sah < bestSplit.Sah)
                    bestSplit = opt;
            }


            //var x = SortMin(adapter, splitlist, Axis.X, center);
            //var y = SortMin(adapter, splitlist, Axis.Y, center);
            //var z = SortMin(adapter, splitlist, Axis.Z, center);
            //var bestSplit = MinSplitAxisOpt(x, y, z);


            //var bestSplit = EachAxis.Min(axis => { return SortMin(adapter, splitlist, axis, center); });

            //var bestSplit = EachAxis.Min(axis =>
            //{
            //    var orderedlist = new List<T>(splitlist);
            //    switch (axis)
            //    {
            //        case Axis.X:
            //            orderedlist.Sort(delegate(T go1, T go2)
            //            {
            //                return adapter.Position(go1).x.CompareTo(adapter.Position(go2).x);
            //            });
            //            break;

            //        case Axis.Y:
            //            orderedlist.Sort(delegate(T go1, T go2)
            //            {
            //                return adapter.Position(go1).y.CompareTo(adapter.Position(go2).y);
            //            });
            //            break;

            //        case Axis.Z:
            //            orderedlist.Sort(delegate(T go1, T go2)
            //            {
            //                return adapter.Position(go1).z.CompareTo(adapter.Position(go2).z);
            //            });
            //            break;
            //        default:
            //            throw new NotImplementedException("unknown split axis: " + axis.ToString());
            //    }

            //    var leftS = orderedlist.GetRange(0, center);
            //    var rightS = orderedlist.GetRange(center, splitlist.Count - center);

            //    var sah = SAofList(adapter, leftS) * leftS.Count + SAofList(adapter, rightS) * rightS.Count;

            //    return new SplitAxisOpt<T>(sah, axis, leftS, rightS);
            //});

            // perform the split
            var newLeftIndex = BucketIndex;
            var newRightIndex = adapter.BVH.CreateBucket();

            Left = CreateNode(adapter.BVH, BaseAddress, bestSplit.Left, bestSplit.Axis, Depth + 1, newLeftIndex); // Split the Hierarchy to the left
            Right = CreateNode(adapter.BVH, BaseAddress, bestSplit.Right, bestSplit.Axis, Depth + 1, newRightIndex); // Split the Hierarchy to the right      

            //Items = null;
            BucketIndex = -1;
        }

        //private static SplitAxisOpt<T> MinSplitAxisOpt<T>(SplitAxisOpt<T> x, SplitAxisOpt<T> y, SplitAxisOpt<T> z) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    return x.Sah < y.Sah ? x.Sah < z.Sah ? x : z : z.Sah < y.Sah ? z : y;
        //}

        private static SplitAxisOpt<T> SortMin<T>(IBoundingHierarchyAdapter<T> adapter, List<T> splitlist, Axis axis, int center) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var orderedlist = new List<T>(splitlist);
            switch (axis)
            {
                case Axis.X:
                    orderedlist.Sort((go1, go2) => adapter.Position(go1).x.CompareTo(adapter.Position(go2).x));
                    break;

                case Axis.Y:
                    orderedlist.Sort((go1, go2) => adapter.Position(go1).y.CompareTo(adapter.Position(go2).y));
                    break;

                case Axis.Z:
                    orderedlist.Sort((go1, go2) => adapter.Position(go1).z.CompareTo(adapter.Position(go2).z));
                    break;
                default:
                    throw new NotImplementedException("unknown split axis: " + axis);
            }

            var leftSplit = orderedlist.GetRange(0, center);
            var rightSplit = orderedlist.GetRange(center, splitlist.Count - center);
            var sah = SAofList(adapter, leftSplit) * leftSplit.Count + SAofList(adapter, rightSplit) * rightSplit.Count;

            return new SplitAxisOpt<T>(sah, axis, leftSplit, rightSplit);
        }




        internal static void AddObject_Pushdown<T>(IBoundingHierarchyAdapter<T> adapter, Node* curNode, T newOb) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var left = curNode->Left;
            var right = curNode->Right;

            // merge and pushdown left and right as a new node..
            var mergedSubnode = CreateNode(adapter.BVH);
            mergedSubnode->Left = left;
            mergedSubnode->Right = right;
            mergedSubnode->Parent = curNode;
            //mergedSubnode.Items = null; // we need to be an interior node... so null out our object list..
            mergedSubnode->BucketIndex = -1;

            left->Parent = mergedSubnode;
            right->Parent = mergedSubnode;
            mergedSubnode->ChildRefit(adapter, false);

            // make new subnode for obj
            var newSubnode = CreateNode(adapter.BVH);
            newSubnode->Parent = curNode;

            if (mergedSubnode->BucketIndex > 0)
            {
                newSubnode->BucketIndex = mergedSubnode->BucketIndex;
                mergedSubnode->BucketIndex = -1;
            }
            else
            {
                var bucketIndex = adapter.BVH.CreateBucket();
                newSubnode->BucketIndex = bucketIndex;
                mergedSubnode->BucketIndex = -1;
            }

            ref var bucket = ref adapter.BVH.GetBucket(newSubnode->BucketIndex);
            bucket.Add(newOb);

            //newSubnode.Items = new List<T> { newOb };
            adapter.MapLeaf(newOb, *newSubnode);
            //newSubnode->ComputeVolume(adapter);
            adapter.BVH.ComputeVolume(newSubnode);

            // make assignments..
            curNode->Left = mergedSubnode;
            curNode->Right = newSubnode;
            curNode->SetDepth(adapter, curNode->Depth); // propagate new depths to our children.
            curNode->ChildRefit(adapter);
        }

        internal int CountNodes()
        {
            if (BucketIndex != -1)
                return 1;
            return Left->CountNodes() + Right->CountNodes();
        }


        public void SetDepth<T>(IBoundingHierarchyAdapter<T> nAda, int newdepth) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            Depth = newdepth;
            if (newdepth > nAda.BVH.maxDepth) nAda.BVH.maxDepth = newdepth;
            if (BucketIndex != -1)
            {
                Left->SetDepth(nAda, newdepth + 1);
                Right->SetDepth(nAda, newdepth + 1);
            }
        }

        internal void FindOverlappingLeaves<T>(ref IBoundingHierarchyAdapter<T> adapter, float3 origin, float radius, List<Node> overlapList) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            if (Box.IntersectsSphere(origin, radius))
            {
                if (IsLeaf)
                {
                    overlapList.Add(this);
                }
                else
                {
                    Left->FindOverlappingLeaves(ref adapter, origin, radius, overlapList);
                    Right->FindOverlappingLeaves(ref adapter, origin, radius, overlapList);
                }
            }
        }

        internal void FindOverlappingLeaves<T>(ref IBoundingHierarchyAdapter<T> adapter, ref BoundingBox aabb, List<Node> overlapList) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            if (Box.IntersectsAABB(aabb))
            {
                if (IsLeaf)
                {
                    overlapList.Add(this);
                }
                else
                {
                    Left->FindOverlappingLeaves(ref adapter, ref aabb, overlapList);
                    Right->FindOverlappingLeaves(ref adapter, ref aabb, overlapList);
                }
            }
        }

        internal BoundingBox ToAabb()
        {
            var aabb = new BoundingBox();
            aabb.Min.x = Box.Min.x;
            aabb.Min.y = Box.Min.y;
            aabb.Min.z = Box.Min.z;
            aabb.Max.x = Box.Max.x;
            aabb.Max.y = Box.Max.y;
            aabb.Max.z = Box.Max.z;
            return aabb;
        }

        internal void ChildExpanded<T>(IBoundingHierarchyAdapter<T> nAda, Node child) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            nAda.BVH.ChildExpanded(this, child);
        }

        internal void ChildRefit<T>(IBoundingHierarchyAdapter<T> nAda, bool propagate = true) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            nAda.BVH.ChildRefit(BaseAddress, propagate);
        }
    }
}