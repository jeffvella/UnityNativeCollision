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
            RootNode = Node.CreateNode(this);

            // todo add objects from input list

            _isCreated = 1;
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
                            item.Dispose();
                    DataBuckets.Dispose();
                }

                if (Nodes.IsCreated) Nodes.Dispose();
                if (Map.IsCreated) Map.Dispose();
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

        internal Node* CreateNode()
        {
            var index = Nodes.Add(new Node());
            var node = Nodes.GetItemPtr<Node>(index);

            // todo: stored pointer needs to be updated if location in Nodes array changes (remove etc)
            node->BaseAddress = node;

            return node;
        }

        private void _traverse(Node curNode, NodeTest hitTest, List<Node> hitlist)
        {
            if (!curNode.IsValid) return;

            if (hitTest(curNode.Box))
            {
                hitlist.Add(curNode);

                if (curNode.Left != null) _traverse(*curNode.Left, hitTest, hitlist);
                if (curNode.Right != null) _traverse(*curNode.Right, hitTest, hitlist);
            }
        }

        private void _traverse(Node curNode, Func<Node, bool> hitTest, List<Node> hitlist)
        {
            if (!curNode.IsValid || hitTest == null) return;

            if (hitTest.Invoke(curNode))
            {
                hitlist.Add(curNode);

                if (curNode.Left != null) _traverse(*curNode.Left, hitTest, hitlist);
                if (curNode.Right != null) _traverse(*curNode.Right, hitTest, hitlist);
            }
        }

        public List<Node> Traverse(NodeTest hitTest)
        {
            if (RootNode == null)
                throw new InvalidOperationException("rootnode null pointer");

            var hits = new List<Node>();
            _traverse(*RootNode, hitTest, hits);
            return hits;
        }

        public List<Node> TraverseNode(Func<Node, bool> hitTest)
        {
            if (RootNode == null)
                throw new InvalidOperationException("rootnode null pointer");

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
            if (LEAF_OBJ_MAX != 1) throw new Exception("In order to use optimize, you must set LEAF_OBJ_MAX=1");

            while (refitNodes.Count > 0)
            {
                var maxdepth = refitNodes.Max(n => n.Depth);
                var sweepNodes = refitNodes.Where(n => n.Depth == maxdepth).ToList();
                sweepNodes.ForEach(n => refitNodes.Remove(n));
                sweepNodes.ForEach(n => Node.TryRotate(this, n.BaseAddress));
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

            var boxSAH = Node.Sa(ref box);

            AddObjectToNode(RootNode, newOb, ref box, boxSAH);
        }

        public void Remove(T newObj)
        {
            var leaf = Adapter.GetLeaf(newObj);
            RemoveObjectFromNode(leaf, newObj);
        }

        public int CountBVHNodes()
        {
            return RootNode->CountNodes();
        }

        internal void Add(Node node, T newOb, ref BoundingBox newObBox, float newObSAH)
        {
            AddObjectToNode(node.BaseAddress, newOb, ref newObBox, newObSAH);
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

                var leftSAH = Node.Sa(left);
                var rightSAH = Node.Sa(right);
                var sendLeftSAH = rightSAH + Node.Sa(left->Box.ExpandedBy(newObBox)); // (L+N,R)
                var sendRightSAH = leftSAH + Node.Sa(right->Box.ExpandedBy(newObBox)); // (L,R+N)
                var mergedLeftAndRightSAH = Node.Sa(Node.AabBofPair(left, right)) + newObSAH; // (L+R,N)

                // Doing a merge-and-pushdown can be expensive, so we only do it if it's notably better
                const float MERGE_DISCOUNT = 0.3f;

                if (mergedLeftAndRightSAH < Math.Min(sendLeftSAH, sendRightSAH) * MERGE_DISCOUNT)
                {
                    Node.AddObject_Pushdown(Adapter, node, newOb);
                    return;
                }

                if (sendLeftSAH < sendRightSAH)
                    node = left;
                else
                    node = right;
            }

            // 2. then we add the object and map it to our leaf
            //curNode.Items.Add(newOb);

            GetBucket(node).Add(newOb);

            Adapter.MapLeaf(newOb, *node);

            node->RefitVolume(Adapter);

            SplitIfNecessary(node);
            //node->SplitIfNecessary(Adapter);
        }


        internal void RemoveObjectFromNode(Node node, T newOb)
        {
            if (node.BucketIndex != -1) throw new Exception("removeObject() called on nonLeaf!");

            Adapter.Unmap(newOb);

            ref var bucket = ref node.Bucket(Adapter);
            var idx = bucket.IndexOf(newOb);
            bucket.RemoveAt(idx);

            //Items.Remove(newOb);

            if (!IsEmpty(node.BaseAddress))
            {
                node.RefitVolume(Adapter);
            }
            else
            {
                // our leaf is empty, so collapse it if we are not the root...
                if (node.Parent != null)
                {
                    node.BucketIndex = -1;
                    //Items = null;
                    RemoveLeaf(node.Parent, node.BaseAddress);
                    node.Parent = null;
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
            if (node->Left == null || node->Right == null) throw new Exception("bad intermediate node");

            Node* keepLeaf;

            if (removeLeaf == node->Left)
                keepLeaf = node->Right;
            else if (removeLeaf == node->Right)
                keepLeaf = node->Left;
            else
                throw new Exception("removeLeaf doesn't match any leaf!");

            // "become" the leaf we are keeping.
            node->Box = keepLeaf->Box;
            node->Left = keepLeaf->Left;
            node->Right = keepLeaf->Right;

            //Items = keepLeaf.Items;

            node->BucketIndex = keepLeaf->BucketIndex;
            ref var keepItems = ref node->Bucket(Adapter);

            if (node->BucketIndex != -1)
            {
                node->Left->Parent = node->BaseAddress;

                // reassign child parents..
                node->Right->Parent = node->BaseAddress;

                // this reassigns depth for our children
                node->SetDepth(Adapter, node->Depth); 
            }
            else
            {
                // map the objects we adopted to us...                                                
                foreach (ref var item in keepItems)
                {
                    Adapter.MapLeaf(item, *node);
                }
            }

            // propagate our new volume..
            if (node->Parent != null) node->Parent->ChildRefit(Adapter);
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
                if (right->Box.Min.x < newBox.Min.x) newBox.Min.x = right->Box.Min.x;
                if (right->Box.Min.y < newBox.Min.y) newBox.Min.y = right->Box.Min.y;
                if (right->Box.Min.z < newBox.Min.z) newBox.Min.z = right->Box.Min.z;

                if (right->Box.Max.x > newBox.Max.x) newBox.Max.x = right->Box.Max.x;
                if (right->Box.Max.y > newBox.Max.y) newBox.Max.y = right->Box.Max.y;
                if (right->Box.Max.z > newBox.Max.z) newBox.Max.z = right->Box.Max.z;

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
                node->SplitNode(Adapter);
            }
        }

        internal void ChildExpanded(Node node, Node child)
        {
            var expanded = false;
            if (child.Box.Min.x < node.Box.Min.x)
            {
                node.Box.Min.x = child.Box.Min.x;
                expanded = true;
            }

            if (child.Box.Max.x > node.Box.Max.x)
            {
                node.Box.Max.x = child.Box.Max.x;
                expanded = true;
            }

            if (child.Box.Min.y < node.Box.Min.y)
            {
                node.Box.Min.y = child.Box.Min.y;
                expanded = true;
            }

            if (child.Box.Max.y > node.Box.Max.y)
            {
                node.Box.Max.y = child.Box.Max.y;
                expanded = true;
            }

            if (child.Box.Min.z < node.Box.Min.z)
            {
                node.Box.Min.z = child.Box.Min.z;
                expanded = true;
            }

            if (child.Box.Max.z > node.Box.Max.z)
            {
                node.Box.Max.z = child.Box.Max.z;
                expanded = true;
            }

            if (expanded && node.Parent != null) node.Parent->ChildExpanded(Adapter, node);
        }

        internal void ComputeVolume(Node* node) 
        {
            ref var bucket = ref GetBucket(node->BucketIndex);

            AssignVolume(node, Adapter.Position(bucket[0]), Adapter.Radius(bucket[0]));

            for (var i = 0; i < bucket.Length; i++)
            {
                ExpandVolume(node, Adapter.Position(bucket[i]), Adapter.Radius(bucket[i]));
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
                node->Parent->ChildExpanded(Adapter, *node);
            }
        }
    }
}