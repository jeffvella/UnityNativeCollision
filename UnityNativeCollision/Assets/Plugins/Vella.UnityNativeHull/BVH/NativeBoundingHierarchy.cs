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

// http://dayabay.ihep.ac.cn/e/muon_simulation/chroma/bvh/

// TODO: pick the best axis to split based on SAH, instead of the biggest
// TODO: Switch SAH comparisons to use (SAH(A) * itemCount(A)) currently it just uses SAH(A)
// TODO: when inserting, compare parent node SAH(A) * itemCount to sum of children, to see if it is better to not split at all
// TODO: implement node merge/split, to handle updates when LEAF_OBJ_MAX > 1
// TODO: handle merge/split when LEAF_OBJ_MAX > 1 and objects move
// TODO: add sphere traversal
// TODO: implement SBVH spacial splits

// Surface Area Heuristic (SAH)
// https://benedikt-bitterli.me/bvh-report.pdf
// https://pbrt.org/
// https://link.springer.com/article/10.1007/BF01911006
// http://www.nvidia.com/docs/IO/77714/sbvh.pdf


// Ray http://psgraphics.blogspot.com/2016/02/new-simple-ray-box-test-from-andrew.html
// http://jcgt.org/published/0007/03/04/
// https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;
using Debug = UnityEngine.Debug;

namespace SimpleScene.Util.ssBVH
{
    [DebuggerDisplay("Node {NodeNumber}: Leaf={IsLeaf} Depth={Depth} Box={Box} HasParent={HasParent}")]
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

        public bool IsLeaf => BucketIndex != -1;

        public bool HasParent => Parent != null;

        public bool IsValid => (IntPtr)Ptr != IntPtr.Zero;

        public bool Equals(Node other)
        {
            return NodeNumber == other.NodeNumber;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is Node other && Equals(other);
        }

        public override int GetHashCode()
        {
            return NodeNumber;
        }
    }

    public unsafe class NativeBoundingHierarchy<T> : IDisposable where T : struct, IBoundingHierarchyNode, IEquatable<T>
    {
        public delegate bool NodeTest(BoundingBox box);

        public const int MaxBuckets = 100;
        public const int MaxItemsPerBucket = 100;
        public const int MaxNodes = 100 ;

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
        /// Storage for all the <typeparamref name="T"/> objects, each group is linked to a leaf node.
        /// </summary>
        public NativeBuffer<NativeBuffer<T>> _buckets;

        /// <summary>
        /// Tree of nodes representing the structure from largest containing box to smallest box.
        /// </summary>
        private NativeBuffer<Node> _nodes;

        /// <summary>
        /// Nodes that need to have their volumes reevaluated.
        /// </summary>
        private HashSet<Node> _refitNodes = new HashSet<Node>();

        private NativeBuffer<int> _unusedBucketIndices;

        //private readonly IndexedRotations _rotations;
        private readonly NodePositionAxisComparer<T> _axisComparer;
 

        private NativeBoundingHierarchy() { }

        public NativeBoundingHierarchy(List<T> objects = null, int maxPerLeft = 1)
        {
            // todo add objects from input list

            // WARNING! currently this must be 1 to use dynamic BVH updates
            _maxLeaves = maxPerLeft;

            //_rotations = new IndexedRotations();
            _map = new NativeHashMap<T, Node>(MaxNodes, Allocator.Persistent);
            _nodes = new NativeBuffer<Node>(MaxNodes, Allocator.Persistent);
            _buckets = new NativeBuffer<NativeBuffer<T>>(MaxBuckets, Allocator.Persistent);
            _unusedBucketIndices = new NativeBuffer<int>(MaxBuckets, Allocator.Persistent);
            _rootNode = CreateNode();
            _axisComparer = new NodePositionAxisComparer<T>();
            _isCreated = 1;
        }

        public void Dispose()
        {
            if (_isCreated != 1)
                 return;
       
            if (_buckets.IsCreated)
            {
                foreach (var item in _buckets)
                {
                    if (item.IsCreated)
                    {
                        item.Dispose();
                    }
                }
                _buckets.Dispose();
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
            // todo: stored pointer needs to be updated if location in Nodes array changes (remove etc)

            if (_buckets.Length + 1 >= MaxItemsPerBucket)
            {
                throw new InvalidOperationException("The maximum number of buckets has been reached");
            }

            var index = _nodes.Add(new Node());
            var node = _nodes.GetItemPtr<Node>(index);
            node->Ptr = node;
            node->BucketIndex = bucketIndex == -1 ? GetOrCreateFreeBucket() : bucketIndex;
            node->NodeNumber = _nodeCount++;
            return node;
        }

        public int GetOrCreateFreeBucket()
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_buckets.Length + 1 >= MaxItemsPerBucket)
            {
                throw new InvalidOperationException("The maximum number of buckets has been reached");
            }
            #endif

            if (_unusedBucketIndices.Length > 0)
            {
                return _unusedBucketIndices.Pop();
            }
            return _buckets.Add(new NativeBuffer<T>(MaxItemsPerBucket, Allocator.Persistent));
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
            return ref _buckets[index];
        }

        public ref NativeBuffer<T> GetBucket(Node* node)
        {
            return ref _buckets[node->BucketIndex];
        }

        public ref NativeBuffer<T> GetBucket(Node node)
        {
            return ref _buckets[node.BucketIndex];
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

        public bool QueueForUpdate(T item)
        {
            if (!TryGetLeaf(item, out Node node))
            {
                return false;
                //throw new Exception($"Item not found {item}!");
            }
            if (!node.IsLeaf)
            {
                throw new Exception("dangling leaf!");
            }
            if (RefitVolume(node.Ptr))
            {
                if (node.Parent != null)
                {
                    _refitNodes.Add(*node.Parent);
                }
            }
            return true;
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
            if (_rootNode == null)
            {
                throw new InvalidOperationException("rootnode null pointer");
            }

            var hits = new List<Node>();
            _traverse(*_rootNode, hitTest, hits);
            return hits;
        }

        public List<Node> TraverseNode(Func<Node, bool> hitTest)
        {
            if (_rootNode == null)
            {
                throw new InvalidOperationException("rootnode null pointer");
            }

            var hits = new List<Node>();
            _traverse(*_rootNode, hitTest, hits);
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
        /// Call this to batch-optimize any object-changes notified through
        /// ssBVHNode.refit_ObjectChanged(..). For example, in a game-loop,
        /// call this once per frame.
        /// </summary>
        public void Optimize()
        {
            if (_maxLeaves != 1)
            {
                throw new Exception("In order to use optimize, you must set LEAF_OBJ_MAX=1");
            }

            while (_refitNodes.Count > 0)
            {
                var maxdepth = _refitNodes.Max(n => n.Depth);
                var sweepNodes = _refitNodes.Where(n => n.Depth == maxdepth).ToList();

                sweepNodes.ForEach(n => _refitNodes.Remove(n));

                sweepNodes.ForEach(n => TryRotate(n.Ptr));
            }
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

        internal Node* CreateNodeFromSplit(Node* parent, SplitAxisOpt<T> splitInfo, SplitSide side, int curdepth, int bucketIndex = -1)
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

            var startIndex = side == SplitSide.Left ? splitInfo.LeftStartIndex : splitInfo.RightStartIndex;
            var endIndex = side == SplitSide.Left ? splitInfo.LeftEndIndex : splitInfo.RightEndIndex;
            var itemCount = endIndex - startIndex + 1;

            ref var bucket = ref GetBucket(newNode->BucketIndex);
            var overwrite = side == SplitSide.Left && itemCount <= bucket.Length;
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
            node->Left = CreateNodeFromSplit(node, splitInfo, SplitSide.Left, node->Depth + 1, node->BucketIndex);
            node->Right = CreateNodeFromSplit(node, splitInfo, SplitSide.Right, node->Depth + 1);
            node->BucketIndex = -1;
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
            // if we are not a grandparent, then we can't rotate, so queue our parent and bail out
            //var isLeaf = node->IsLeaf;

            if (node->IsLeaf || !node->Left->IsLeaf || !node->Right->IsLeaf)
            {
                if (node->Parent != null)
                {
                    //_refitNodes.Add(*node->Parent);
                    TryRotate(node->Parent);
                    return;
                }
            }

            // for each rotation, check that there are grandchildren as necessary (aka not a leaf)
            // then compute total SAH cost of our branches after the rotation.

            var mySa = SurfaceArea(node->Left) + SurfaceArea(node->Right);

            var bestRot = new RotOpt(float.MaxValue, Rot.None);    
            
            //FindBestRotation(node, Rot.None, mySa, ref bestRot);
            FindBestRotation(node, Rot.LRl, mySa, ref bestRot);
            FindBestRotation(node, Rot.LRr, mySa, ref bestRot);
            FindBestRotation(node, Rot.RLl, mySa, ref bestRot);
            FindBestRotation(node, Rot.RLr, mySa, ref bestRot);
            FindBestRotation(node, Rot.LlRl, mySa, ref bestRot);
            FindBestRotation(node, Rot.LlRr, mySa, ref bestRot);

            // perform the best rotation...            
            if (bestRot.Rot == Rot.None)
            {
                Debug.Log($"BVH bestrot {bestRot.Rot} from {mySa} to {bestRot.Sah}");

                // if the best rotation is no-rotation... we check our parents anyhow..                
                if (node->Parent != null)
                {
                    //if (DateTime.Now.Ticks % 100 < 2)
                    //{
                        TryRotate(node->Parent);
                        //_refitNodes.Add(*node->Parent);
                    //}
                }
            }
            else
            {
                if (node->Parent != null)
                {
                    TryRotate(node->Parent);
                    //_refitNodes.Add(*node->Parent);
                    //_refitNodes.Add(*node->Parent);
                }

                var diff = (mySa - bestRot.Sah) / mySa;
                if (diff < 0.1f)
                {
                    Debug.Log($"BVH no benefit ({diff})  {bestRot.Rot} from {mySa} to {bestRot.Sah}");
                    return; // the benefit is not worth the cost
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

        private static void FindBestRotation(Node* node, Rot r, float mySa, ref RotOpt bestRot)
        {
            var rot = RotOpt2(node, r, mySa);
            if (rot.Sah < bestRot.Sah)
            {
                bestRot = rot;
            }
        }

        private static RotOpt RotOpt2(Node* node, Rot rot, float mySa)
        {
            switch (rot)
            {
                case Rot.None: return new RotOpt(mySa, Rot.None);

                // child to grandchild rotations
                case Rot.LRl:
                    return node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(node->Right->Left) + SurfaceArea(AabBofPair(node->Left, node->Right->Right)), rot);

                case Rot.LRr:
                    return node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(node->Right->Right) + SurfaceArea(AabBofPair(node->Left, node->Right->Left)), rot);

                case Rot.RLl:
                    return node->Left->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right, node->Left->Right)) + SurfaceArea(node->Left->Left), rot);

                case Rot.RLr:
                    return node->Left->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right, node->Left->Left)) + SurfaceArea(node->Left->Right), rot);

                // grandchild to grandchild rotations
                case Rot.LlRr:
                    return node->Left->IsLeaf || node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right->Right, node->Left->Right)) + SurfaceArea(AabBofPair(node->Right->Left, node->Left->Left)), rot);

                case Rot.LlRl:
                    return node->Left->IsLeaf || node->Right->IsLeaf 
                        ? new RotOpt(float.MaxValue, Rot.None) 
                        : new RotOpt(SurfaceArea(AabBofPair(node->Right->Left, node->Left->Right)) + SurfaceArea(AabBofPair(node->Left->Left, node->Right->Right)), rot);
               
                default:
                    throw new NotImplementedException("Missing implementation for BVH Rotation SAH Computation .. " + rot);
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
            if (_buckets.Length+1 >= MaxItemsPerBucket)
            {
                throw new InvalidOperationException("The maximum number of buckets has been reached");
            }

            var box = BoundingBox.FromSphere(newOb.Position, newOb.Radius);
            var boxSah = SurfaceArea(ref box);
            AddObjectToNode(_rootNode, newOb, ref box, boxSah);
        }

        public void Remove(T newObj)
        {
            if (!TryGetLeaf(newObj, out Node leaf))
            {
                throw new ArgumentException(nameof(newObj), "Object wasn't found in BVH");

            }
            RemoveObjectFromNode(leaf.Ptr, newObj);
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

                var leftSAH = SurfaceArea(left);
                var rightSAH = SurfaceArea(right);
                var sendLeftSAH = rightSAH + SurfaceArea(left->Box.ExpandedBy(newObBox)); // (L+N,R)
                var sendRightSAH = leftSAH + SurfaceArea(right->Box.ExpandedBy(newObBox)); // (L,R+N)
                var mergedLeftAndRightSAH = SurfaceArea(AabBofPair(left, right)) + newObSAH; // (L+R,N)

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

            MapLeaf(newOb, *node);

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

        internal void RemoveObjectFromNode(Node* node, T newOb)
        {
            if (!node->IsLeaf)
            {
                throw new Exception("removeObject() called on nonLeaf!");
            }

            UnmapLeaf(newOb);

            //ref var bucket = ref node.Bucket(Adapter);
            ref var bucket = ref GetBucket(node->BucketIndex);
            var idx = bucket.IndexOf(newOb);
            bucket.RemoveAt(idx);

            //Items.Remove(newOb);

            if (!IsEmpty(node))
            {
                RefitVolume(node);
            }
            else
            {
                // our leaf is empty, so collapse it if we are not the root...
                if (node->HasParent)
                {
                    FreeBucket(node);

                    //Items = null;
                    RemoveLeaf(node->Parent, node);

                    // todo remove node from _nodes collection??

                    node->Parent = null;
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
                    _refitNodes.Add(*node.Parent);
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

        internal void RemoveLeaf(Node* parent, Node* nodeToRemove)
        {
            // Collapse remove a child from its parent

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (parent->Left == null || parent->Right == null)
            {
                throw new ArgumentException("Bad node encountered: an intermediary (non-leaf) node must have child nodes.");
            }

            if (nodeToRemove->Parent != parent || (parent->Right != nodeToRemove && parent->Left != nodeToRemove))
            {
                throw new ArgumentException("Attempt to remove a leaf with an incorrect parent-child relationship.");
            }
#endif

            var nodeToKeep = nodeToRemove == parent->Left ? parent->Right : parent->Left;

            // The parent and the node to remove will both disappear, by moving the nodeToKeep into the place of its parent.
            
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

            // todo: currently nodes are still there after being orphaned, since they self-reference their pointer
            // ill need to update any other nodes that were moved as part of the remove operation (ie from swap back)
            // or leave the space there and re-use them like buckets

            //_nodes.Remove(nodeToRemove);
            //_nodes.Remove(parent);

            if (nodeToKeep->BucketIndex != -1)
            {
                SetDepth(nodeToKeep, nodeToKeep->Depth - 1);
            }

            //UnmapLeaf();
            //Debug.Assert(!_map.TryGetValue(*parent, out Node result));

            //parent = nodeToKeep;

            //// "become" the leaf we are keeping.
            //parent->Box = nodeToKeep->Box;
            //parent->Left = nodeToKeep->Left;
            //parent->Right = nodeToKeep->Right;

            ////Items = keepLeaf.Items;

            //var oldBucketIndex = parent->BucketIndex;
            //var newBucketIndex = nodeToKeep->BucketIndex;
            //parent->BucketIndex = newBucketIndex;

            ////ref var keepItems = ref node->Bucket(Adapter);

            //if (newBucketIndex != -1)
            //{
            //    parent->Left->Parent = parent->Ptr;

            //    // reassign child parents..
            //    parent->Right->Parent = parent->Ptr;

            //    // this reassigns depth for our children
            //    SetDepth(parent, parent->Depth);
            //}
            //else if (newBucketIndex != oldBucketIndex)
            //{
            //    ref var keepItems = ref GetBucket(parent->BucketIndex);
            //    foreach (ref var item in keepItems)
            //    {
            //        MapLeaf(item, *parent);
            //    }
            //}

            // propagate our new volume..
            if (nodeToKeep->Parent != null)
            {
                ChildRefit(nodeToKeep->Parent);
            }
        }

        internal void AddObject_Pushdown(Node* curNode, T newOb)
        {
           
            var left = curNode->Left;
            var right = curNode->Right;

            // merge and pushdown left and right as a new node..
            var mergedSubnode = CreateNode();
            mergedSubnode->Left = curNode->Left;
            mergedSubnode->Right = curNode->Right;
            mergedSubnode->Parent = curNode;
            //mergedSubnode.Items = null; // we need to be an interior node... so null out our object list..

            //var oldBucket = mergedSubnode->BucketIndex;
            FreeBucket(mergedSubnode);
            //mergedSubnode->BucketIndex = -1;

            left->Parent = mergedSubnode;
            right->Parent = mergedSubnode;
            ChildRefit(mergedSubnode, false);

            // make new subnode for obj
            var newSubnode = CreateNode();
            newSubnode->Parent = curNode;

            //if (mergedSubnode->BucketIndex > 0)
            //{
            //    newSubnode->BucketIndex = mergedSubnode->BucketIndex;
            //    mergedSubnode->BucketIndex = -1;
            //}
            //else
            //{
                //var bucketIndex = GetOrCreateFreeBucket();
                //newSubnode->BucketIndex = bucketIndex;
            //    mergedSubnode->BucketIndex = -1;
            //}

            ref var bucket = ref GetBucket(newSubnode->BucketIndex);
            bucket.Add(newOb);

            //newSubnode.Items = new List<T> { newOb };
            MapLeaf(newOb, *newSubnode);

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
            // When items are added, they're all put into the best matching node's item bucket.
            // if the total items in a node grows larger than our limit, it must be split.

            if (ItemCount(node) > _maxLeaves)
            {
                SplitNode(node);
            }
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
    }

    //public interface IBoundingHierarchyAdapter<T> where T : struct, IBoundingHierarchyNode, IEquatable<T>
    //{
    //   // NativeBoundingHierarchy<T> BVH { get; }

    //    //void SetBvH(NativeBoundingHierarchy<T> bvh);

    //    float3 Position(T obj);

    //    float Radius(T obj);

    //    //void MapLeaf(T obj, Node leaf);

    //    //void Unmap(T obj);

    //    void CheckForChanges(T obj);

    //    //Node GetLeaf(T obj);
    //}

    public interface IReadIndexed<out T>
    {
        T this[int index] { get; }

        int Length { get; }
    }

    //public struct IndexedRotations : IReadIndexed<Rot>
    //{
    //    public const int ItemCount = 7;

    //    private unsafe fixed int _values[ItemCount];

    //    unsafe IndexedRotations()
    //    {
    //        for (int i = 0; i < ItemCount; i++)
    //        {        
    //            _values[i] = i;               
    //        }
    //    }

    //    public unsafe Rot this[int index]
    //    {
    //        get
    //        {
    //            return (Rot)_values[index];
    //        }
    //    }

    //    public int Length => ItemCount;
    //}

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
        LRl,
        LRr,
        RLl,
        RLr,
        LlRr,
        LlRl
    }

    public enum SplitSide
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