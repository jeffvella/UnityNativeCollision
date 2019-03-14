
// BVH for Unity Burst by Jeffrey Vella;
// I dedicate any and all copyright interest in this software to the public domain. I make this dedication 
// for the benefit of the public at large and to the detriment of my heirs and successors. I intend this
// dedication to be an overt act of relinquishment in perpetuity of all present and future rights to 
// this software under copyright law.

// Based on Dynamic BVH (Bounding Volume Hierarchy) using incremental refit and tree-rotations
// Copyright David W. Jeske, 2014 and released to public domain.
// https://github.com/jeske/SimpleScene/tree/master/SimpleScene/Util/ssBVH

// Surface Area Heuristic (SAH):
// https://benedikt-bitterli.me/bvh-report.pdf
// https://pbrt.org/
// https://link.springer.com/article/10.1007/BF01911006
// http://www.nvidia.com/docs/IO/77714/sbvh.pdf

// Rays:
// http://psgraphics.blogspot.com/2016/02/new-simple-ray-box-test-from-andrew.html
// http://jcgt.org/published/0007/03/04/
// https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;
using Debug = UnityEngine.Debug;

namespace Vella.Common
{
    [DebuggerDisplay("Node {NodeNumber}: IsValid={IsValid} Depth={Depth} Leaf={IsLeaf} State{State}")]
    public unsafe struct Node : IEquatable<Node>
    {
        public BoundingBox Box;
        public Node* Ptr;
        public Node* Left;
        public Node* Right;
        public Node* Parent;
        public int Depth;
        public int NodeNumber;
        public int BucketIndex;
        public NodeState State;

        public bool IsLeaf => BucketIndex != -1;

        public bool HasParent => (IntPtr)Parent != IntPtr.Zero;

        public bool IsValid => (IntPtr)Ptr != IntPtr.Zero && (IsValidLeafNode || IsValidBranchNode);

        public bool IsValidBranchNode => !IsLeaf && (IntPtr)Left != IntPtr.Zero && (IntPtr)Right != IntPtr.Zero;

        public bool IsValidLeafNode => IsLeaf && (IntPtr)Left == IntPtr.Zero && (IntPtr)Right == IntPtr.Zero;

        public bool IsValidBranch => IsValid && (IsLeaf || Right->IsValidBranch && Left->IsValidBranch);

        public bool Equals(Node other) => NodeNumber == other.NodeNumber;

        public override bool Equals(object obj) => !ReferenceEquals(null, obj) && (obj is Node other && Equals(other));

        public override int GetHashCode() => NodeNumber;

        [Flags]
        public enum NodeState
        {
            None = 0,
            OptimizationQueued = 1
        }
    }

    public unsafe class NativeBoundingHierarchy<T> : IDisposable where T : struct, IBoundingHierarchyNode, IEquatable<T>
    {
        public delegate bool NodeTest(BoundingBox box);


        // Currently using fixed size collections,
        // todo allow NativeBuffer to grow.
        public const int MaxBuckets = 100;
        public const int MaxItemsPerBucket = 100;
        public const int MaxNodes = 1000 ;

        private Node* _rootNode;
        private int _isCreated;
        private int _maxDepth;
        private int _nodeCount;
        private int _maxLeaves;

        public bool IsCreated => _isCreated == 1;

        /// <summary>
        /// Allows finding a leaf associated with a particular <typeparamref name="T"/> object.
        /// </summary>
        private NativeHashMap<T, Node> _map;

        /// <summary>
        /// Storage for all the <typeparamref name="T"/> objects, each group is linked to a leaf node via <see cref="_map"/>.
        /// </summary>
        public NativeBuffer<NativeBuffer<T>> Buckets;

        /// <summary>
        /// Tree of nodes representing the structure from largest containing box to smallest box.
        /// </summary>
        private NativeBuffer<Node> _nodes;

        private NativeBuffer<int> _unusedBucketIndices;
        private NativeBuffer<int> _unusedNodeIndices;

        private NativeBuffer<Node> _refitQueue;

        private readonly NodePositionAxisComparer<T> _axisComparer;
        private readonly NodeDepthComparer _nodeDepthComparer;

        private NativeBoundingHierarchy() { }

        public NativeBoundingHierarchy(List<T> objects = null, int maxPerLeft = 1)
        {
            // todo add objects from input list, more than 1 per leaf

            // WARNING! currently this must be 1 to use dynamic BVH updates
            _maxLeaves = maxPerLeft;

            _map = new NativeHashMap<T, Node>(MaxNodes, Allocator.Persistent);
            _nodes = new NativeBuffer<Node>(MaxNodes, Allocator.Persistent);
            _unusedBucketIndices = new NativeBuffer<int>(MaxBuckets, Allocator.Persistent);
            _unusedNodeIndices = new NativeBuffer<int>(MaxBuckets, Allocator.Persistent);
            _refitQueue = new NativeBuffer<Node>(MaxNodes, Allocator.Persistent);

            // todo, Buckets to private and expose iterator.
            Buckets = new NativeBuffer<NativeBuffer<T>>(MaxBuckets, Allocator.Persistent);

            _nodeDepthComparer = new NodeDepthComparer();
            _axisComparer = new NodePositionAxisComparer<T>();

            _rootNode = CreateNode();
            _isCreated = 1;

            Debug.Assert(_rootNode->IsValid);
        }

        public void Dispose()
        {
            if (_isCreated != 1)
                 return;
       
            if (Buckets.IsCreated)
            {
                foreach (var item in Buckets)
                {
                    if (item.IsCreated)
                    {
                        item.Dispose();
                    }
                }
                Buckets.Dispose();
            }

            if (_nodes.IsCreated)
            {
                _nodes.Dispose();
            }

            if (_map.IsCreated)
            {
                _map.Dispose();
            }
        }        

        internal Node* CreateNode(int bucketIndex = -1)
        {
            if (Buckets.Length + 1 >= MaxItemsPerBucket)
            {
                throw new InvalidOperationException("The maximum number of buckets has been reached");
            }

            var index = _unusedNodeIndices.Length > 0
                ? _unusedNodeIndices.Pop()
                : _nodes.Add(new Node());

            var node = _nodes.GetItemPtr<Node>(index);
            node->Ptr = node;
            node->BucketIndex = bucketIndex == -1 ? GetOrCreateFreeBucket() : bucketIndex;
            node->NodeNumber = _nodeCount++;
            return node;
        }

        public void FreeNode(Node* node)
        {
            node->Parent = null;
            node->Ptr = null;
            node->Left = null;
            node->Right = null;
            node->BucketIndex = -1;
            node->Depth = 0;

            var index = _nodes.IndexOf(node);

            Debug.Assert((IntPtr)UnsafeUtility.AddressOf(ref _nodes[index]) == (IntPtr)node);

            _unusedNodeIndices.Add(index);         
        }

        public int GetOrCreateFreeBucket()
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Buckets.Length + 1 >= MaxItemsPerBucket)
            {
                throw new InvalidOperationException("The maximum number of buckets has been reached");
            }
            #endif

            if (_unusedBucketIndices.Length > 0)
            {
                return _unusedBucketIndices.Pop();
            }
            return Buckets.Add(new NativeBuffer<T>(MaxItemsPerBucket, Allocator.Persistent));
        }

        public void FreeBucket(Node* node)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_unusedBucketIndices.Contains(node->BucketIndex))
            {
                throw new InvalidOperationException("Attempt to free a bucket that has already been marked as unused.");
            }
#endif
            _unusedBucketIndices.Add(node->BucketIndex);
            node->BucketIndex = -1;            
        }

        public ref NativeBuffer<T> GetBucket(int index)
        {
            return ref Buckets[index];
        }

        public ref NativeBuffer<T> GetBucket(Node* node)
        {
            return ref Buckets[node->BucketIndex];
        }

        public ref NativeBuffer<T> GetBucket(Node node)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (node.BucketIndex < 0 || node.BucketIndex > Buckets.Length - 1)
                throw new IndexOutOfRangeException($"Bucket index {node.BucketIndex} is outside valid range [0-{Buckets.Length - 1}]");
#endif
            return ref Buckets[node.BucketIndex];
        }

        /// <summary>
        /// Remove the mapping for an <paramref name="item"/>
        /// </summary>
        /// <param name="item"></param>
        public void UnmapLeaf(T item)
        {
            _map.Remove(item);
        }

        /// <summary>
        /// Associates a node with an <paramref name="item"/>
        /// </summary>
        public void MapLeaf(T item, Node node)
        {
            // todo: efficient move/replace method for NativeHashMap

            _map.Remove(item);
            _map.TryAdd(item, node);
        }

        /// <summary>
        /// Finds a node associated with an <paramref name="item"/>
        /// </summary>
        public bool TryGetLeaf(T item, out Node node)
        {
            // todo ContainsKey method for map;

            return _map.TryGetValue(item, out node);
        }

        public bool QueueForOptimize(T item)
        {
            if (!TryGetLeaf(item, out Node node))
            {
                return false;
            }
            if (!node.IsLeaf)
            {
                throw new Exception("dangling leaf!");
            }
            if (TryFindBetterNode(node.Ptr, item, out Node* bestNode))
            {
                MoveItemBetweenNodes(node.Ptr, bestNode, item);
            }
            else if (RefitVolume(node.Ptr))
            {
                if (bestNode->Parent != null)
                {
                    _refitQueue.Add(node);
                }                
            }
            return true;
        }

        public List<Node> Traverse(BoundingBox volume)
        {
            return Traverse(box => box.IntersectsAABB(volume));
        }

        public List<Node> Traverse(NodeTest hitTest)
        {
            var hits = new List<Node>();
            Traverse(*_rootNode, hitTest, hits);
            return hits;
        }

        public List<Node> TraverseNodes(Func<Node, bool> hitTest)
        {
            var hits = new List<Node>();
            Traverse(*_rootNode, hitTest, hits);
            return hits;
        }

        private void Traverse(Node curNode, NodeTest hitTest, List<Node> hitlist)
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
                    Traverse(*curNode.Left, hitTest, hitlist);
                }

                if (curNode.Right != null)
                {
                    Traverse(*curNode.Right, hitTest, hitlist);
                }
            }
        }

        private void Traverse(Node curNode, Func<Node, bool> hitTest, List<Node> hitlist)
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
                    Traverse(*curNode.Left, hitTest, hitlist);
                }

                if (curNode.Right != null)
                {
                    Traverse(*curNode.Right, hitTest, hitlist);
                }
            }
        }



        /// <summary>
        /// Call this to batch-optimize any object-changes notified through <see cref="QueueForOptimize"/>     
        /// </summary>
        public void Optimize()
        {
            if (_maxLeaves != 1)
            {
                throw new Exception("In order to use optimize, you must set LEAF_OBJ_MAX=1");
            }

            if (_refitQueue.Length == 0)
                return;
    
            // Traverse upwards looking at every node for a depth before moving to a higher depth,
            // add each nodes' parents without creating duplicates when branches consolidate.

            _refitQueue.SortDescending(_nodeDepthComparer);

            int currentDepthPass = _refitQueue.First.Depth;

            int i = 0;

            while (currentDepthPass > 0)
            {
                for (; i < _refitQueue.Length; i++)
                {
                    Node* node = _refitQueue[i].Ptr;

                    if (i == 0)
                        currentDepthPass = node->Depth;

                    if (node->Depth != currentDepthPass)
                        break;

                    if (!node->IsValid)
                        continue;

                    node->State &= ~Node.NodeState.OptimizationQueued;

                    TryRotate(node);

                    if (!node->HasParent)
                        continue;

                    var isParentQueued = (node->Parent->State & Node.NodeState.OptimizationQueued) != 0;
                    if (isParentQueued)
                        continue;

                    node->Parent->State |= Node.NodeState.OptimizationQueued;

                    _refitQueue.Add(*node->Parent);
                }
                currentDepthPass--;
            }
            _refitQueue.Clear();
        }

        internal static float SurfaceArea(BoundingBox box)
        {
            var xSize = box.Max.x - box.Min.x;
            var ySize = box.Max.y - box.Min.y;
            var zSize = box.Max.z - box.Min.z;
            return 2.0f * (xSize * ySize + xSize * zSize + ySize * zSize);
        }

        internal static float SurfaceArea(ref BoundingBox box)
        {
            var xSize = box.Max.x - box.Min.x;
            var ySize = box.Max.y - box.Min.y;
            var zSize = box.Max.z - box.Min.z;
            return 2.0f * (xSize * ySize + xSize * zSize + ySize * zSize);
        }

        internal static float SurfaceArea(Node* node)
        {
            var xSize = node->Box.Max.x - node->Box.Min.x;
            var ySize = node->Box.Max.y - node->Box.Min.y;
            var zSize = node->Box.Max.z - node->Box.Min.z;
            return 2.0f * (xSize * ySize + xSize * zSize + ySize * zSize);
        }

        internal float SurfaceArea(T obj)
        {
            var radius = obj.Radius;
            var size = radius * 2;
            return 6.0f * (size * size);
        }

        internal static BoundingBox AabBofPair(Node* nodea, Node* nodeb)
        {
            var box = nodea->Box;
            box.ExpandToFit(nodeb->Box);
            return box;
        }

        internal static float SurfaceArea(Node nodea, Node nodeb)
        {
            var box = nodea.Box;
            box.ExpandToFit(nodeb.Box);
            return SurfaceArea(ref box);
        }

        internal static float SurfaceArea(BoundingBox boxa, BoundingBox boxb)
        {
            var pairbox = boxa;
            pairbox.ExpandToFit(boxb);
            return SurfaceArea(ref pairbox);
        }

        internal float SurfaceArea(NativeBuffer<T> items, int startIndex, int itemCount)
        {
            var box = CalculateBox(items[0]);
            for (var i = startIndex + 1; i < itemCount; i++)
            {
                var newbox = CalculateBox(items[i]);
                box.ExpandBy(newbox);
            }
            return SurfaceArea(box);
        }

        internal BoundingBox CalculateBox(T obj)
        {
            var radius = obj.Radius;
            BoundingBox box;
            box.Min.x = -radius;
            box.Max.x = radius;
            box.Min.y = -radius;
            box.Max.y = radius;
            box.Min.z = -radius;
            box.Max.z = radius;
            return box;
        }

        internal Node* CreateNodeFromSplit(Node* parent, SplitAxisOpt<T> splitInfo, NodeSide side, int curdepth, int bucketIndex = -1)
        {

            var newNode = CreateNode(bucketIndex);
            
            newNode->Parent = parent;
            newNode->Depth = curdepth;  

            if (_maxDepth < curdepth)
            {
                _maxDepth = curdepth;
            }

            // Early out check due to bad data
            // If the list is empty then we have no BVHGObj, or invalid parameters are passed in
            if (splitInfo.Items.Length < 1)
            {
                throw new Exception("ssBVHNode constructed with invalid paramaters");
            }

            var startIndex = side == NodeSide.Left ? splitInfo.LeftStartIndex : splitInfo.RightStartIndex;
            var endIndex = side == NodeSide.Left ? splitInfo.LeftEndIndex : splitInfo.RightEndIndex;
            var itemCount = endIndex - startIndex + 1;

            ref var bucket = ref GetBucket(newNode->BucketIndex);
            var overwrite = side == NodeSide.Left && itemCount <= bucket.Length;
            if (overwrite)
            {
                // Overwrite existing data and ignore excess by setting the length
                for (var i = startIndex; i <= endIndex; i++)
                {                                                   
                    bucket[i] = splitInfo.Items[i];
                    MapLeaf(splitInfo.Items[i], *newNode);
                }
                bucket.SetLength(itemCount);
            }
            else
            {
                for (var i = startIndex; i <= endIndex; i++)
                {
                    bucket.Add(splitInfo.Items[i]);
                    MapLeaf(splitInfo.Items[i], *newNode);
                }
            }


            //newNode->BucketIndex = bucketIndex;
            //ref var bucket = ref GetBucket(newNode->BucketIndex);
            //bucket.Clear();
            //for (var i = startIndex; i <= endIndex; i++)
            //{
            //    bucket.Add(splitInfo.Items[i]);
            //    MapLeaf(splitInfo.Items[i], *newNode);
            //}
            //}

            // Check if we’re at our LEAF node, and if so, save the objects and stop recursing.
            // Also store the min/max for the leaf node and update the parent appropriately
            if (itemCount <= _maxLeaves)
            {
                newNode->Left = null;
                newNode->Right = null;
                ComputeVolume(newNode);
                SplitIfNecessary(newNode);
            }
            else
            {
                ComputeVolume(newNode);
                SplitNode(newNode);
                ChildRefit(newNode, false);
            }

            Debug.Assert(newNode->IsValid);
            return newNode;
        }


        /// <summary>
        /// Splits the items within a leaf node by the best axis, and moves them into two new child nodes (left/right).
        /// </summary>
        internal void SplitNode(Node* node)
        {
            // Disassociate the items from their current node because we'll be moving them into new nodes.
            ref var bucket = ref GetBucket(node->BucketIndex);
            foreach (ref var item in bucket)
            {
                UnmapLeaf(item);
            }

            // A copy of the items that will be disposed automatically at the end of the method.
            // This is used for sorting in place to find the best axis to split upon.

            var tmpPtr = stackalloc byte[bucket.CapacityBytes];
            var tmpBuffer = new NativeBuffer<T>(tmpPtr, bucket.Capacity);
            tmpBuffer.CopyFrom(bucket);

            Debug.Assert(tmpBuffer.Length == bucket.Length);

            var center = tmpBuffer.Length / 2;
            var splitInfo = new SplitAxisOpt<T>(tmpBuffer, center);

            TryImproveAxisSplit(ref splitInfo, Axis.X);
            TryImproveAxisSplit(ref splitInfo, Axis.Y);
            TryImproveAxisSplit(ref splitInfo, Axis.Z);

            // Node is no longer a leaf; point left and right to the new child nodes.
            // Left node takes over the existing bucket, Right node creates a new bucket.
            node->Left = CreateNodeFromSplit(node, splitInfo, NodeSide.Left, node->Depth + 1, node->BucketIndex);
            node->Right = CreateNodeFromSplit(node, splitInfo, NodeSide.Right, node->Depth + 1);
            node->BucketIndex = -1;

            var isValidBranch = !node->IsLeaf && node->Left->IsLeaf && node->Left->Left == null && node->Left->Right == null && node->Right->IsLeaf && node->Right->Right == null && node->Right->Left == null;

            Debug.Assert(node->IsValid);

            Debug.Assert(isValidBranch);
            if (!isValidBranch)
            {
                Debugger.Break();                
            }
        }

        /// <summary>
        /// Calculates sah from items grouped left/right along a particular axis.
        /// Updates saves the result as the new best split if the value is lower.
        /// </summary>
        private void TryImproveAxisSplit(ref SplitAxisOpt<T> split, Axis axis)
        {
            split.Items.Sort(_axisComparer, axis);

            var leftSah = SurfaceArea(split.Items, split.LeftStartIndex, split.LeftItemCount);
            var rightSah = SurfaceArea(split.Items, split.RightStartIndex, split.RightItemCount);
            var newSah = leftSah * split.LeftItemCount + rightSah * split.RightItemCount;

            if (split.HasValue == 0 || newSah < split.Sah)
            {
                split.Sah = newSah;
                split.Axis = axis;
                split.HasValue = 1;
            }
        }

        /// <summary>
        /// tryRotate looks at all candidate rotations, and executes the rotation with the best resulting SAH (if any)
        /// </summary>
        /// <param name="bvh"></param>
        internal void TryRotate(Node* node)
        {     
            if (node->IsLeaf && node->Parent != null)
            {
                return;            
            }

            // for each rotation, check that there are grandchildren as necessary (aka not a leaf)
            // then compute total SAH cost of our branches after the rotation.

            var mySa = SurfaceArea(node->Left) + SurfaceArea(node->Right);
            var bestRot = new RotOpt(float.MaxValue, Rot.None);    
            
            //FindBestRotation(node, Rot.None, mySa, ref bestRot);
            FindBestRotation(node, Rot.LeftRightLeft, mySa, ref bestRot);
            FindBestRotation(node, Rot.LeftRightRight, mySa, ref bestRot);
            FindBestRotation(node, Rot.RightLeftLeft, mySa, ref bestRot);
            FindBestRotation(node, Rot.RightLeftRight, mySa, ref bestRot);
            FindBestRotation(node, Rot.LeftLeftRightLeft, mySa, ref bestRot);
            FindBestRotation(node, Rot.LeftLeftRightRight, mySa, ref bestRot);


            // perform the best rotation...            
            if (bestRot.Rot != Rot.None)
            {
                var diff = (mySa - bestRot.Sah) / mySa;
                if (diff <= 0)
                {
                    //Debug.Log($"BVH no benefit ({diff})  {bestRot.Rot} from {mySa} to {bestRot.Sah}");
                    return;
                }

                Debug.LogFormat("BVH swap {0} from {1} to {2}", bestRot.Rot.ToString(), mySa, bestRot.Sah);

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
                    case Rot.LeftRightLeft:
                        swap = node->Left;
                        node->Left = node->Right->Left;
                        node->Left->Parent = node;
                        node->Right->Left = swap;
                        swap->Parent = node->Right;
                        ChildRefit(node->Right, false);
                        break;

                    case Rot.LeftRightRight:
                        swap = node->Left;
                        node->Left = node->Right->Right;
                        node->Left->Parent = node;
                        node->Right->Right = swap;
                        swap->Parent = node->Right;
                        ChildRefit(node->Right, false);
                        break;

                    case Rot.RightLeftLeft:
                        swap = node->Right;
                        node->Right = node->Left->Left;
                        node->Right->Parent = node;
                        node->Left->Left = swap;
                        swap->Parent = node->Left;
                        ChildRefit(node->Left, false);
                        break;

                    case Rot.RightLeftRight:
                        swap = node->Right;
                        node->Right = node->Left->Right;
                        node->Right->Parent = node;
                        node->Left->Right = swap;
                        swap->Parent = node->Left;
                        ChildRefit(node->Left, false);
                        break;

                    // grandchild to grandchild rotations
                    case Rot.LeftLeftRightRight:
                        swap = node->Left->Left;
                        node->Left->Left = node->Right->Right;
                        node->Right->Right = swap;
                        node->Left->Left->Parent = node->Left;
                        swap->Parent = node->Right;
                        ChildRefit(node->Left, false);
                        ChildRefit(node->Right, false);
                        break;

                    case Rot.LeftLeftRightLeft:
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

                switch (bestRot.Rot)
                {
                    case Rot.LeftRightLeft:
                    case Rot.LeftRightRight:
                    case Rot.RightLeftLeft:
                    case Rot.RightLeftRight:
                        SetDepth(node, node->Depth);
                        break;
                }
            }
     
        }

        private static void FindBestRotation(Node* node, Rot r, float mySa, ref RotOpt bestRot)
        {
            var rot = GetRotationSurfaceArea(node, r, mySa);
            if (rot.Sah < bestRot.Sah)
            {
                bestRot = rot;
            }
        }

        private static RotOpt GetRotationSurfaceArea(Node* node, Rot rot, float mySa)
        {
            switch (rot)
            {
                case Rot.None: return new RotOpt(mySa, Rot.None);

                // child to grandchild rotations
                case Rot.LeftRightLeft:
                    return node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(node->Right->Left) + SurfaceArea(AabBofPair(node->Left, node->Right->Right)), rot);

                case Rot.LeftRightRight:
                    return node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(node->Right->Right) + SurfaceArea(AabBofPair(node->Left, node->Right->Left)), rot);

                case Rot.RightLeftLeft:
                    return node->Left->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right, node->Left->Right)) + SurfaceArea(node->Left->Left), rot);

                case Rot.RightLeftRight:
                    return node->Left->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right, node->Left->Left)) + SurfaceArea(node->Left->Right), rot);

                // grandchild to grandchild rotations
                case Rot.LeftLeftRightRight:
                    return node->Left->IsLeaf || node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right->Right, node->Left->Right)) + SurfaceArea(AabBofPair(node->Right->Left, node->Left->Left)), rot);

                case Rot.LeftLeftRightLeft:
                    return node->Left->IsLeaf || node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right->Left, node->Left->Right)) + SurfaceArea(AabBofPair(node->Left->Left, node->Right->Right)), rot);

                default:
                    throw new NotImplementedException("Missing implementation for BVH Rotation SAH Computation .. " + rot);
            }
        }

        public void Add(T newOb)
        {
            if (Buckets.Length+1 >= MaxItemsPerBucket)
            {
                throw new InvalidOperationException("The maximum number of buckets has been reached");
            }

            AddItemToNode(newOb);
        }

        private void AddItemToNode(T newOb)
        {
            var box = BoundingBox.FromSphere(newOb.Position, newOb.Radius);
            var boxSah = SurfaceArea(ref box);
            AddObjectToNode(_rootNode, newOb, ref box, boxSah);
        }

        public void Remove(T newObj)
        {
            if (!TryGetLeaf(newObj, out Node leaf))
            {
                throw new ArgumentException($"{nameof(newObj)} wasn't found in BVH");
            }
            RemoveItemFromNode(leaf.Ptr, newObj);
        }

        internal void Add(Node node, T newOb, ref BoundingBox newObBox, float newObSAH)
        {
            AddObjectToNode(node.Ptr, newOb, ref newObBox, newObSAH);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="item"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool TryFindBetterNode(Node* currentNode, T item, out Node* node)
        {
            // This is a step in addressing one of the issues with the tree, corruption because of moving objects.
            // Things are well formed on creation but if any of the nodes move significantly then they should be grouped with the nodes they're now close to.
            // Flipping as with Optimize() is efficient but not particularly effective on large trees because it doesn't swap nodes from deep in one branch to deep within another branch.

            var box = BoundingBox.FromSphere(item.Position, item.Radius);
            var sah = SurfaceArea(ref box);

            node = _rootNode;

            while (node->BucketIndex == -1)
            {
                if (!node->IsValid || node->Left != null && !node->Left->IsValid || node->Right != null && !node->Right->IsValid)
                {
                    Debug.Log("Invalid Node");
                    Debugger.Break();
                }

                var left = node->Left;
                var right = node->Right;
                var sendLeftSAH = SurfaceArea(right) + SurfaceArea(left->Box.ExpandedBy(box)); // (L+N,R)
                var sendRightSAH = SurfaceArea(left) + SurfaceArea(right->Box.ExpandedBy(box)); // (L,R+N)
                var mergedLeftAndRightSAH = SurfaceArea(AabBofPair(left, right)) + sah; // (L+R,N)

                // Doing a merge-and-pushdown can be expensive, so we only do it if it's notably better
                const float MERGE_DISCOUNT = 0.3f;

                if (mergedLeftAndRightSAH < Math.Min(sendLeftSAH, sendRightSAH) * MERGE_DISCOUNT)
                {
                    break;
                }

                node = sendLeftSAH < sendRightSAH ? left : right;
            }

            if (_rootNode == node || node == currentNode)
                return false;

            if (currentNode->Parent == node && currentNode->IsLeaf)
            {
                // This scenario doesn't work because the source is a leaf and the parent already has two nodes,
                // so moving it up would create a dangling leaf in the vacated spot.
                // todo: might need to allow this where max leaf count > 1 and parent items bucket is not at max capacity.
                /*            
                      x           x          
                     / \         / \            
                    ?   d       ?   s          
                       / \	       / 	     
                      ?   s       ?         
                */
                return false;
            }

            //if (node->IsLeaf)
            //{ 
            //    DebugDrawer.DrawWireCube(node->Box.Center(), node->Box.Size(), Color.blue);
            //}
            //else
            //{
            //    DebugDrawer.DrawWireCube(node->Box.Center(), node->Box.Size(), Color.yellow);
            //}

            return true;
        }

        public void MoveItemBetweenNodes(Node* source, Node* destination, T item)
        {
            RemoveItemFromNode(source, item);
            AddItemToNode(destination, item);
        }

        private void AddItemToNode(Node* destination, T item)
        {
            if (destination->IsLeaf)
            {
                AddItemToLeaf(destination, item);
            }
            else
            {
                AddItemToBranch(destination, item);
            }
        }

        private void AddItemToLeaf(Node* destination, T item)
        {
            GetBucket(destination).Add(item);
            MapLeaf(item, *destination);
            RefitVolume(destination);
            SplitIfNecessary(destination);

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

                var leftSAH = SurfaceArea(left);
                var rightSAH = SurfaceArea(right);

                var sendLeftSAH = rightSAH + SurfaceArea(left->Box.ExpandedBy(newObBox)); // (L+N,R)
                var sendRightSAH = leftSAH + SurfaceArea(right->Box.ExpandedBy(newObBox)); // (L,R+N)
                var mergedLeftAndRightSAH = SurfaceArea(AabBofPair(left, right)) + newObSAH; // (L+R,N)

                // Doing a merge-and-pushdown can be expensive, so we only do it if it's notably better
                const float MERGE_DISCOUNT = 0.3f;

                if (mergedLeftAndRightSAH < Math.Min(sendLeftSAH, sendRightSAH) * MERGE_DISCOUNT)
                {
                    AddItemToBranch(node, newOb);
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
            MapLeaf(newOb, *node);
            RefitVolume(node);
            SplitIfNecessary(node);
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

        internal void RemoveItemFromNode(Node* node, T newOb)
        {
            

            if (!node->IsLeaf)
            {
                throw new Exception("removeObject() called on nonLeaf!");
            }
            if (!node->HasParent)
            {
                throw new Exception("Attempt to collapse a node to a parent when it has no parent");
            }

            UnmapLeaf(newOb);

            ref var bucket = ref GetBucket(node->BucketIndex);
            var idx = bucket.IndexOf(newOb);
            bucket.RemoveAt(idx);

            if (!IsEmpty(node))
            {
                RefitVolume(node);
            }
            else
            {
                // our leaf is empty, so collapse it if we are not the root...
                if (node->HasParent)
                {         
                    // Note this is destructive to both the node and node-parent, so any following logic can't use either.
                    RemoveNode(node);
                        
                    // todo remove node from _nodes collection??        
                }
                else
                {
                    Debug.Log("Attempt to collapse node with no parent");
                }
            }

        }

        public Node* GetSibling(Node* node)
        {
            return node->Parent->Left == node ? node->Parent->Right : node->Parent->Left;
        }

        public bool IsEmpty(Node* node)
        {
            return !node->IsLeaf || GetBucket(node).Length == 0;
        }

        public int ItemCount(Node* node)
        {
            return node->BucketIndex >= 0 ? GetBucket(node).Length : 0;
        }

        internal Node* RemoveNode(Node* nodeToRemove)
        {
            // Remove a child from its parent

            var parent = nodeToRemove->Parent;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (parent->Left == null || parent->Right == null)
            {
                throw new ArgumentException("Invalid Node: A branch node must have two child nodes.");
            }

            if (nodeToRemove->Parent != parent || (parent->Right != nodeToRemove && parent->Left != nodeToRemove))
            {
                throw new ArgumentException("Attempt to remove a leaf with an incorrect parent-child relationship.");
            }
#endif
            var nodeToKeep = GetSibling(nodeToRemove); //nodeToRemove == parent->Left ? parent->Right : parent->Left;

            // The parent and the node to remove will both disappear, by moving the nodeToKeep into the place of its parent.


            Debug.Log($"Removing Node {nodeToRemove->NodeNumber} (Leaf:{nodeToRemove->IsLeaf} Valid:{nodeToRemove->IsValid}) replacing its parent: {parent->NodeNumber} (Leaf:{parent->IsLeaf}, Valid:{parent->IsValid} Grandparent={(parent->Parent != null ? parent->Parent->NodeNumber.ToString() : "Null")}) with it's other child: {nodeToKeep->NodeNumber} (Leaf:{nodeToKeep->IsLeaf}, Valid:{nodeToKeep->IsValid})");


            var grandparent = parent->Parent;
            if (grandparent == null)
            {
                if (!nodeToKeep->IsLeaf)
                {
                    _rootNode = nodeToKeep;
                }
            }
            else
            {
                nodeToKeep->Parent = parent->Parent;

                if (parent->Parent->Left == parent)
                {
                    parent->Parent->Left = nodeToKeep;
                }
                else if (parent->Parent->Right == parent)
                {
                    parent->Parent->Right = nodeToKeep;
                }
                else
                {
                    throw new ArgumentException("Attempt to remove a leaf with a parent that has an incorrect parent-child relationship.");
                }
            }


            FreeBucket(nodeToRemove);
            FreeNode(nodeToRemove);
            FreeNode(parent);

            // todo: currently nodes are still there after being orphaned, since they self-reference their pointer
            // ill need to update any other nodes that were moved as part of the remove operation (ie from swap back)
            // or leave the space there and re-use them like buckets

            //_nodes.Remove(nodeToRemove);
            //_nodes.Remove(parent);

            if (nodeToKeep->BucketIndex != -1)
            {
                SetDepth(nodeToKeep, nodeToKeep->Depth - 1);
            }

            if (nodeToKeep->Parent != null)
            {
                ChildRefit(nodeToKeep->Parent);
            }
            return nodeToKeep->Parent;
        }

        internal void AddItemToBranch(Node* destination, T newObj)
        {
            /*            
            Item inserted into a branch node: 
            * The current children (l & r) become children of a new node (m).
            * This frees up a side to attach 'newObj' to a new node (+)
		            
		            Parent    P          
		            |         |           
		            Dest      D          
	               / \	     / \	     
                  l   r     +   m       
	                           / \       
			                  l   r                
            */

            var left = destination->Left;
            var right = destination->Right;

            // merge and pushdown left and right as a new node..
            var mergedSubnode = CreateNode();
            mergedSubnode->Left = destination->Left;
            mergedSubnode->Right = destination->Right;
            mergedSubnode->Parent = destination;
            FreeBucket(mergedSubnode);

            left->Parent = mergedSubnode;
            right->Parent = mergedSubnode;
            ChildRefit(mergedSubnode, false);

            // make new subnode for obj
            var newSubnode = CreateNode();
            newSubnode->Parent = destination;

            ref var bucket = ref GetBucket(newSubnode->BucketIndex);
            bucket.Add(newObj);

            MapLeaf(newObj, *newSubnode);
            ComputeVolume(newSubnode);

            destination->Left = mergedSubnode;
            destination->Right = newSubnode;

            SetDepth(destination, destination->Depth); 
            ChildRefit(destination);
        }

        public int CountNodes()
        {
            return CountNodes(_rootNode);
        }

        internal int CountNodes(Node* node)
        {
            if (node->IsLeaf)
            {
                return 1;
            }
            return CountNodes(node->Left) + CountNodes(node->Right);
        }

        public void SetDepth(Node* node, int newDepth)
        {
           
            node->Depth = newDepth;
            if (newDepth > _maxDepth)
            {
                _maxDepth = newDepth;
            }

            if (!node->IsLeaf)
            {
                if (!node->IsValidBranch)
                {
                    Debug.Log($"Bad Branch: Node {node->NodeNumber} (Parent={(node->Parent != null ? node->Parent->NodeNumber.ToString() : "Null")})> ({node->Left->NodeNumber} {(node->Left->IsValid?"Invalid":"Valid")}, {node->Right->NodeNumber} {(node->Right->IsValid ? "Invalid" : "Valid")})");

                    if (!node->IsValid)
                        PrintDebugInvalidReason(node);
                    if (!node->Right->IsValid)
                        PrintDebugInvalidReason(node->Right);
                    if (!node->Left->IsValid)
                        PrintDebugInvalidReason(node->Left);
                }
                SetDepth(node->Left, newDepth + 1);
                SetDepth(node->Right, newDepth + 1);
            }
        }

        public void PrintDebugInvalidReason(Node* node)
        {
            if(node->BucketIndex == -1)
            {
                if (node->Left == null)
                    Debug.LogError($"Node {node->NodeNumber} left is null pointer");
                else if (node->Left == null)
                    Debug.LogError($"Node {node->NodeNumber} right is null pointer");
            }
            else
            {
                if (node->Left != null)
                    Debug.LogError($"Node {node->NodeNumber} is leaf but has left child pointer");
                if (node->Left != null)
                    Debug.LogError($"Node {node->NodeNumber} is leaf but has left child pointer");
            }
            if (node != _rootNode && node->Parent == null)
            {
                Debug.LogError($"Node {node->NodeNumber} has no parent");
            }
            if (node->Parent->Left != node && node->Parent->Right != node)
            {
                Debug.LogError($"Node {node->NodeNumber}'s parent doesn't point to it as a left or right child");
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

        /// <summary>
        /// Splits a node that contains too many items (more than beyond <see cref="MaxItemsPerBucket"/>);
        /// </summary>
        /// <param name="node">a node to be split</param>
        internal void SplitIfNecessary(Node* node)
        {
            // When items are added, they're all put into the best matching node's item bucket.
            // if the total items in a node grows larger than our limit, it must be split.

            if (ItemCount(node) > _maxLeaves)
            {
                SplitNode(node);
            }
            Debug.Assert(node->IsValid);
        }

        internal void ComputeVolume(Node* node)
        {
            ref var bucket = ref GetBucket(node->BucketIndex);

            AssignVolume(node, bucket[0].Position, bucket[0].Radius);

            for (var i = 0; i < bucket.Length; i++)
            {
                ExpandVolume(node,bucket[i].Position, bucket[i].Radius);
            }
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

        private void ExpandVolume(Node* node, float3 position, float radius)
        {
            var expanded = false;

            // test min X and max X against the current bounding volume
            if (position.x - radius < node->Box.Min.x)
            {
                node->Box.Min.x = position.x - radius;
                expanded = true;
            }

            if (position.x + radius > node->Box.Max.x)
            {
                node->Box.Max.x = position.x + radius;
                expanded = true;
            }

            // test min Y and max Y against the current bounding volume
            if (position.y - radius < node->Box.Min.y)
            {
                node->Box.Min.y = position.y - radius;
                expanded = true;
            }

            if (position.y + radius > node->Box.Max.y)
            {
                node->Box.Max.y = position.y + radius;
                expanded = true;
            }

            // test min Z and max Z against the current bounding volume
            if (position.z - radius < node->Box.Min.z)
            {
                node->Box.Min.z = position.z - radius;
                expanded = true;
            }

            if (position.z + radius > node->Box.Max.z)
            {
                node->Box.Max.z = position.z + radius;
                expanded = true;
            }

            if (expanded && node->Parent != null)
            {
                ExpandParentVolume(node->Parent, node);
            }
        }

        internal void ExpandParentVolume(Node* node, Node* child)
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
                ExpandParentVolume(node->Parent, node);
            }
        }

        /// <summary>
        /// Sort function that asks T object for a position and compares on a particular axis.
        /// </summary>
        public struct NodePositionAxisComparer<T> : IComparer<T, Axis> where T : struct, IBoundingHierarchyNode, IEquatable<T>
        {
            public int Compare(T a, T b, Axis axis)
            {
                var posA = a.Position;
                var posB = b.Position;
                switch (axis)
                {
                    case Axis.X: return posA.x < posB.x ? -1 : posA.x > posB.x ? 1 : 0;
                    case Axis.Y: return posA.y < posB.y ? -1 : posA.y > posB.y ? 1 : 0;
                    case Axis.Z: return posA.z < posB.z ? -1 : posA.z > posB.z ? 1 : 0;
                }
                throw new InvalidOperationException(nameof(NodePositionAxisComparer<T>) + " - Unsupported Axis: " + axis);
            }
        }

        public struct NodeDepthComparer : IComparer<Node>
        {
            public int Compare(Node a, Node b) => StructComparer<int>.Default.Compare(a.Depth, b.Depth);
        }
    }

    public enum Axis
    {
        None = 0,
        X,
        Y,
        Z
    }

    public enum Rot
    {
        None = 0,
        LeftRightLeft,
        LeftRightRight,
        RightLeftLeft,
        RightLeftRight,
        LeftLeftRightRight,
        LeftLeftRightLeft,
    }

    public enum NodeSide
    {
        None = 0,
        Left,
        Right
    }

    internal struct RotOpt
    {
        public Rot Rot;
        public float Sah;

        internal RotOpt(float sah, Rot rot)
        {
            Sah = sah;
            Rot = rot;
        }
    }

    public interface IBoundingHierarchyNode
    {
        float3 Position { get; }
        float Radius { get; }
        bool HasChanged { get; }
    }

    internal struct SplitAxisOpt<T> where T : struct
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
}