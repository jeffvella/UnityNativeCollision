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

using JacksonDunstan.NativeCollections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        X, Y, Z,
    }

    public interface IBVHNodeAdapter<T> where T : struct, IBVHNode, IEquatable<T>
    {
        void SetBvH(NativeBoundingHierarchy<T> bvh);

        NativeBoundingHierarchy<T> BVH { get; }

        //bool IsCreated { get; }

        //void setBVH(NativeBoundingHierarchy<T> bvh);

        float3 objectpos(T obj);

        float radius(T obj);

        void mapObjectToBVHLeaf(T obj, Node leaf);

        void UnmapObject(T obj);

        void checkMap(T obj);

        void checkForChanges(T obj);

        Node getLeaf(T obj);
    }

    public unsafe class NativeBoundingHierarchy<T> : IDisposable where T : struct, IBVHNode, IEquatable<T>
    {
        public const int MaxBuckets = 10;
        public const int MaxItemsPerBucket = 10;
        public const int MaxNodes = 100;

        public int _isCreated;
        public bool IsCreated => _isCreated == 1;

        public Node* rootBVH;

        //public Node* rootBVHPtr;

        public IBVHNodeAdapter<T> adapter;

        //public NativeList<Node<T>> items = new NativeList<Node<T>>();

        // Heap array of pointers to other scattered heap arrays.
        public NativeBuffer<NativeBuffer<T>> dataBuckets;

        public NativeBuffer<Node> Nodes;

        public NativeHashMap<T, Node> _map;

        //public NativeBuffer<Node2<T>> nodes;

        public readonly int LEAF_OBJ_MAX;

        public int nodeCount = 0;
        public int maxDepth = 0;

        public ref NativeBuffer<T> GetBucketRef(int index)
        {
            return ref dataBuckets[index];
        }

        public ref NativeBuffer<T> GetBucketRef(Node node)
        {
            return ref dataBuckets[node.ItemIndex];
        }

        public int CreateBucket()
        {
            return dataBuckets.Add(new NativeBuffer<T>(MaxItemsPerBucket, Allocator.Persistent));
        }

        public Node* CreateNode()
        {
            var index = Nodes.Add(new Node());
            var node = Nodes.GetItemPtr<Node>(index);           
            node->ThisPtr = node;
            return node;
        }

        public HashSet<Node> refitNodes = new HashSet<Node>();

        public delegate bool NodeTest(SSAABB box);

        // internal functional traversal...
        private void _traverse(Node curNode, NodeTest hitTest, List<Node> hitlist)
        {
            if (!curNode.IsValid) // not sure about this traverse end.
            {
                return;
            }

            if (hitTest(curNode.box))
            {
                hitlist.Add(curNode);

                if (curNode.left != null)
                {
                    _traverse(*curNode.left, hitTest, hitlist);
                }
                if (curNode.right != null)
                {
                    _traverse(*curNode.right, hitTest, hitlist);
                }
            }
        }

        private void _traverse(Node curNode, Func<Node,bool> hitTest, List<Node> hitlist)
        {
            if (!curNode.IsValid || hitTest == null)
            {
                return;
            }

            if (hitTest.Invoke(curNode))
            {
                hitlist.Add(curNode);

                if (curNode.left != null)
                {
                    _traverse(*curNode.left, hitTest, hitlist);
                }
                if (curNode.right != null)
                {
                    _traverse(*curNode.right, hitTest, hitlist);
                }
            }
        }

        // public interface to traversal..
        public List<Node> Traverse(NodeTest hitTest)
        {
            if (rootBVH == null)
                throw new InvalidOperationException("rootnode null pointer");

            var hits = new List<Node>();
            this._traverse(*rootBVH, hitTest, hits);
            return hits;
        }


        public List<Node> TraverseNode(Func<Node, bool> hitTest)
        {
            if (rootBVH == null)
                throw new InvalidOperationException("rootnode null pointer");

            var hits = new List<Node>();
            this._traverse(*rootBVH, hitTest, hits);
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

        public List<Node> traverse(SSAABB volume)
        {
            return Traverse(box => box.IntersectsAABB(volume));
        }

        /// <summary>
        /// Call this to batch-optimize any object-changes notified through 
        /// ssBVHNode.refit_ObjectChanged(..). For example, in a game-loop, 
        /// call this once per frame.
        /// </summary>

        public void optimize()
        {
            if (LEAF_OBJ_MAX != 1)
            {
                throw new Exception("In order to use optimize, you must set LEAF_OBJ_MAX=1");
            }

            while (refitNodes.Count > 0)
            {
                int maxdepth = refitNodes.Max(n => n.depth);
                var sweepNodes = refitNodes.Where(n => n.depth == maxdepth).ToList();
                sweepNodes.ForEach(n => refitNodes.Remove(n));
                sweepNodes.ForEach(n => Node.tryRotate(n.ThisPtr, this));
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


        public void addObject(T newOb)
        {
            SSAABB box = SSAABB.FromSphere(adapter.objectpos(newOb), adapter.radius(newOb));

            float boxSAH = Node.SA(ref box);

            rootBVH->AddObject(adapter, newOb, ref box, boxSAH);
        }

        public void removeObject(T newObj)
        {
            var leaf = adapter.getLeaf(newObj);
            leaf.RemoveObject(adapter, newObj);
        }

        public int countBVHNodes()
        {
            return rootBVH->CountBVHNodes();
        }

        public void Dispose()
        {
            if(IsCreated)
            {    
                if (dataBuckets.IsCreated)
                {
                    foreach (var item in dataBuckets)
                    {
                        if (item.IsCreated)
                        {
                            item.Dispose();
                        }
                    }
                    dataBuckets.Dispose();
                }
                if(Nodes.IsCreated)
                {
                    Nodes.Dispose();
                }  
                if(_map.IsCreated)
                {
                    _map.Dispose();
                }
            }
        }

        /// <summary>
        /// initializes a BVH with a given nodeAdaptor, and object list.
        /// </summary>
        /// <param name="nodeAdaptor"></param>
        /// <param name="objects"></param>
        /// <param name="maxPerLeft">WARNING! currently this must be 1 to use dynamic BVH updates</param>
        public NativeBoundingHierarchy(IBVHNodeAdapter<T> nodeAdaptor, List<T> objects = null, int maxPerLeft = 1)
        {
            this.LEAF_OBJ_MAX = maxPerLeft;

            //nodeAdaptor.Allocate(this);

            this._map = new NativeHashMap<T, Node>(MaxNodes, Allocator.Persistent);

            //nodeAdaptor.setBVH(this);

            this.adapter = nodeAdaptor;

            this.adapter.SetBvH(this);               

            this.dataBuckets = new NativeBuffer<NativeBuffer<T>>(MaxBuckets, Allocator.Persistent);

            this.Nodes = new NativeBuffer<Node>(MaxNodes, Allocator.Persistent);

            _isCreated = 1;    

            //this.nodes = new NativeBuffer<Node2<T>>(100, Allocator.Persistent);

            //var root = new NativeBuffer<T>(10, Allocator.Persistent);

            //root.Add(new T());

            //var index = this.dataBuckets.Add(root);

            //var first = this.dataBuckets.InsertAfter(this.dataBuckets.Tail, root);

            //var test = this.dataBuckets[first.m_Index];

            //var rootNode = new Node<T>();

            //this.items.InsertAfter(items.Tail, rootNode);

            //this.items = new NativeLinkedList<T>(100, Allocator.Persistent);

            //if (objects?.Count > 0)
            //{
            //    rootBVH = new ssBVHNode<T>(this, objects);

            //}
            //else
            //{
            rootBVH = Node.CreateNode(this);
  
                //rootBVH.Items = new List<T>(); // it's a leaf, so give it an empty object list
            //}
        }


    }
}