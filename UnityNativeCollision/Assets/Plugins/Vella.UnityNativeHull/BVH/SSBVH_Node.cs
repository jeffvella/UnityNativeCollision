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
using System.Linq;
using System.Text;
using System.Diagnostics;
using Unity.Mathematics;
using Vella.Common;
using Unity.Collections.LowLevel.Unsafe;

namespace SimpleScene.Util.ssBVH
{
    //[DebuggerDisplay("Node<{0}>:{1}")]
    //public struct Node2<T> where T : struct, IBVHNode
    //{
    //    public SSAABB box;

    //    public int ItemIndex;

    //    public Node<T> parent;
    //    public Node<T> left;
    //    public Node<T> right;

    //    public int depth;
    //    public int nodeNumber; // for debugging

    //    public int parentIndex;
    //    public int leftIndex;
    //    public int rightIndex;
    //    public int selfIndex;

    //    public ref Node2<T> GetParent(IBVHNodeAdapter<T> nAda) => nAda.FindNode(parentIndex);
    //    public ref Node2<T> GetLeft(IBVHNodeAdapter<T> nAda) => nAda.FindNode(leftIndex);
    //    public ref Node2<T> GetRight(IBVHNodeAdapter<T> nAda) => nAda.FindNode(rightIndex);
    //    public ref Node2<T> GetSelf(IBVHNodeAdapter<T> nAda) => nAda.FindNode(selfIndex);
    //}

    [DebuggerDisplay("Node<{0}>:{1}")]
    public unsafe struct Node // : IEquatable<Node>
    {
        public bool IsValid => (IntPtr)ThisPtr != IntPtr.Zero;

        public Node* ThisPtr; // => (Node*)UnsafeUtility.AddressOf(ref this);

        public SSAABB box;

        public int ItemIndex;

        public Node* left;
        public Node* right;
        public Node* parent;      

        //public Node Dereference(Node* ptr) => *ptr;

        public ref Node AsRef() => ref *ThisPtr;

        public int depth;
        public int nodeNumber; // for debugging

        public int parentIndex;
        public int leftIndex;
        public int rightIndex;
        public int selfIndex;

        //public bool Equals(Node other)
        //{
        //    return Ptr == parent.Ptr;
        //}


        //public ref Node2<T> GetParent(IBVHNodeAdapter<T> nAda) => nAda.FindNode(parentIndex);
        //public ref Node2<T> GetParent(IBVHNodeAdapter<T> nAda) => nAda.FindNode(parentIndex);
        //public ref Node2<T> GetParent(IBVHNodeAdapter<T> nAda) => nAda.FindNode(parentIndex);
        //public ref Node2<T> GetParent(IBVHNodeAdapter<T> nAda) => nAda.FindNode(parentIndex);

        //public List<T> Items;  // only populated in leaf nodes



        //private Axis PickSplitAxis()
        //{
        //    // return the biggest axis
        //    float axis_x = box.Max.x - box.Min.x;
        //    float axis_y = box.Max.y - box.Min.y;
        //    float axis_z = box.Max.z - box.Min.z;
        //    return axis_x > axis_y ? axis_x > axis_z ? Axis.X : Axis.Z : axis_y > axis_z ? Axis.Y : Axis.Z;
        //}

        public bool IsLeaf
        {
            get
            {
                bool isLeaf = ItemIndex != -1; //(Items != null);

                // if we're a leaf, then both left and right should be null..
                if (isLeaf && ((right != null) || (left != null)))
                {
                    throw new Exception("ssBVH Leaf has objects and left/right pointers!");
                }

                return isLeaf;

            }
        }

        private Axis NextAxis(Axis cur)
        {
            switch (cur)
            {
                case Axis.X: return Axis.Y;
                case Axis.Y: return Axis.Z;
                case Axis.Z: return Axis.X;
                default: throw new NotSupportedException();
            }
        }

        public void Refit_ObjectChanged<T>(IBVHNodeAdapter<T> nAda, ref T obj) where T : struct, IBVHNode
        {
            if (!IsLeaf) { throw new Exception("dangling leaf!"); }
            if (refitVolume(nAda))
            {
                // add our parent to the optimize list...
                if (parent != null)
                {
                    nAda.BVH.refitNodes.Add(*parent);

                    // you can force an optimize every time something moves, but it's not very efficient
                    // instead we do this per-frame after a bunch of updates.
                    // nAda.BVH.optimize();                    
                }
            }
        }

        private void ExpandVolume<T>(IBVHNodeAdapter<T> nAda, float3 objectpos, float radius) where T : struct, IBVHNode
        {
            bool expanded = false;

            // test min X and max X against the current bounding volume
            if ((objectpos.x - radius) < box.Min.x)
            {
                box.Min.x = (objectpos.x - radius); expanded = true;
            }
            if ((objectpos.x + radius) > box.Max.x)
            {
                box.Max.x = (objectpos.x + radius); expanded = true;
            }
            // test min Y and max Y against the current bounding volume
            if ((objectpos.y - radius) < box.Min.y)
            {
                box.Min.y = (objectpos.y - radius); expanded = true;
            }
            if ((objectpos.y + radius) > box.Max.y)
            {
                box.Max.y = (objectpos.y + radius); expanded = true;
            }
            // test min Z and max Z against the current bounding volume
            if ((objectpos.z - radius) < box.Min.z)
            {
                box.Min.z = (objectpos.z - radius); expanded = true;
            }
            if ((objectpos.z + radius) > box.Max.z)
            {
                box.Max.z = (objectpos.z + radius); expanded = true;
            }

            if (expanded && parent != null)
            {
                parent->ChildExpanded(nAda, this);
            }
        }

        private void assignVolume(float3 objectpos, float radius)
        {
            box.Min.x = objectpos.x - radius;
            box.Max.x = objectpos.x + radius;
            box.Min.y = objectpos.y - radius;
            box.Max.y = objectpos.y + radius;
            box.Min.z = objectpos.z - radius;
            box.Max.z = objectpos.z + radius;
        }

        public ref NativeBuffer<T> Bucket<T>(IBVHNodeAdapter<T> nAda) where T : struct, IBVHNode
        {
            return ref nAda.BVH.GetBucketRef(ItemIndex);
        }

        internal void computeVolume<T>(IBVHNodeAdapter<T> nAda) where T : struct, IBVHNode
        {
            ref var bucket = ref nAda.BVH.GetBucketRef(ItemIndex);
            assignVolume(nAda.objectpos(bucket[0]), nAda.radius(bucket[0]));
            for (int i = 0; i < bucket.Length; i++)
            {
                ExpandVolume(nAda, nAda.objectpos(bucket[i]), nAda.radius(bucket[i]));
            }

            //assignVolume(nAda.objectpos(Items[0]), nAda.radius(Items[0]));
            //for (int i = 1; i < Items.Count; i++)
            //{
            //    ExpandVolume(nAda, nAda.objectpos(Items[i]), nAda.radius(Items[i]));
            //}
        }

        public bool IsEmpty<T>(IBVHNodeAdapter<T> nAda) where T : struct, IBVHNode
        {
            return !IsLeaf || Bucket(nAda).Length == 0;
        }

        public int ItemCount<T>(IBVHNodeAdapter<T> nAda) where T : struct, IBVHNode
        {
            return ItemIndex >= 0 ? Bucket(nAda).Length : 0;
        }

        internal bool refitVolume<T>(IBVHNodeAdapter<T> nAda) where T : struct, IBVHNode
        {            
            //if (Items.Count == 0) { throw new NotImplementedException(); }  // TODO: fix this... we should never get called in this case...

            SSAABB oldbox = box;

            computeVolume(nAda);
            if (!box.Equals(oldbox))
            {
                if (parent != null) parent->ChildRefit(nAda);
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static float SA(SSAABB box)
        {
            float x_size = box.Max.x - box.Min.x;
            float y_size = box.Max.y - box.Min.y;
            float z_size = box.Max.z - box.Min.z;

            return 2.0f * ((x_size * y_size) + (x_size * z_size) + (y_size * z_size));

        }
        internal static float SA(ref SSAABB box)
        {
            float x_size = box.Max.x - box.Min.x;
            float y_size = box.Max.y - box.Min.y;
            float z_size = box.Max.z - box.Min.z;

            return 2.0f * ((x_size * y_size) + (x_size * z_size) + (y_size * z_size));
        }

        internal static float SA(Node* node)
        {
            float x_size = node->box.Max.x - node->box.Min.x;
            float y_size = node->box.Max.y - node->box.Min.y;
            float z_size = node->box.Max.z - node->box.Min.z;

            return 2.0f * ((x_size * y_size) + (x_size * z_size) + (y_size * z_size));
        }
        internal static float SA<T>(IBVHNodeAdapter<T> nAda, T obj) where T : struct, IBVHNode
        {
            float radius = nAda.radius(obj);

            float size = radius * 2;
            return 6.0f * (size * size);
        }

        internal static SSAABB AABBofPair(Node* nodea, Node* nodeb)
        {
            SSAABB box = nodea->box;
            box.ExpandToFit(nodeb->box);
            return box;
        }

        internal float SAofPair(Node nodea, Node nodeb)
        {
            SSAABB box = nodea.box;
            box.ExpandToFit(nodeb.box);
            return SA(ref box);
        }
        internal float SAofPair(SSAABB boxa, SSAABB boxb)
        {
            SSAABB pairbox = boxa;
            pairbox.ExpandToFit(boxb);
            return SA(ref pairbox);
        }
        internal static SSAABB AABBofOBJ<T>(IBVHNodeAdapter<T> nAda, T obj) where T : struct, IBVHNode
        {
            float radius = nAda.radius(obj);
            SSAABB box;
            box.Min.x = -radius; box.Max.x = radius;
            box.Min.y = -radius; box.Max.y = radius;
            box.Min.z = -radius; box.Max.z = radius;
            return box;
        }

        internal static float SAofList<T>(IBVHNodeAdapter<T> nAda, List<T> list) where T : struct, IBVHNode
        {
            var box = AABBofOBJ(nAda, list[0]);

            list.ToList().GetRange(1, list.Count - 1).ForEach(obj => {
                var newbox = AABBofOBJ(nAda, obj);
                box.ExpandBy(newbox);
            });
            return SA(box);
        }

        // The list of all candidate rotations, from "Fast, Effective BVH Updates for Animated Scenes", Figure 1.
        internal enum Rot
        {
            NONE, L_RL, L_RR, R_LL, R_LR, LL_RR, LL_RL,
        }

        internal class rotOpt : IComparable<rotOpt>
        {  // rotation option
            public float SAH;
            public Rot rot;
            internal rotOpt(float SAH, Rot rot)
            {
                this.SAH = SAH;
                this.rot = rot;
            }
            public int CompareTo(rotOpt other)
            {
                return SAH.CompareTo(other.SAH);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static List<Rot> eachRot
        {
            get
            {
                return new List<Rot>((Rot[])Enum.GetValues(typeof(Rot)));
            }
        }

        /// <summary>
        /// tryRotate looks at all candidate rotations, and executes the rotation with the best resulting SAH (if any)
        /// </summary>
        /// <param name="bvh"></param>
        internal static void tryRotate<T>(Node* node, NativeBoundingHierarchy<T> bvh) where T : struct, IBVHNode
        {
            IBVHNodeAdapter<T> nAda = bvh.adapter;

            // if we are not a grandparent, then we can't rotate, so queue our parent and bail out
            if (node->left->IsLeaf && node->right->IsLeaf)
            {
                if (node->parent != null)
                {
                    bvh.refitNodes.Add(*node->parent);
                    return;
                }
            }

            // for each rotation, check that there are grandchildren as necessary (aka not a leaf)
            // then compute total SAH cost of our branches after the rotation.

            float mySA = SA(node->left) + SA(node->right);

            rotOpt bestRot = eachRot.Min((rot) => 
            {
                switch (rot)
                {
                    case Rot.NONE: return new rotOpt(mySA, Rot.NONE);
                    // child to grandchild rotations
                    case Rot.L_RL:
                        if (node->right->IsLeaf) return new rotOpt(float.MaxValue, Rot.NONE);
                        else return new rotOpt(SA(node->right->left) + SA(AABBofPair(node->left, node->right->right)), rot);
                    case Rot.L_RR:
                        if (node->right->IsLeaf) return new rotOpt(float.MaxValue, Rot.NONE);
                        else return new rotOpt(SA(node->right->right) + SA(AABBofPair(node->left, node->right->left)), rot);
                    case Rot.R_LL:
                        if (node->left->IsLeaf) return new rotOpt(float.MaxValue, Rot.NONE);
                        else return new rotOpt(SA(AABBofPair(node->right, node->left->right)) + SA(node->left->left), rot);
                    case Rot.R_LR:
                        if (node->left->IsLeaf) return new rotOpt(float.MaxValue, Rot.NONE);
                        else return new rotOpt(SA(AABBofPair(node->right, node->left->left)) + SA(node->left->right), rot);
                    // grandchild to grandchild rotations
                    case Rot.LL_RR:
                        if (node->left->IsLeaf || node->right->IsLeaf) return new rotOpt(float.MaxValue, Rot.NONE);
                        else return new rotOpt(SA(AABBofPair(node->right->right, node->left->right)) + SA(AABBofPair(node->right->left, node->left->left)), rot);
                    case Rot.LL_RL:
                        if (node->left->IsLeaf || node->right->IsLeaf) return new rotOpt(float.MaxValue, Rot.NONE);
                        else return new rotOpt(SA(AABBofPair(node->right->left, node->left->right)) + SA(AABBofPair(node->left->left, node->right->right)), rot);
                    // unknown...
                    default: throw new NotImplementedException("missing implementation for BVH Rotation SAH Computation .. " + rot.ToString());
                }
            });

            // perform the best rotation...            
            if (bestRot.rot != Rot.NONE)
            {
                // if the best rotation is no-rotation... we check our parents anyhow..                
                if (node->parent != null)
                {
                    // but only do it some random percentage of the time.
                    if ((DateTime.Now.Ticks % 100) < 2)
                    {
                        bvh.refitNodes.Add(*node->parent);
                    }
                }
            }
            else
            {

                if (node->parent != null) { bvh.refitNodes.Add(*node->parent); }

                if (((mySA - bestRot.SAH) / mySA) < 0.3f)
                {
                    return; // the benefit is not worth the cost
                }
                Console.WriteLine("BVH swap {0} from {1} to {2}", bestRot.rot.ToString(), mySA, bestRot.SAH);

                // in order to swap we need to:
                //  1. swap the node locations
                //  2. update the depth (if child-to-grandchild)
                //  3. update the parent pointers
                //  4. refit the boundary box
                Node* swap = null;
                switch (bestRot.rot)
                {
                    case Rot.NONE: break;
                    // child to grandchild rotations
                    case Rot.L_RL:
                        swap = node->left;
                        node->left = node->right->left;
                        node->left->parent = node;
                        node->right->left = swap;
                        swap->parent = node->right;
                        node->right->ChildRefit(nAda, propagate: false);
                        break;

                    case Rot.L_RR:
                        swap = node->left;
                        node->left = node->right->right;
                        node->left->parent = node;
                        node->right->right = swap;
                        swap->parent = node->right;
                        node->right->ChildRefit(nAda, propagate: false);
                        break;

                    case Rot.R_LL:
                        swap = node->right;
                        node->right = node->left->left;
                        node->right->parent = node;
                        node->left->left = swap;
                        swap->parent = node->left;
                        node->left->ChildRefit(nAda, propagate: false);
                        break;

                    case Rot.R_LR:
                        swap = node->right;
                        node->right = node->left->right;
                        node->right->parent = node;
                        node->left->right = swap;
                        swap->parent = node->left;
                        node->left->ChildRefit(nAda, propagate: false);
                        break;

                    // grandchild to grandchild rotations
                    case Rot.LL_RR:
                        swap = node->left->left;
                        node->left->left = node->right->right;
                        node->right->right = swap;
                        node->left->left->parent = node->left;
                        swap->parent = node->right;
                        node->left->ChildRefit(nAda, propagate: false);
                        node->right->ChildRefit(nAda, propagate: false);
                        break;

                    case Rot.LL_RL:
                        swap = node->left->left;
                        node->left->left = node->right->left;
                        node->right->left = swap;
                        node->left->left->parent = node->left;
                        swap->parent = node->right;
                        node->left->ChildRefit(nAda, propagate: false);
                        node->right->ChildRefit(nAda, propagate: false);
                        break;

                    // unknown...
                    default: throw new NotImplementedException("missing implementation for BVH Rotation .. " + bestRot.rot.ToString());
                }

                // fix the depths if necessary....
                switch (bestRot.rot)
                {
                    case Rot.L_RL:
                    case Rot.L_RR:
                    case Rot.R_LL:
                    case Rot.R_LR:
                        node->SetDepth(nAda, node->depth);
                        break;
                }
            }

        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static List<Axis> eachAxis
        {
            get
            {
                return new List<Axis>((Axis[])Enum.GetValues(typeof(Axis)));
            }
        }

        internal class SplitAxisOpt<T> : IComparable<SplitAxisOpt<T>>
        {  
            // split Axis option
            public float SAH;
            public Axis axis;
            public List<T> left, right;

            internal SplitAxisOpt(float SAH, Axis axis, List<T> left, List<T> right)
            {
                this.SAH = SAH;
                this.axis = axis;
                this.left = left;
                this.right = right;
            }

            public int CompareTo(SplitAxisOpt<T> other)
            {
                return SAH.CompareTo(other.SAH);
            }
        }

        internal void splitNode<T>(IBVHNodeAdapter<T> adapter) where T : struct, IBVHNode
        {
            // second, decide which axis to split on, and sort..
            //List<T> splitlist = Items;
            ref var bucket = ref Bucket(adapter);
            foreach(ref var item in bucket)
            {
                adapter.UnmapObject(item);
            }

            List<T> splitlist = bucket.ToArray().ToList();
            //splitlist.ForEach(o => adapter.UnmapObject(o));


            int center = (int)(splitlist.Count / 2); // find the center object

            SplitAxisOpt<T> bestSplit = eachAxis.Min((axis) => {
                var orderedlist = new List<T>(splitlist);
                switch (axis)
                {
                    case Axis.X:
                        orderedlist.Sort(delegate (T go1, T go2) { return adapter.objectpos(go1).x.CompareTo(adapter.objectpos(go2).x); });
                        break;
                    case Axis.Y:
                        orderedlist.Sort(delegate (T go1, T go2) { return adapter.objectpos(go1).y.CompareTo(adapter.objectpos(go2).y); });
                        break;
                    case Axis.Z:
                        orderedlist.Sort(delegate (T go1, T go2) { return adapter.objectpos(go1).z.CompareTo(adapter.objectpos(go2).z); });
                        break;
                    default:
                        throw new NotImplementedException("unknown split axis: " + axis.ToString());
                }

                var left_s = orderedlist.GetRange(0, center);
                var right_s = orderedlist.GetRange(center, splitlist.Count - center);

                float SAH = SAofList(adapter, left_s) * left_s.Count + SAofList(adapter, right_s) * right_s.Count;

                return new SplitAxisOpt<T>(SAH, axis, left_s, right_s);
            });

            // perform the split
            var newLeftIndex = ItemIndex;
            var newRightIndex = adapter.BVH.CreateBucket();

            this.left = CreateNode<T>(adapter.BVH, ThisPtr, bestSplit.left, bestSplit.axis, depth + 1, newLeftIndex); // Split the Hierarchy to the left
            this.right = CreateNode<T>(adapter.BVH, ThisPtr, bestSplit.right, bestSplit.axis, depth + 1, newRightIndex); // Split the Hierarchy to the right      

            //Items = null;
            ItemIndex = -1;
        }


        internal void splitIfNecessary<T>(IBVHNodeAdapter<T> adapter) where T : struct, IBVHNode
        {
            if (ItemCount(adapter) > adapter.BVH.LEAF_OBJ_MAX)
            {
                splitNode(adapter);
            }
        }

        internal void AddObject<T>(IBVHNodeAdapter<T> nAda, T newOb, ref SSAABB newObBox, float newObSAH) where T : struct, IBVHNode
        {
            AddObject(nAda, ThisPtr, newOb, ref newObBox, newObSAH);
        }

        internal static void AddObject<T>(IBVHNodeAdapter<T> nAda, Node* curNode, T newOb, ref SSAABB newObBox, float newObSAH) where T : struct, IBVHNode
        {
            // 1. first we traverse the node looking for the best leaf
            while (curNode->ItemIndex == -1)
            {
                // find the best way to add this object.. 3 options..
                // 1. send to left node  (L+N,R)
                // 2. send to right node (L,R+N)
                // 3. merge and pushdown left-and-right node (L+R,N)

                var left = curNode->left;
                var right = curNode->right;

                float leftSAH = SA(left);
                float rightSAH = SA(right);
                float sendLeftSAH = rightSAH + SA(left->box.ExpandedBy(newObBox));    // (L+N,R)
                float sendRightSAH = leftSAH + SA(right->box.ExpandedBy(newObBox));   // (L,R+N)
                float mergedLeftAndRightSAH = SA(AABBofPair(left, right)) + newObSAH; // (L+R,N)

                // Doing a merge-and-pushdown can be expensive, so we only do it if it's notably better
                const float MERGE_DISCOUNT = 0.3f;

                if (mergedLeftAndRightSAH < (Math.Min(sendLeftSAH, sendRightSAH)) * MERGE_DISCOUNT)
                {
                    AddObject_Pushdown(nAda, curNode, newOb);
                    return;
                }
                else
                {
                    if (sendLeftSAH < sendRightSAH)
                    {
                        curNode = left;
                    }
                    else
                    {
                        curNode = right;
                    }
                }
            }

            // 2. then we add the object and map it to our leaf
            //curNode.Items.Add(newOb);

            ref var bucket = ref curNode->Bucket(nAda);
            bucket.Add(newOb);

            nAda.mapObjectToBVHLeaf(newOb, *curNode);

            curNode->refitVolume(nAda);
            // split if necessary...
            curNode->splitIfNecessary(nAda);
        }


        internal static void AddObject_Pushdown<T>(IBVHNodeAdapter<T> nAda, Node* curNode, T newOb) where T : struct, IBVHNode
        {
            var left = curNode->left;
            var right = curNode->right;

            // merge and pushdown left and right as a new node..
            var mergedSubnode = Node.CreateNode<T>(nAda.BVH);
            mergedSubnode->left = left;
            mergedSubnode->right = right;
            mergedSubnode->parent = curNode;
            //mergedSubnode.Items = null; // we need to be an interior node... so null out our object list..
            mergedSubnode->ItemIndex = -1;

            left->parent = mergedSubnode;
            right->parent = mergedSubnode;
            mergedSubnode->ChildRefit(nAda, propagate: false);

            // make new subnode for obj
            var newSubnode = Node.CreateNode<T>(nAda.BVH);
            newSubnode->parent = curNode;

            if(mergedSubnode->ItemIndex > 0)
            {
                newSubnode->ItemIndex = mergedSubnode->ItemIndex;
                mergedSubnode->ItemIndex = -1;
            }
            else
            {
                var bucketIndex = nAda.BVH.CreateBucket();                
                newSubnode->ItemIndex = bucketIndex;
                mergedSubnode->ItemIndex = -1;
            }
            ref var bucket = ref nAda.BVH.GetBucketRef(newSubnode->ItemIndex);
            bucket.Add(newOb);

            //newSubnode.Items = new List<T> { newOb };
            nAda.mapObjectToBVHLeaf(newOb, *newSubnode);
            newSubnode->computeVolume(nAda);

            // make assignments..
            curNode->left = mergedSubnode;
            curNode->right = newSubnode;
            curNode->SetDepth(nAda, curNode->depth); // propagate new depths to our children.
            curNode->ChildRefit(nAda);
        }

        internal int CountBVHNodes()
        {
            if (ItemIndex != -1)
            {
                return 1;
            }
            else
            {
                return left->CountBVHNodes() + right->CountBVHNodes();
            }
        }

        internal void RemoveObject<T>(IBVHNodeAdapter<T> adapter, T newOb) where T : struct, IBVHNode
        {
            if (ItemIndex != -1) { throw new Exception("removeObject() called on nonLeaf!"); }

            adapter.UnmapObject(newOb);

            ref var bucket = ref Bucket(adapter);
            var idx = bucket.IndexOf(newOb);
            bucket.RemoveAt(idx);

            //Items.Remove(newOb);

            if (!IsEmpty(adapter))
            {
                refitVolume(adapter);
            }
            else
            {
                // our leaf is empty, so collapse it if we are not the root...
                if (parent != null)
                {
                    ItemIndex = -1;
                    //Items = null;
                    parent->RemoveLeaf(adapter, ThisPtr);
                    parent = null;
                }
            }
        }

        void SetDepth<T>(IBVHNodeAdapter<T> nAda, int newdepth) where T : struct, IBVHNode
        {
            this.depth = newdepth;
            if (newdepth > nAda.BVH.maxDepth)
            {
                nAda.BVH.maxDepth = newdepth;
            }
            if (ItemIndex != -1)
            {
                left->SetDepth(nAda, newdepth + 1);
                right->SetDepth(nAda, newdepth + 1);
            }
        }

        internal void RemoveLeaf<T>(IBVHNodeAdapter<T> nAda, Node* removeLeaf) where T : struct, IBVHNode
        {
            if (left == null || right == null) { throw new Exception("bad intermediate node"); }

            Node* keepLeaf;

            if (removeLeaf == left)
            {
                keepLeaf = right;
            }
            else if (removeLeaf == right)
            {
                keepLeaf = left;
            }
            else
            {
                throw new Exception("removeLeaf doesn't match any leaf!");
            }

            // "become" the leaf we are keeping.
            box = keepLeaf->box;
            left = keepLeaf->left;
            right = keepLeaf->right;

            //Items = keepLeaf.Items;

            ItemIndex = keepLeaf->ItemIndex;
            ref var keepItems = ref Bucket(nAda);

            //foreach(item in keepLeaf.ItemIndex)

            // clear the leaf..
            // keepLeaf.left = null; keepLeaf.right = null; keepLeaf.gobjects = null; keepLeaf.parent = null; 

            if (ItemIndex != -1)
            {
                left->parent = ThisPtr;
                right->parent = ThisPtr;  // reassign child parents..

                this.SetDepth(nAda, this.depth); // this reassigns depth for our children
            }
            else
            {
                // map the objects we adopted to us...                                                
                //Items.ForEach(o => { nAda.mapObjectToBVHLeaf(o, this); });
                foreach(ref var item in keepItems)
                {
                    nAda.mapObjectToBVHLeaf(item, this);
                }
            }

            // propagate our new volume..
            if (parent != null)
            {
                parent->ChildRefit(nAda);
            }
        }

        internal Node rootNode()
        {
            Node cur = this;
            while (cur.parent != null)
            {
                cur = *cur.parent;
            }
            return cur;
        }


        internal void FindOverlappingLeaves<T>(IBVHNodeAdapter<T> nAda, float3 origin, float radius, List<Node> overlapList) where T : struct, IBVHNode
        {
            if (ToAABB().IntersectsSphere(origin, radius))
            {
                if (IsLeaf)
                {
                    overlapList.Add(this);
                }
                else
                {
                    left->FindOverlappingLeaves(nAda, origin, radius, overlapList);
                    right->FindOverlappingLeaves(nAda, origin, radius, overlapList);
                }
            }
        }

        internal void FindOverlappingLeaves<T>(IBVHNodeAdapter<T> nAda, SSAABB aabb, List<Node> overlapList) where T : struct, IBVHNode
        {
            if (ToAABB().IntersectsAABB(aabb))
            {
                if (IsLeaf)
                {
                    overlapList.Add(this);
                }
                else
                {
                    left->FindOverlappingLeaves(nAda, aabb, overlapList);
                    right->FindOverlappingLeaves(nAda, aabb, overlapList);
                }
            }
        }

        internal SSAABB ToAABB()
        {
            SSAABB aabb = new SSAABB();
            aabb.Min.x = box.Min.x;
            aabb.Min.y = box.Min.y;
            aabb.Min.z = box.Min.z;
            aabb.Max.x = box.Max.x;
            aabb.Max.y = box.Max.y;
            aabb.Max.z = box.Max.z;
            return aabb;
        }

        internal void ChildExpanded<T>(IBVHNodeAdapter<T> nAda, Node child) where T : struct, IBVHNode
        {
            bool expanded = false;

            if (child.box.Min.x < box.Min.x)
            {
                box.Min.x = child.box.Min.x; expanded = true;
            }
            if (child.box.Max.x > box.Max.x)
            {
                box.Max.x = child.box.Max.x; expanded = true;
            }
            if (child.box.Min.y < box.Min.y)
            {
                box.Min.y = child.box.Min.y; expanded = true;
            }
            if (child.box.Max.y > box.Max.y)
            {
                box.Max.y = child.box.Max.y; expanded = true;
            }
            if (child.box.Min.z < box.Min.z)
            {
                box.Min.z = child.box.Min.z; expanded = true;
            }
            if (child.box.Max.z > box.Max.z)
            {
                box.Max.z = child.box.Max.z; expanded = true;
            }

            if (expanded && parent != null)
            {
                parent->ChildExpanded(nAda, this);
            }
        }

        internal void ChildRefit<T>(IBVHNodeAdapter<T> nAda, bool propagate = true) where T : struct, IBVHNode
        {
            ChildRefit(nAda, ThisPtr, propagate: propagate);
        }

        internal static void ChildRefit<T>(IBVHNodeAdapter<T> nAda, Node* curNode, bool propagate = true) where T : struct, IBVHNode
        {
            do
            {
                SSAABB oldbox = curNode->box;
                Node* left = curNode->left;
                Node* right = curNode->right;

                // start with the left box
                SSAABB newBox = left->box;

                // expand any dimension bigger in the right node
                if (right->box.Min.x < newBox.Min.x) { newBox.Min.x = right->box.Min.x; }
                if (right->box.Min.y < newBox.Min.y) { newBox.Min.y = right->box.Min.y; }
                if (right->box.Min.z < newBox.Min.z) { newBox.Min.z = right->box.Min.z; }

                if (right->box.Max.x > newBox.Max.x) { newBox.Max.x = right->box.Max.x; }
                if (right->box.Max.y > newBox.Max.y) { newBox.Max.y = right->box.Max.y; }
                if (right->box.Max.z > newBox.Max.z) { newBox.Max.z = right->box.Max.z; }

                // now set our box to the newly created box
                curNode->box = newBox;

                // and walk up the tree
                curNode = curNode->parent;
            } while (propagate && curNode != null);
        }

        //internal Node()
        //{

        //}

        internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh) where T : struct, IBVHNode
            //internal Node(ssBVH<T> bvh)
        {
            //var node = new Node();
            Node* node = bvh.CreateNode();
            node->ItemIndex = bvh.CreateBucket();
            node->nodeNumber = bvh.nodeCount++;

            //ItemIndex =      
            //Items = new List<T>();
            //left = right = null;
            //parent = null;            
            //this.nodeNumber = bvh.nodeCount++;

            return node;
        }

        internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh, List<T> gobjectlist) where T : struct, IBVHNode
        {
            return Node.CreateNode<T>(bvh, null, gobjectlist, Axis.X, 0);
        }
        //internal Node(ssBVH<T> bvh, List<T> gobjectlist) : this(bvh, null, gobjectlist, Axis.X, 0)
        //{

        //}

        internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh, Node* lparent, List<T> gobjectlist, Axis lastSplitAxis, int curdepth, int bucketIndex = -1) where T : struct, IBVHNode
        //private Node(ssBVH<T> bvh, Node<T> lparent, List<T> gobjectlist, Axis lastSplitAxis, int curdepth, int bucketIndex = -1)
        {
            Node* node = bvh.CreateNode();
            IBVHNodeAdapter<T> nAda = bvh.adapter;
            node->nodeNumber = bvh.nodeCount++;
            node->parent = lparent; // save off the parent BVHGObj Node
            node->depth = curdepth;

            if (bvh.maxDepth < curdepth)
            {
                bvh.maxDepth = curdepth;
            }

            // Early out check due to bad data
            // If the list is empty then we have no BVHGObj, or invalid parameters are passed in
            if (gobjectlist == null || gobjectlist.Count < 1)
            {
                throw new Exception("ssBVHNode constructed with invalid paramaters");
            }

                    
            if (bucketIndex < 0)
            {
                // new bucket
                node->ItemIndex = bvh.CreateBucket();
                ref var bucket = ref bvh.GetBucketRef(node->ItemIndex);

                for (int i = 0; i < gobjectlist.Count; i++)
                {                    
                    bucket.Add(gobjectlist[i]);
                    nAda.mapObjectToBVHLeaf(gobjectlist[i], *node);
                }
            }
            else
            {
                node->ItemIndex = bucketIndex;
                ref var bucket = ref bvh.GetBucketRef(node->ItemIndex);
                bucket.Clear();

                for (int i = 0; i < gobjectlist.Count; i++)
                {
                    bucket.Add(gobjectlist[i]);
                    nAda.mapObjectToBVHLeaf(gobjectlist[i], *node);
                }
            }


            // Check if we’re at our LEAF node, and if so, save the objects and stop recursing.  Also store the min/max for the leaf node and update the parent appropriately
            if (gobjectlist.Count <= bvh.LEAF_OBJ_MAX)
            {
                // once we reach the leaf node, we must set prev/next to null to signify the end
                node->left = null;
                node->right = null;
                // at the leaf node we store the remaining objects, so initialize a list
                //Items = gobjectlist;

                //Items.ForEach(o => nAda.mapObjectToBVHLeaf(o, this));

                node->computeVolume(nAda);
                node->splitIfNecessary(nAda);
            }
            else
            {
                // --------------------------------------------------------------------------------------------
                // if we have more than (bvh.LEAF_OBJECT_COUNT) objects, then compute the volume and split
                //Items = gobjectlist;

                node->computeVolume(nAda);
                node->splitNode(nAda);
                node->ChildRefit(nAda, propagate: false);
            }

            return node;
        }


    }
}