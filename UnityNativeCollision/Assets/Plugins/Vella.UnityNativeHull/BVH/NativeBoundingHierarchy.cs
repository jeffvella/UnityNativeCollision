// Copyright(C) David W. Jeske, 2014, and released to the public domain. 
//
// Dynamic BVH (Bounding Volume Hierarchy) using incremental refit and tree-rotations
//
// initial BVH build based on: Bounding Volume Hierarchies (BVH) – A brief tutorial on what they are and how to implement them
//              http://www.3dmuve.com/3dmblog/?p=182
//
// Dynamic Updates based on: "Fast, Effective BVH Updates for Animated Scenes" (Kopta, Ize, Spjut, Brunvand, David, Kensler)
//              https://github.com/jeske/SimpleScene/blob/master/SimpleScene/Util/ssBVH/docs/BVH_fast_effective_updates_for_animated_scenes.pdf
//
// see also:  Space Partitioning: Octree vs. BVH
//            http://thomasdiewald.com/blog/?p=1488
//
//

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;

//using OpenTK;

// TODO: handle merge/split when LEAF_OBJ_MAX > 1 and objects move
// TODO: add sphere traversal

namespace SimpleScene.Util.ssBVH
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public interface IBoundingHierarchyAdapter<T> where T : struct, IBoundingHierarchyNode, IEquatable<T>
    {
        NativeBoundingHierarchy<T> BVH { get; }
        void SetBvH(NativeBoundingHierarchy<T> bvh);

        float3 Position(T obj);

        float Radius(T obj);

        void MapLeaf(T obj, Node leaf);

        void Unmap(T obj);

        void checkForChanges(T obj);

        Node GetLeaf(T obj);
    }

    public unsafe class NativeBoundingHierarchy<T> : IDisposable where T : struct, IBoundingHierarchyNode, IEquatable<T>
    {
        public delegate bool NodeTest(BoundingBox box);

        public const int MaxBuckets = 10;
        public const int MaxItemsPerBucket = 10;
        public const int MaxNodes = 100;

        public readonly int LEAF_OBJ_MAX;
        public int _isCreated;
        public IBoundingHierarchyAdapter<T> Adapter;

        public NodePositionAxisComparer<T> AxisComprarer;
        public NativeBuffer<NativeBuffer<T>> DataBuckets;

        // All data has been moved here so it can be merged into as few blocks as possible. #todo #yolo
        public NativeHashMap<T, Node> Map;
        public int maxDepth = 0;
        public int nodeCount = 0;
        public NativeBuffer<Node> Nodes;

        public HashSet<Node> refitNodes = new HashSet<Node>();

        public Node* RootNode;

        private NativeBoundingHierarchy() { }

        /// <summary>
        ///     initializes a BVH with a given nodeAdaptor, and object list.
        /// </summary>
        /// <param name="nodeAdaptor"></param>
        /// <param name="objects"></param>
        /// <param name="maxPerLeft">WARNING! currently this must be 1 to use dynamic BVH updates</param>
        public NativeBoundingHierarchy(IBoundingHierarchyAdapter<T> nodeAdaptor, List<T> objects = null, int maxPerLeft = 1)
        {
            LEAF_OBJ_MAX = maxPerLeft;

            Adapter = nodeAdaptor;
            Adapter.SetBvH(this);

            Map = new NativeHashMap<T, Node>(MaxNodes, Allocator.Persistent);
            Nodes = new NativeBuffer<Node>(MaxNodes, Allocator.Persistent);
            DataBuckets = new NativeBuffer<NativeBuffer<T>>(MaxBuckets, Allocator.Persistent);
            RootNode = CreateNode();

            AxisComprarer = new NodePositionAxisComparer<T>(Adapter);

            // todo add objects from input list

            _isCreated = 1;
        }

        internal Node* CreateNode()
        {
            var index = Nodes.Add(new Node());
            var node = Nodes.GetItemPtr<Node>(index);

            // todo: stored pointer needs to be updated if location in Nodes array changes (remove etc)
            node->Ptr = node;
            node->BucketIndex = CreateBucket();
            node->NodeNumber = nodeCount++;

            return node;
        }

        public bool IsCreated => _isCreated == 1;

        public void Dispose()
        {
            if (IsCreated)
            {
                if (DataBuckets.IsCreated)
                {
                    foreach (var item in DataBuckets)
                        if (item.IsCreated)
                        {
                            item.Dispose();
                        }

                    DataBuckets.Dispose();
                }

                if (Nodes.IsCreated)
                {
                    Nodes.Dispose();
                }

                if (Map.IsCreated)
                {
                    Map.Dispose();
                }
            }
        }



        public int CreateBucket()
        {
            return DataBuckets.Add(new NativeBuffer<T>(MaxItemsPerBucket, Allocator.Persistent));
        }

        public ref NativeBuffer<T> GetBucket(int index)
        {
            return ref DataBuckets[index];
        }

        public ref NativeBuffer<T> GetBucket(Node* node)
        {
            return ref DataBuckets[node->BucketIndex];
        }

        public ref NativeBuffer<T> GetBucket(Node node)
        {
            return ref DataBuckets[node.BucketIndex];
        }



        private void _traverse(Node curNode, NodeTest hitTest, List<Node> hitlist)
        {
            if (!curNode.IsValid)
            {
                return;
            }

            if (hitTest(curNode.Box))
            {
                hitlist.Add(curNode);

                if (curNode.Left != null)
                {
                    _traverse(*curNode.Left, hitTest, hitlist);
                }

                if (curNode.Right != null)
                {
                    _traverse(*curNode.Right, hitTest, hitlist);
                }
            }
        }

        private void _traverse(Node curNode, Func<Node, bool> hitTest, List<Node> hitlist)
        {
            if (!curNode.IsValid || hitTest == null)
            {
                return;
            }

            if (hitTest.Invoke(curNode))
            {
                hitlist.Add(curNode);

                if (curNode.Left != null)
                {
                    _traverse(*curNode.Left, hitTest, hitlist);
                }

                if (curNode.Right != null)
                {
                    _traverse(*curNode.Right, hitTest, hitlist);
                }
            }
        }

        public List<Node> Traverse(NodeTest hitTest)
        {
            if (RootNode == null)
            {
                throw new InvalidOperationException("rootnode null pointer");
            }

            var hits = new List<Node>();
            _traverse(*RootNode, hitTest, hits);
            return hits;
        }

        public List<Node> TraverseNode(Func<Node, bool> hitTest)
        {
            if (RootNode == null)
            {
                throw new InvalidOperationException("rootnode null pointer");
            }

            var hits = new List<Node>();
            _traverse(*RootNode, hitTest, hits);
            return hits;
        }

        //// left in for compatibility..
        //public List<ssBVHNode<GO>> traverseRay(SSRay ray)
        //{
        //    float tnear = 0f, tfar = 0f;

        //    return traverse(box => OpenTKHelper.intersectRayAABox1(ray, box, ref tnear, ref tfar));
        //}

        //public List<ssBVHNode<GO>> traverse(SSRay ray)
        //{
        //    float tnear = 0f, tfar = 0f;

        //    return traverse(box => OpenTKHelper.intersectRayAABox1(ray, box, ref tnear, ref tfar));
        //}

        public List<Node> traverse(BoundingBox volume)
        {
            return Traverse(box => box.IntersectsAABB(volume));
        }

        /// <summary>
        ///     Call this to batch-optimize any object-changes notified through
        ///     ssBVHNode.refit_ObjectChanged(..). For example, in a game-loop,
        ///     call this once per frame.
        /// </summary>
        public void Optimize()
        {
            if (LEAF_OBJ_MAX != 1)
            {
                throw new Exception("In order to use optimize, you must set LEAF_OBJ_MAX=1");
            }

            while (refitNodes.Count > 0)
            {
                var maxdepth = refitNodes.Max(n => n.Depth);
                var sweepNodes = refitNodes.Where(n => n.Depth == maxdepth).ToList();
                sweepNodes.ForEach(n => refitNodes.Remove(n));
                sweepNodes.ForEach(n => TryRotate(n.Ptr));
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

        internal static float SAofPair(Node nodea, Node nodeb)
        {
            var box = nodea.Box;
            box.ExpandToFit(nodeb.Box);
            return Sa(ref box);
        }

        internal static float SAofPair(BoundingBox boxa, BoundingBox boxb)
        {
            var pairbox = boxa;
            pairbox.ExpandToFit(boxb);
            return Sa(ref pairbox);
        }

        internal static float SAofList<T>(IBoundingHierarchyAdapter<T> nAda, NativeBuffer<T> items, int startIndex, int itemCount) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            var box = AabBofObj(nAda, items[0]);
            for (var i = startIndex + 1; i < itemCount; i++)
            {
                var newbox = AabBofObj(nAda, items[i]);
                box.ExpandBy(newbox);
            }
            return Sa(box);
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

        //internal Node* CreateNode() 
        //{
        //    var node = CreateNode();
        //    node->BucketIndex = CreateBucket();
        //    node->NodeNumber = nodeCount++;
        //    return node;
        //}

        //internal static Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh, List<T> gobjectlist) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    return CreateNode(bvh, null, gobjectlist, Axis.X, 0);
        //}

        internal Node* CreateNode<T>(NativeBoundingHierarchy<T> bvh, Node* lparent, SplitAxisOpt<T> splitInfo, SplitSide side, int curdepth, int bucketIndex = -1) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            //Debugger.Break();

            var node = bvh.CreateNode();
            var nAda = bvh.Adapter;

            node->NodeNumber = bvh.nodeCount++;
            node->Parent = lparent; // save off the parent BVHGObj Node
            node->Depth = curdepth;

            if (bvh.maxDepth < curdepth) bvh.maxDepth = curdepth;

            // Early out check due to bad data
            // If the list is empty then we have no BVHGObj, or invalid parameters are passed in
            if (splitInfo.Items.Length < 1)
            {
                throw new Exception("ssBVHNode constructed with invalid paramaters");
            }

            var startIndex = side == SplitSide.Left ? splitInfo.LeftStartIndex : splitInfo.RightStartIndex;
            var endIndex = side == SplitSide.Left ? splitInfo.LeftEndIndex : splitInfo.RightEndIndex;
            var itemCount = endIndex - startIndex;

            if (bucketIndex < 0)
            {
                // new bucket
                node->BucketIndex = bvh.CreateBucket();
                ref var bucket = ref bvh.GetBucket(node->BucketIndex);

                for (var i = startIndex; i <= endIndex; i++)
                {
                    bucket.Add(splitInfo.Items[i]);
                    nAda.MapLeaf(splitInfo.Items[i], *node);
                }
            }
            else
            {
                node->BucketIndex = bucketIndex;
                ref var bucket = ref bvh.GetBucket(node->BucketIndex);
                bucket.Clear();

                for (var i = startIndex; i <= endIndex; i++)
                {
                    bucket.Add(splitInfo.Items[i]);
                    nAda.MapLeaf(splitInfo.Items[i], *node);
                }

                //for (var i = 0; i < splitInfo.Count; i++)
                //{
                //    bucket.Add(splitInfo[i]);
                //    nAda.MapLeaf(splitInfo[i], *node);
                //}
            }

            // Check if we’re at our LEAF node, and if so, save the objects and stop recursing.  Also store the min/max for the leaf node and update the parent appropriately
            if (itemCount <= bvh.LEAF_OBJ_MAX)
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
                SplitNode(node);
                ChildRefit(node, false);
            }

            return node;
        }


        /// <summary>
        /// Splits the items within a leaf node by the best axis, and moves them into two new child nodes (left/right).
        /// </summary>
        internal void SplitNode(Node* node)
        {
            ref var bucket = ref GetBucket(node->BucketIndex);
            foreach (ref var item in bucket)
            {
                Adapter.Unmap(item);
            }

            var tmpPtr = stackalloc byte[bucket.CapacityBytes];
            var tmpBuffer = new NativeBuffer<T>(tmpPtr, bucket.Capacity);
            tmpBuffer.CopyFrom(bucket);

            Debug.Assert(tmpBuffer.Length == bucket.Length);

            var center = tmpBuffer.Length / 2;

            SplitAxisOpt<T> splitInfo = new SplitAxisOpt<T>(tmpBuffer, center);

            TryImproveAxisSplit(ref splitInfo, Adapter, Axis.X);
            TryImproveAxisSplit(ref splitInfo, Adapter, Axis.Y);
            TryImproveAxisSplit(ref splitInfo, Adapter, Axis.Z);

            var newLeftIndex = node->BucketIndex;
            var newRightIndex = Adapter.BVH.CreateBucket();

            node->Left = CreateNode(Adapter.BVH, node, splitInfo, SplitSide.Left, node->Depth + 1, newLeftIndex); 
            node->Right = CreateNode(Adapter.BVH, node, splitInfo, SplitSide.Right, node->Depth + 1, newRightIndex); 

            node->BucketIndex = -1;
        }

        /// <summary>
        /// Calculates sah from items grouped left/right along a particular axis.
        /// Updates saves the result as the new best split if the value is lower.
        /// </summary>
        private static void TryImproveAxisSplit(ref SplitAxisOpt<T> split, IBoundingHierarchyAdapter<T> adapter, Axis axis)
        {
            split.Items.Sort(adapter.BVH.AxisComprarer, axis);

            var leftSah = SAofList(adapter, split.Items, split.LeftStartIndex, split.LeftItemCount);
            var rightSah = SAofList(adapter, split.Items, split.RightStartIndex, split.RightItemCount);
            var newSah = leftSah * split.LeftItemCount + rightSah * split.RightItemCount;

            if (split.HasValue == 0 || newSah < split.Sah)
            {
                split.Sah = newSah;
                split.Axis = axis;
                split.HasValue = 1;
            }
        }

        /// <summary>
        ///     tryRotate looks at all candidate rotations, and executes the rotation with the best resulting SAH (if any)
        /// </summary>
        /// <param name="bvh"></param>
        internal void TryRotate(Node* node)
        {
            // if we are not a grandparent, then we can't rotate, so queue our parent and bail out
            if (node->Left->IsLeaf && node->Right->IsLeaf)
                if (node->Parent != null)
                {
                    refitNodes.Add(*node->Parent);
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
                        refitNodes.Add(*node->Parent);
            }
            else
            {
                if (node->Parent != null) refitNodes.Add(*node->Parent);

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
                        ChildRefit(node->Right, false);
                        break;

                    case Rot.LRr:
                        swap = node->Left;
                        node->Left = node->Right->Right;
                        node->Left->Parent = node;
                        node->Right->Right = swap;
                        swap->Parent = node->Right;
                        ChildRefit(node->Right, false);
                        break;

                    case Rot.RLl:
                        swap = node->Right;
                        node->Right = node->Left->Left;
                        node->Right->Parent = node;
                        node->Left->Left = swap;
                        swap->Parent = node->Left;
                        ChildRefit(node->Left, false);
                        break;

                    case Rot.RLr:
                        swap = node->Right;
                        node->Right = node->Left->Right;
                        node->Right->Parent = node;
                        node->Left->Right = swap;
                        swap->Parent = node->Left;
                        ChildRefit(node->Left, false);
                        break;

                    // grandchild to grandchild rotations
                    case Rot.LlRr:
                        swap = node->Left->Left;
                        node->Left->Left = node->Right->Right;
                        node->Right->Right = swap;
                        node->Left->Left->Parent = node->Left;
                        swap->Parent = node->Right;
                        ChildRefit(node->Left, false);
                        ChildRefit(node->Right, false);
                        break;

                    case Rot.LlRl:
                        swap = node->Left->Left;
                        node->Left->Left = node->Right->Left;
                        node->Right->Left = swap;
                        node->Left->Left->Parent = node->Left;
                        swap->Parent = node->Right;
                        ChildRefit(node->Left, false);
                        ChildRefit(node->Right, false);
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
                        SetDepth(node, node->Depth);
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

        //public void CheckForChanges()
        //{
        //    TraverseForChanges(rootBVH);
        //}

        //private void TraverseForChanges(Node curNode)
        //{
        //    // object driven update alternative is to have an event T, which is registered in IBVHNodeAdapter.mapObjectToBVHLeaf(), 
        //    // and when triggered should checkForChanges/ add itself to Refit_ObjectChanged().

        //    if (curNode == null)
        //        return;

        //    //if(curNode.Items?.Count > 0)
        //    //{
        //    //    for (int i = 0; i < curNode.Items.Count; i++)
        //    //    {
        //    //        var item = curNode.Items[0];
        //    //        nAda.checkForChanges(ref item);
        //    //    }
        //    //}


        //    //ref var bucket = ref FindBucket(curNode);
        //    //foreach(var item in bucket)
        //    //{
        //    //    nAda.checkForChanges(item);
        //    //}

        //    //if (curNode.Items?.Count > 0)
        //    //{
        //    //    for (int i = 0; i < curNode.Items.Count; i++)
        //    //    {
        //    //        var item = curNode.Items[0];
        //    //        nAda.checkForChanges(ref item);
        //    //    }
        //    //}

        //    TraverseForChanges(curNode.left);
        //    TraverseForChanges(curNode.right);            
        //}


        public void Add(T newOb)
        {
            var box = BoundingBox.FromSphere(Adapter.Position(newOb), Adapter.Radius(newOb));

            var boxSAH = Sa(ref box);

            AddObjectToNode(RootNode, newOb, ref box, boxSAH);
        }

        public void Remove(T newObj)
        {
            var leaf = Adapter.GetLeaf(newObj);
            RemoveObjectFromNode(leaf, newObj);
        }


        internal void Add(Node node, T newOb, ref BoundingBox newObBox, float newObSAH)
        {
            AddObjectToNode(node.Ptr, newOb, ref newObBox, newObSAH);
        }

        internal void AddObjectToNode(Node* node, T newOb, ref BoundingBox newObBox, float newObSAH)
        {
            // 1. first we traverse the node looking for the best leaf
            while (node->BucketIndex == -1)
            {
                // find the best way to add this object.. 3 options..
                // 1. send to left node  (L+N,R)
                // 2. send to right node (L,R+N)
                // 3. merge and pushdown left-and-right node (L+R,N)

                var left = node->Left;
                var right = node->Right;

                var leftSAH = Sa(left);
                var rightSAH = Sa(right);
                var sendLeftSAH = rightSAH + Sa(left->Box.ExpandedBy(newObBox)); // (L+N,R)
                var sendRightSAH = leftSAH + Sa(right->Box.ExpandedBy(newObBox)); // (L,R+N)
                var mergedLeftAndRightSAH = Sa(AabBofPair(left, right)) + newObSAH; // (L+R,N)

                // Doing a merge-and-pushdown can be expensive, so we only do it if it's notably better
                const float MERGE_DISCOUNT = 0.3f;

                if (mergedLeftAndRightSAH < Math.Min(sendLeftSAH, sendRightSAH) * MERGE_DISCOUNT)
                {
                    AddObject_Pushdown(node, newOb);
                    return;
                }

                if (sendLeftSAH < sendRightSAH)
                {
                    node = left;
                }
                else
                {
                    node = right;
                }
            }

            // 2. then we add the object and map it to our leaf
            //curNode.Items.Add(newOb);

            GetBucket(node).Add(newOb);

            Adapter.MapLeaf(newOb, *node);

            RefitVolume(node);

            SplitIfNecessary(node);
            //node->SplitIfNecessary(Adapter);
        }

        internal bool RefitVolume(Node* node)
        {
            var oldbox = node->Box;
            ComputeVolume(node);

            if (!node->Box.Equals(oldbox))
            {
                if (node->Parent != null)
                {
                    ChildRefit(node->Parent);
                }

                return true;
            }

            return false;
        }

        internal void RemoveObjectFromNode(Node node, T newOb)
        {
            if (node.BucketIndex != -1)
            {
                throw new Exception("removeObject() called on nonLeaf!");
            }

            Adapter.Unmap(newOb);

            //ref var bucket = ref node.Bucket(Adapter);
            ref var bucket = ref GetBucket(node.BucketIndex);
            var idx = bucket.IndexOf(newOb);
            bucket.RemoveAt(idx);

            //Items.Remove(newOb);

            if (!IsEmpty(node.Ptr))
            {
                RefitVolume(node.Ptr);
            }
            else
            {
                // our leaf is empty, so collapse it if we are not the root...
                if (node.Parent != null)
                {
                    node.BucketIndex = -1;
                    //Items = null;
                    RemoveLeaf(node.Parent, node.Ptr);
                    node.Parent = null;
                }
            }
        }


        public void Refit_ObjectChanged(Node node, ref T obj)
        {
            if (!node.IsLeaf)
            {
                throw new Exception("dangling leaf!");
            }

            if (RefitVolume(node.Ptr))
            {
                if (node.Parent != null)
                {
                    refitNodes.Add(*node.Parent);
                }
            }
        }

        public bool IsEmpty(Node* node)
        {
            return !node->IsLeaf || GetBucket(node).Length == 0;
        }

        public int ItemCount(Node* node)
        {
            return node->BucketIndex >= 0 ? GetBucket(node).Length : 0;
        }

        internal void RemoveLeaf(Node* node, Node* removeLeaf)
        {
            if (node->Left == null || node->Right == null)
            {
                throw new Exception("bad intermediate node");
            }

            Node* keepLeaf;

            if (removeLeaf == node->Left)
            {
                keepLeaf = node->Right;
            }
            else if (removeLeaf == node->Right)
            {
                keepLeaf = node->Left;
            }
            else
            {
                throw new Exception("removeLeaf doesn't match any leaf!");
            }

            // "become" the leaf we are keeping.
            node->Box = keepLeaf->Box;
            node->Left = keepLeaf->Left;
            node->Right = keepLeaf->Right;

            //Items = keepLeaf.Items;

            var oldIndex = node->BucketIndex;
            var newIndex = keepLeaf->BucketIndex;

            node->BucketIndex = newIndex;

            

            //ref var keepItems = ref node->Bucket(Adapter);

            if (newIndex != -1)
            {
                node->Left->Parent = node->Ptr;

                // reassign child parents..
                node->Right->Parent = node->Ptr;

                // this reassigns depth for our children
                SetDepth(node, node->Depth);
            }
            else if (newIndex != oldIndex)
            {
                ref var keepItems = ref GetBucket(node->BucketIndex);                                             
                foreach (ref var item in keepItems)
                    Adapter.MapLeaf(item, *node);
            }

            // propagate our new volume..
            if (node->Parent != null)
            {
                ChildRefit(node->Parent);
            }
        }

        internal struct SplitAxisOpt<T> where T : struct // : IComparable<SplitAxisOpt<T>>
        {
            public Axis Axis;
            public NativeBuffer<T> Items;
            public int SplitIndex;
            public float Sah;
            public int HasValue;

            public SplitAxisOpt(NativeBuffer<T> items, int splitIndex)
            {
                Items = items;
                SplitIndex = splitIndex;
                Sah = default;
                Axis = default;
                HasValue = 0;
            }

            public int LeftStartIndex => 0;
            public int LeftEndIndex => SplitIndex - 1;
            public int LeftItemCount => LeftEndIndex;
            public int RightStartIndex => SplitIndex;
            public int RightEndIndex => Items.Length - 1;
            public int RightItemCount => RightEndIndex - RightStartIndex;
        }

        public enum SplitSide
        {
            None = 0,
            Left,
            Right
        }

        internal void AddObject_Pushdown(Node* curNode, T newOb)
        {
            var left = curNode->Left;
            var right = curNode->Right;

            // merge and pushdown left and right as a new node..
            var mergedSubnode = CreateNode();
            mergedSubnode->Left = left;
            mergedSubnode->Right = right;
            mergedSubnode->Parent = curNode;
            //mergedSubnode.Items = null; // we need to be an interior node... so null out our object list..
            mergedSubnode->BucketIndex = -1;

            left->Parent = mergedSubnode;
            right->Parent = mergedSubnode;
            ChildRefit(mergedSubnode, false);

            // make new subnode for obj
            var newSubnode = CreateNode();
            newSubnode->Parent = curNode;

            if (mergedSubnode->BucketIndex > 0)
            {
                newSubnode->BucketIndex = mergedSubnode->BucketIndex;
                mergedSubnode->BucketIndex = -1;
            }
            else
            {
                var bucketIndex = CreateBucket();
                newSubnode->BucketIndex = bucketIndex;
                mergedSubnode->BucketIndex = -1;
            }

            ref var bucket = ref GetBucket(newSubnode->BucketIndex);
            bucket.Add(newOb);

            //newSubnode.Items = new List<T> { newOb };
            Adapter.MapLeaf(newOb, *newSubnode);

            //newSubnode->ComputeVolume(adapter);
            ComputeVolume(newSubnode);

            // make assignments..
            curNode->Left = mergedSubnode;
            curNode->Right = newSubnode;
            SetDepth(curNode, curNode->Depth); // propagate new depths to our children.
            ChildRefit(curNode);
        }

        public int CountNodes()
        {
            return CountNodes(RootNode);
        }

        internal int CountNodes(Node* node)
        {                        
            if (node->BucketIndex != -1) // todo node->IsLeaf
            {
                return 1;
            }
            return CountNodes(node->Left) + CountNodes(node->Right);
        }

        public void SetDepth(Node* node, int newDepth)
        {
            node->Depth = newDepth;
            if (newDepth > maxDepth)
            {
                maxDepth = newDepth;
            }
            if (node->BucketIndex != -1)
            {
                SetDepth(node->Left, newDepth + 1);
                SetDepth(node->Right, newDepth + 1);
            }
        }

        internal void FindOverlappingLeaves(Node* node, float3 origin, float radius, List<Node> overlapList) 
        {
            if (node->Box.IntersectsSphere(origin, radius))
            {
                if (node->IsLeaf)
                {
                    overlapList.Add(*node);
                }
                else
                {
                    FindOverlappingLeaves(node->Left, origin, radius, overlapList);
                    FindOverlappingLeaves(node->Right, origin, radius, overlapList);
                }
            }
        }

        internal void FindOverlappingLeaves(Node* node, BoundingBox otherBox, NativeList<Node> overlapList)
        {
            if (node->Box.IntersectsAABB(otherBox))
            {
                if (node->IsLeaf)
                {
                    overlapList.Add(*node);
                }
                else
                {
                    FindOverlappingLeaves(node->Left, otherBox, overlapList);
                    FindOverlappingLeaves(node->Right, otherBox, overlapList);
                }
            }
        }

        internal BoundingBox ToAabb(Node* node)
        {
            var aabb = new BoundingBox();
            aabb.Min.x = node->Box.Min.x;
            aabb.Min.y = node->Box.Min.y;
            aabb.Min.z = node->Box.Min.z;
            aabb.Max.x = node->Box.Max.x;
            aabb.Max.y = node->Box.Max.y;
            aabb.Max.z = node->Box.Max.z;
            return aabb;
        }

        //internal void ChildExpanded<T>(IBoundingHierarchyAdapter<T> nAda, Node child) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    nAda.BVH.ChildExpanded(this, child);
        //}

        //internal void ChildRefit<T>(IBoundingHierarchyAdapter<T> nAda, bool propagate = true) where T : struct, IBoundingHierarchyNode, IEquatable<T>
        //{
        //    nAda.BVH.ChildRefit(Ptr, propagate);
        //}

        internal void ChildRefit(Node* curNode, bool propagate = true)
        {
            do
            {
                var oldbox = curNode->Box;
                var left = curNode->Left;
                var right = curNode->Right;

                // start with the left box
                var newBox = left->Box;

                // expand any dimension bigger in the right node
                if (right->Box.Min.x < newBox.Min.x)
                {
                    newBox.Min.x = right->Box.Min.x;
                }

                if (right->Box.Min.y < newBox.Min.y)
                {
                    newBox.Min.y = right->Box.Min.y;
                }

                if (right->Box.Min.z < newBox.Min.z)
                {
                    newBox.Min.z = right->Box.Min.z;
                }

                if (right->Box.Max.x > newBox.Max.x)
                {
                    newBox.Max.x = right->Box.Max.x;
                }

                if (right->Box.Max.y > newBox.Max.y)
                {
                    newBox.Max.y = right->Box.Max.y;
                }

                if (right->Box.Max.z > newBox.Max.z)
                {
                    newBox.Max.z = right->Box.Max.z;
                }

                // now set our box to the newly created box
                curNode->Box = newBox;

                // and walk up the tree
                curNode = curNode->Parent;
            } while (propagate && curNode != null);
        }

        internal void SplitIfNecessary(Node* node)
        {
            if (ItemCount(node) > LEAF_OBJ_MAX)
            {
                SplitNode(node);
            }
        }



        internal void ComputeVolume(Node* node)
        {
            ref var bucket = ref GetBucket(node->BucketIndex);

            AssignVolume(node, Adapter.Position(bucket[0]), Adapter.Radius(bucket[0]));

            for (var i = 0; i < bucket.Length; i++)
                ExpandVolume(node, Adapter.Position(bucket[i]), Adapter.Radius(bucket[i]));
        }

        public static void AssignVolume(Node* node, float3 position, float radius)
        {
            node->Box.Min.x = position.x - radius;
            node->Box.Max.x = position.x + radius;
            node->Box.Min.y = position.y - radius;
            node->Box.Max.y = position.y + radius;
            node->Box.Min.z = position.z - radius;
            node->Box.Max.z = position.z + radius;
        }

        private void ExpandVolume(Node* node, float3 objectpos, float radius)
        {
            var expanded = false;

            // test min X and max X against the current bounding volume
            if (objectpos.x - radius < node->Box.Min.x)
            {
                node->Box.Min.x = objectpos.x - radius;
                expanded = true;
            }

            if (objectpos.x + radius > node->Box.Max.x)
            {
                node->Box.Max.x = objectpos.x + radius;
                expanded = true;
            }

            // test min Y and max Y against the current bounding volume
            if (objectpos.y - radius < node->Box.Min.y)
            {
                node->Box.Min.y = objectpos.y - radius;
                expanded = true;
            }

            if (objectpos.y + radius > node->Box.Max.y)
            {
                node->Box.Max.y = objectpos.y + radius;
                expanded = true;
            }

            // test min Z and max Z against the current bounding volume
            if (objectpos.z - radius < node->Box.Min.z)
            {
                node->Box.Min.z = objectpos.z - radius;
                expanded = true;
            }

            if (objectpos.z + radius > node->Box.Max.z)
            {
                node->Box.Max.z = objectpos.z + radius;
                expanded = true;
            }

            if (expanded && node->Parent != null)
            {
                ExpandUpward(node->Parent, node);
            }
        }

        internal void ExpandUpward(Node* node, Node* child)
        {
            var expanded = false;
            if (child->Box.Min.x < node->Box.Min.x)
            {
                node->Box.Min.x = child->Box.Min.x;
                expanded = true;
            }

            if (child->Box.Max.x > node->Box.Max.x)
            {
                node->Box.Max.x = child->Box.Max.x;
                expanded = true;
            }

            if (child->Box.Min.y < node->Box.Min.y)
            {
                node->Box.Min.y = child->Box.Min.y;
                expanded = true;
            }

            if (child->Box.Max.y > node->Box.Max.y)
            {
                node->Box.Max.y = child->Box.Max.y;
                expanded = true;
            }

            if (child->Box.Min.z < node->Box.Min.z)
            {
                node->Box.Min.z = child->Box.Min.z;
                expanded = true;
            }

            if (child->Box.Max.z > node->Box.Max.z)
            {
                node->Box.Max.z = child->Box.Max.z;
                expanded = true;
            }

            if (expanded && node->Parent != null)
            {
                ExpandUpward(node->Parent, node);
            }
        }

        /// <summary>
        /// Sort function that asks T object for a position via an adapter interface then compare them on a particular axis.
        /// </summary>
        public struct NodePositionAxisComparer<T> : IComparer<T, Axis> where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            private readonly IBoundingHierarchyAdapter<T> _adapter;

            public NodePositionAxisComparer(IBoundingHierarchyAdapter<T> adapter)
            {
                _adapter = adapter;
            }

            public int Compare(T a, T b, Axis axis)
            {
                var posA = _adapter.Position(a);
                var posB = _adapter.Position(b);
                switch (axis)
                {
                    case Axis.X: return posA.x < posB.x ? -1 : posA.x > posB.x ? 1 : 0;
                    case Axis.Y: return posA.y < posB.y ? -1 : posA.y > posB.y ? 1 : 0;
                    case Axis.Z: return posA.z < posB.z ? -1 : posA.z > posB.z ? 1 : 0;
                }
                throw new InvalidOperationException(nameof(NodePositionAxisComparer<T>) + " - Unsupported Axis: " + axis);
            }
        }
    }
}