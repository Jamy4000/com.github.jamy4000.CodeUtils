using System.Collections.Generic;
using UnityEngine;

namespace UnityTechnologies.CodeUtils.SpatialPartionning
{
    /// <summary>
    /// Public façade over <see cref="KDTreeInternal{TDimension,TDimensionComparer}"/>.
    /// Performs a one-time type switch at construction to instantiate the correct struct-specialised
    /// internal tree, giving the JIT full visibility into all comparer calls with zero virtual dispatch.
    /// </summary>
    /// <typeparam name="TDimension">
    /// Position type. Must be <see cref="float"/>, <see cref="Vector2"/>, or <see cref="Vector3"/>.
    /// </typeparam>
    public sealed class KDTree<TDimension> : ISpatialPartitioner<TDimension>
        where TDimension : struct
    {
        // We hold the heavily optimized tree behind the interface
        private readonly ISpatialPartitioner<TDimension> _internalTree;

        /// <summary>
        /// Constructs an empty KD-tree. The correct dimension-specialised internal tree is
        /// selected once here and reused for all subsequent operations.
        /// </summary>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when <typeparamref name="TDimension"/> is not <see cref="float"/>,
        /// <see cref="Vector2"/>, or <see cref="Vector3"/>.
        /// </exception>
        public KDTree()
        {
            // Type switch once upon creation to instantiate the heavily optimized version
            System.Type type = typeof(TDimension);
            
            if (type == typeof(float))
                _internalTree = (ISpatialPartitioner<TDimension>)(object)new KDTreeInternal<float, OneDimensionComparer>();

            else if (type == typeof(Vector2))
                _internalTree = (ISpatialPartitioner<TDimension>)(object)new KDTreeInternal<Vector2, TwoDimensionComparer>();

            else if (type == typeof(Vector3))
                _internalTree = (ISpatialPartitioner<TDimension>)(object)new KDTreeInternal<Vector3, ThreeDimensionComparer>();

            else
                throw new System.NotSupportedException($"Dimension type {type} is not supported.");
        }

        /// <summary>
        /// Convenience constructor that builds the tree immediately from the provided arrays.
        /// Equivalent to calling the default constructor followed by <see cref="InsertRange"/>.
        /// Uses the O(n log n) balanced median-split build path.
        /// </summary>
        /// <param name="elements">Positions of elements to insert. Must be the same length as <paramref name="elementsIDs"/>.</param>
        /// <param name="elementsIDs">Caller-supplied identifiers. Must be the same length as <paramref name="elements"/>.</param>
        public KDTree(TDimension[] elements, int[] elementsIDs) : this()
        {
            InsertRange(elements, elementsIDs);
        }

        // Forward all calls to the internal tree
        // This is ONE interface call per operation, which is perfectly fine.
        public void Insert(TDimension element, int elementID) => _internalTree.Insert(element, elementID);
        public void InsertRange(TDimension[] elements, int[] elementsIDs, int start = 0, int count = -1) => 
            _internalTree.InsertRange(elements, elementsIDs);
        
        public void Remove(TDimension element, int elementID) => _internalTree.Remove(element, elementID);
        public void RemoveAll() => _internalTree.RemoveAll();
        
        public bool TryQueryClosest(TDimension element, out QueryResult result) => 
            _internalTree.TryQueryClosest(element, out result);
        public int QueryWithinRange_NoAlloc(TDimension source, float range, QueryResult[] results, 
            bool sortResults = true) => _internalTree.QueryWithinRange_NoAlloc(source, range, results, sortResults);
        
        public void Dispose() => _internalTree.Dispose();
        public void Rebuild() => _internalTree.Rebuild();
        
        public void OnDrawGizmos() => _internalTree.OnDrawGizmos();
    }

    /// <summary>
    /// A generic KD-tree spatial partitioner that supports 1-D, 2-D, and 3-D position types
    /// via the <see cref="IDimensionComparer{TDimension}"/> abstraction.
    /// <para>
    /// The tree stores nodes in a flat <see cref="List{T}"/> indexed by integer "pointers", avoiding
    /// object allocation per node. Use <see cref="InsertRange"/> for initial bulk population, which
    /// builds a balanced tree in O(n log n) via median-split. Individual <see cref="Insert"/> calls
    /// after that traverse the existing structure iteratively.
    /// </para>
    /// </summary>
    /// <typeparam name="TDimension">
    /// The position type. Must be <see cref="float"/>, <see cref="Vector2"/>, or <see cref="Vector3"/>.
    /// </typeparam>
    /// <typeparam name="TDimensionComparer">
    /// The comparer struct type corresponding to <typeparamref name="TDimension"/>.
    /// Instantiated as <c>default</c> — must be a zero-allocation value type.
    /// </typeparam>
    internal sealed class KDTreeInternal<TDimension, TDimensionComparer> : ISpatialPartitioner<TDimension>
        where TDimension : struct
        where TDimensionComparer : struct, IDimensionComparer<TDimension>
    {
        /// <summary>
        /// A single node in the KD-tree, stored by value inside a flat list.
        /// Child references are integer indices into the same list (-1 = no child).
        /// </summary>
        private struct KDNode
        {
            /// <summary>Caller-supplied identifier for the element stored at this node.</summary>
            public int ExternalID;
            /// <summary>World-space position of the element stored at this node.</summary>
            public TDimension Position;
            /// <summary>Index of the left child in the node list, or -1 if absent.</summary>
            public int LeftNodeIndex;
            /// <summary>Index of the right child in the node list, or -1 if absent.</summary>
            public int RightNodeIndex;

            /// <summary><see langword="true"/> if this node has a left child.</summary>
            public readonly bool HasLeftChild => LeftNodeIndex != -1;
            /// <summary><see langword="true"/> if this node has a right child.</summary>
            public readonly bool HasRightChild => RightNodeIndex != -1;

            /// <param name="elementID">Caller-supplied identifier.</param>
            /// <param name="position">World-space position.</param>
            /// <param name="leftNodeIndex">Left child index. Defaults to -1 (none).</param>
            /// <param name="rightNodeIndex">Right child index. Defaults to -1 (none).</param>
            public KDNode(int elementID, TDimension position, int leftNodeIndex = -1, int rightNodeIndex = -1)
            {
                ExternalID = elementID;
                Position = position;
                LeftNodeIndex = leftNodeIndex;
                RightNodeIndex = rightNodeIndex;
            }
        }

        private TDimensionComparer _dimensionComparer = default;

        private KDNode[] _nodes;
        private int _nodesCount;
        private int _freeListHead = -1; // Points to the first deleted node index

        /// <summary>
        /// Constructs an empty internal KD-tree with the given initial node-array capacity.
        /// </summary>
        /// <param name="initialCapacity">Pre-allocated number of node slots. Defaults to 256.</param>
        internal KDTreeInternal(int initialCapacity = 256)
        {
            _nodes = new KDNode[initialCapacity];
        }

        /// <summary>
        /// Constructs an internal KD-tree and immediately bulk-inserts the provided elements
        /// using the O(n log n) balanced median-split build path.
        /// </summary>
        /// <param name="elements">Positions to insert. May be <see langword="null"/> or empty.</param>
        /// <param name="elementsIDs">Caller-supplied identifiers, parallel to <paramref name="elements"/>.</param>
        /// <param name="initialCapacity">
        /// Pre-allocated node-array capacity. Pass -1 to default to <c>elements.Length</c>.
        /// </param>
        internal KDTreeInternal(TDimension[] elements, int[] elementsIDs, int initialCapacity = -1)
            : this(initialCapacity < 0 ? (elements?.Length ?? 256) : initialCapacity)
        {
            if (elements != null && elements.Length > 0)
                InsertRange(elements, elementsIDs);
        }

        /// <inheritdoc/>
        public void Dispose() { }

#region Rebuild Logic
        /// <summary>
        /// Extracts all active nodes, strips away fragmented memory (the Free List), 
        /// and rebuilds the entire tree into perfect mathematical balance.
        /// </summary>
        public void Rebuild()
        {
            if (_nodesCount == 0) return;

            // Rent TWO parallel buffers
            var workPos = System.Buffers.ArrayPool<TDimension>.Shared.Rent(_nodesCount);
            var workIds = System.Buffers.ArrayPool<int>.Shared.Rent(_nodesCount);
            int activeCount = 0;

            try
            {
                ExtractNodesRecursive(0, workPos, workIds, ref activeCount);

                if (activeCount == 0)
                {
                    RemoveAll();
                    return;
                }

                _nodesCount = 0;
                _freeListHead = -1;

                BuildBalanced(workPos, workIds, 0, activeCount - 1, 0);
            }
            finally
            {
                System.Buffers.ArrayPool<TDimension>.Shared.Return(workPos);
                System.Buffers.ArrayPool<int>.Shared.Return(workIds);
            }
        }

        /// <summary>
        /// Recursively walks the live tree from <paramref name="nodeIndex"/> and copies each
        /// valid node's data into <paramref name="buffer"/>.
        /// <para>
        /// Freed nodes are never reachable from live child pointers under normal operation,
        /// but the <c>ExternalID == -1</c> guard makes this robust against any future
        /// inconsistency in the free-list bookkeeping.
        /// </para>
        /// </summary>
        /// <param name="nodeIndex">Index of the current node to visit.</param>
        /// <param name="posBuffer">Output buffer to write live elements into.</param>
        /// <param name="activeCount">Running write cursor; advanced for each element written.</param>
        private void ExtractNodesRecursive(int nodeIndex, TDimension[] posBuffer, int[] idBuffer, ref int activeCount)
        {
            if (nodeIndex == -1) return;

            KDNode node = _nodes[nodeIndex];
            if (node.ExternalID == -1) return; // Skip freed nodes

            posBuffer[activeCount] = node.Position;
            idBuffer[activeCount] = node.ExternalID;
            activeCount++;

            ExtractNodesRecursive(node.LeftNodeIndex, posBuffer, idBuffer, ref activeCount);
            ExtractNodesRecursive(node.RightNodeIndex, posBuffer, idBuffer, ref activeCount);
        }
#endregion Rebuild Logic

#region Insertion Logic

#region Single Insert
        /// <summary>
        /// Inserts a single element into the tree.
        /// If the tree is empty the element becomes the root; otherwise the tree is traversed
        /// iteratively to find the correct leaf position.
        /// </summary>
        /// <param name="element">World-space position of the element.</param>
        /// <param name="elementID">Caller-supplied identifier.</param>
        public void Insert(TDimension element, int elementID)
        {
            if (_nodesCount == 0)
            {
                AllocateNode(elementID, element); // Root goes to index 0
                return;
            }

            Insert_Internal(element, elementID);
        }

        /// <summary>
        /// Iterative insert: walks down the tree choosing left or right at each level based on the
        /// current splitting axis, then appends a new leaf and links it to its parent.
        /// </summary>
        /// <param name="position">World-space position to insert.</param>
        /// <param name="elementID">Caller-supplied identifier.</param>
        private void Insert_Internal(TDimension position, int elementID)
        {
            // Index 0 is always the live root:
            // - BuildBalanced always places the root at index 0 (first AllocateNode call on a clean tree).
            // - Remove_Internal never removes the root slot; it overwrites it with the successor's data.
            // - Rebuild resets _nodesCount to 0, so the next BuildBalanced again starts at index 0.
            int depth = 0;
            int currentIndex = 0;

            while (true)
            {
                KDNode current = _nodes[currentIndex];
                int axis = depth % _dimensionComparer.GetDimension();
                bool goLeft = _dimensionComparer.Compare(position, current.Position, axis) < 0;

                if (goLeft)
                {
                    if (!current.HasLeftChild)
                    {
                        current.LeftNodeIndex = AllocateNode(elementID, position);
                        _nodes[currentIndex] = current;
                        return;
                    }
                    currentIndex = current.LeftNodeIndex;
                }
                else
                {
                    if (!current.HasRightChild)
                    {
                        current.RightNodeIndex = AllocateNode(elementID, position);
                        _nodes[currentIndex] = current;
                        return;
                    }
                    currentIndex = current.RightNodeIndex;
                }
                depth++;
            }
        }
#endregion

#region Multiple Inserts
        /// <summary>
        /// Bulk-inserts a slice of elements and builds a balanced KD-tree via median-split.
        /// <para>
        /// A working copy of the slice is sorted on alternating axes so that the median element
        /// of each sub-range becomes the splitting node. This guarantees a tree depth of
        /// ⌈log₂(n)⌉ regardless of input order, giving O(log n) query performance.
        /// </para>
        /// <para>
        /// If the tree already contains nodes from prior <see cref="Insert"/> calls, each element
        /// is inserted individually via the iterative path to keep the tree unified. Call
        /// <see cref="Rebuild"/> afterwards to restore perfect balance.
        /// </para>
        /// </summary>
        /// <param name="elements">Source array of positions.</param>
        /// <param name="elementsIDs">Caller-supplied identifiers, parallel to <paramref name="elements"/>.</param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        public void InsertRange(TDimension[] elements, int[] elementsIDs, int start = 0, int count = -1)
        {
            if (elements == null || elements.Length == 0 ||
                elementsIDs == null || elements.Length != elementsIDs.Length)
            {
                throw new System.ArgumentException("Elements and IDs must be non-empty and of the same length.");
            }

            int elementsCount = count < 0 ? elements.Length - start : count;
            if (elementsCount == 0) return;

            if (_nodesCount > 0)
            {
                for (int i = 0; i < elementsCount; i++)
                    Insert_Internal(elements[start + i], elementsIDs[start + i]);
                return;
            }

            // Rent TWO parallel buffers
            var workPos = System.Buffers.ArrayPool<TDimension>.Shared.Rent(elementsCount);
            var workIds = System.Buffers.ArrayPool<int>.Shared.Rent(elementsCount);
            try
            {
                // Safely copy the user's data into the rented working buffers
                System.Array.Copy(elements, start, workPos, 0, elementsCount);
                System.Array.Copy(elementsIDs, start, workIds, 0, elementsCount);

                ArrayExtensions.EnsureCapacity(ref _nodes, elementsCount);
                BuildBalanced(workPos, workIds, 0, elementsCount - 1, 0); 
            }
            finally
            {
                System.Buffers.ArrayPool<TDimension>.Shared.Return(workPos);
                System.Buffers.ArrayPool<int>.Shared.Return(workIds);
            }
        }
#endregion Multiple Inserts
        
        /// <summary>
        /// Returns the index of a freshly initialised node containing <paramref name="elementID"/>
        /// and <paramref name="position"/>. Reuses a slot from the free list when one is available;
        /// otherwise appends a new slot to the end of <see cref="_nodes"/>, growing the array if needed.
        /// </summary>
        /// <param name="elementID">Caller-supplied identifier for the new node.</param>
        /// <param name="position">World-space position for the new node.</param>
        /// <returns>The array index of the allocated node.</returns>
        private int AllocateNode(int elementID, TDimension position)
        {
            // 1. If we have a deleted node, recycle its index to prevent memory leaks!
            if (_freeListHead != -1)
            {
                int reusedIndex = _freeListHead;
                // The deleted node stored the NEXT free index in its LeftNodeIndex
                _freeListHead = _nodes[reusedIndex].LeftNodeIndex;

                _nodes[reusedIndex] = new KDNode(elementID, position);
                return reusedIndex;
            }

            // 2. Otherwise, allocate a brand new slot at the end of the array
            ArrayExtensions.EnsureCapacity(ref _nodes, _nodesCount + 1);
            int newIndex = _nodesCount;
            _nodes[newIndex] = new KDNode(elementID, position);
            _nodesCount++;

            return newIndex;
        }

        /// <summary>
        /// Recursively builds a balanced subtree over <c>buffer[low..high]</c> using median-split.
        /// Places the median element as a new node, recurses into the left and right halves,
        /// then patches the child indices back via a read-modify-write on the flat list.
        /// </summary>
        /// <param name="buffer">Working copy of the elements to partition.</param>
        /// <param name="low">Inclusive lower bound of the current slice.</param>
        /// <param name="high">Inclusive upper bound of the current slice.</param>
        /// <param name="depth">Current tree depth, used to cycle the splitting axis.</param>
        /// <returns>The list index of the node created for this call, or -1 if the slice is empty.</returns>
        private int BuildBalanced(TDimension[] pos, int[] ids, int low, int high, int depth)
        {
            if (low > high) return -1;

            int axis = depth % _dimensionComparer.GetDimension();
            int mid = low + (high - low) / 2;
    
            // Pass both arrays down to be partitioned in parallel
            NthElement(pos, ids, low, high, mid, axis);

            int nodeIndex = AllocateNode(ids[mid], pos[mid]);

            int leftIndex  = BuildBalanced(pos, ids, low, mid - 1, depth + 1);
            int rightIndex = BuildBalanced(pos, ids, mid + 1, high, depth + 1);

            _nodes[nodeIndex].LeftNodeIndex  = leftIndex;
            _nodes[nodeIndex].RightNodeIndex = rightIndex;

            return nodeIndex;
        }

        /// <summary>
        /// Rearranges <c>buffer[lo..hi]</c> so that <c>buffer[k]</c> holds the element that would
        /// occupy position <paramref name="k"/> after a full sort on <paramref name="axis"/>.
        /// All elements before index <paramref name="k"/> are ≤ <c>buffer[k]</c> and all elements
        /// after are ≥ <c>buffer[k]</c>, but neither half is otherwise sorted.
        /// Uses the quickselect algorithm — O(n) average, O(n²) worst case.
        /// </summary>
        /// <param name="buffer">Array to rearrange in-place.</param>
        /// <param name="lo">Inclusive lower bound.</param>
        /// <param name="hi">Inclusive upper bound.</param>
        /// <param name="k">Target index — the element that belongs here after a full sort.</param>
        /// <param name="axis">Zero-based axis index used for comparisons.</param>
        private void NthElement(TDimension[] pos, int[] ids, int lo, int hi, int k, int axis)
        {
            while (lo < hi)
            {
                int pivotIndex = Partition(pos, ids, lo, hi, axis);
                if      (pivotIndex == k) return;
                
                if (k < pivotIndex)  
                    hi = pivotIndex - 1;
                else
                    lo = pivotIndex + 1;
            }
        }

        /// <summary>
        /// Partitions <c>buffer[lo..hi]</c> around a pivot chosen via median-of-three,
        /// placing all elements ≤ pivot before it and all elements &gt; pivot after it.
        /// </summary>
        /// <param name="buffer">Array to partition in-place.</param>
        /// <param name="lo">Inclusive lower bound.</param>
        /// <param name="hi">Inclusive upper bound. The pivot is placed here before partitioning.</param>
        /// <param name="axis">Zero-based axis index used for comparisons.</param>
        /// <returns>The final index of the pivot element after partitioning.</returns>
        private int Partition(TDimension[] pos, int[] ids, int lo, int hi, int axis)
        {
            int mid = lo + (hi - lo) / 2;
            if (_dimensionComparer.Compare(pos[mid], pos[lo], axis) < 0)
            {
                pos.Swap(lo, mid);
                ids.Swap(lo, mid);
            }

            if (_dimensionComparer.Compare(pos[hi], pos[lo], axis) < 0)
            {
                pos.Swap(lo, hi);
                ids.Swap(lo, hi);
            }

            if (_dimensionComparer.Compare(pos[mid], pos[hi], axis) < 0)
            {
                pos.Swap(mid, hi);
                ids.Swap(mid, hi);
            }

            var pivot = pos[hi];
            int i = lo;
            for (int j = lo; j < hi; j++)
            {
                if (_dimensionComparer.Compare(pos[j], pivot, axis) <= 0)
                {
                    pos.Swap(i, j);
                    ids.Swap(i, j);
                    i++;
                }
            }
            pos.Swap(i, hi);
            ids.Swap(i, hi);
            
            return i;
        }
        
#endregion Insertion Logic

#region Removal Logic

        /// <summary>
        /// Removes a single element identified by both <paramref name="element"/> and
        /// <paramref name="elementID"/> from the tree.
        /// If the removed node was the only node in the tree, the list is cleared entirely.
        /// </summary>
        /// <param name="element">World-space position of the element to remove.</param>
        /// <param name="elementID">Identifier of the element to remove.</param>
        public void Remove(TDimension element, int elementID)
        {
            if (_nodesCount == 0) return;

            int newRoot = Remove_Internal(0, element, elementID, 0);

            // If the root itself was a leaf and was removed, the list still holds its slot.
            // Clear it so the tree is truly empty.
            if (newRoot == -1)
                RemoveAll();
        }

        /// <summary>
        /// Recursively removes the node matching <paramref name="elementID"/> from the subtree
        /// rooted at <paramref name="currentIndex"/>.
        /// <para>
        /// When the target node has two children, it is replaced by its in-order successor
        /// (minimum of the right subtree) and the successor is then removed recursively.
        /// </para>
        /// </summary>
        /// <param name="currentIndex">Root of the subtree to search.</param>
        /// <param name="position">Position of the element to remove, used for tree traversal.</param>
        /// <param name="elementID">Identifier of the element to remove.</param>
        /// <param name="depth">Current depth, used to determine the splitting axis.</param>
        /// <returns>
        /// The index that should replace <paramref name="currentIndex"/> in the parent's child pointer,
        /// or -1 if the subtree is now empty.
        /// </returns>
        private int Remove_Internal(int currentIndex, TDimension position, int elementID, int depth)
        {
            if (currentIndex == -1) return -1; // Base case: node not found

            KDNode current = _nodes[currentIndex];
            int axis = depth % _dimensionComparer.GetDimension();

            if (current.ExternalID == elementID)
            {
                // 1. If we have a right child, find the min in the right subtree and replace.
                if (current.RightNodeIndex != -1)
                {
                    int minIndex = FindMin(current.RightNodeIndex, axis, depth + 1);
                    KDNode minNode = _nodes[minIndex];

                    current.ExternalID = minNode.ExternalID;
                    current.Position = minNode.Position;
                    _nodes[currentIndex] = current;

                    current.RightNodeIndex = Remove_Internal(current.RightNodeIndex, minNode.Position, minNode.ExternalID, depth + 1);
                }
                // 2. If we ONLY have a left child, find the min in the left subtree, replace, 
                //    and MOVE the left subtree to become the right subtree!
                else if (current.LeftNodeIndex != -1)
                {
                    int minIndex = FindMin(current.LeftNodeIndex, axis, depth + 1);
                    KDNode minNode = _nodes[minIndex];

                    current.ExternalID = minNode.ExternalID;
                    current.Position = minNode.Position;
                    _nodes[currentIndex] = current;

                    current.RightNodeIndex = Remove_Internal(current.LeftNodeIndex, minNode.Position, minNode.ExternalID, depth + 1);
                    current.LeftNodeIndex = -1; // The left subtree is now the right subtree
                }
                // 3. Leaf node
                else
                {
                    FreeNode(currentIndex);
                    return -1;
                }
                
                _nodes[currentIndex] = current;
            }
            else
            {
                bool goLeft = _dimensionComparer.Compare(position, current.Position, axis) < 0;
                if (goLeft)
                    current.LeftNodeIndex = Remove_Internal(current.LeftNodeIndex, position, elementID, depth + 1);
                else
                    current.RightNodeIndex = Remove_Internal(current.RightNodeIndex, position, elementID, depth + 1);

                _nodes[currentIndex] = current; // Write updated child indices back
            }

            return currentIndex; // Return the current index
        }

        /// <inheritdoc/>
        public void RemoveAll()
        {
            _nodesCount = 0;
            _freeListHead = -1;
        }
        
        /// <summary>
        /// Marks the node at <paramref name="index"/> as deleted by writing a sentinel
        /// (<c>ExternalID = -1</c>) and prepending it to the intrusive free list.
        /// Its <c>LeftNodeIndex</c> stores the previous free-list head so the chain can be
        /// walked by <see cref="AllocateNode"/>.
        /// </summary>
        /// <param name="index">Index of the node to free. Must be a valid, live node index.</param>
        private void FreeNode(int index)
        {
            // Wipe the data, and store the current Free List head in the LeftNodeIndex
            _nodes[index] = new KDNode(-1, default, _freeListHead, -1);

            // This node is now the new head of the Free List
            _freeListHead = index;
        }
        
        /// <summary>
        /// Finds the node with the smallest value on <paramref name="axis"/> within the subtree
        /// rooted at <paramref name="currentIndex"/>. Used during removal to locate the in-order
        /// successor when a two-child node must be replaced.
        /// </summary>
        /// <param name="nodeIndex">The index of the current node being checked.</param>
        /// <param name="targetAxis">The axis on which to find the minimum.</param>
        /// <param name="depth">Current depth, used to determine the current splitting axis.</param>
        /// <returns>The list index of the node with the minimum value on <paramref name="axis"/>.</returns>
        private int FindMin(int nodeIndex, int targetAxis, int depth)
        {
            if (nodeIndex == -1) return -1;

            KDNode node = _nodes[nodeIndex];
            int currentAxis = depth % _dimensionComparer.GetDimension();

            // If the current splitting axis matches the axis we are searching for, 
            // the minimum MUST be in the left subtree (if it exists).
            if (currentAxis == targetAxis)
            {
                if (node.LeftNodeIndex == -1) return nodeIndex;
                return FindMin(node.LeftNodeIndex, targetAxis, depth + 1);
            }

            // Otherwise, the minimum could be anywhere. We must check this node, 
            // the left subtree, and the right subtree.
            int leftMinIndex = FindMin(node.LeftNodeIndex, targetAxis, depth + 1);
            int rightMinIndex = FindMin(node.RightNodeIndex, targetAxis, depth + 1);

            int minIndex = nodeIndex;

            if (leftMinIndex != -1 &&
                _dimensionComparer.Compare(_nodes[leftMinIndex].Position, _nodes[minIndex].Position, targetAxis) < 0)
            {
                minIndex = leftMinIndex;
            }

            if (rightMinIndex != -1 &&
                _dimensionComparer.Compare(_nodes[rightMinIndex].Position, _nodes[minIndex].Position, targetAxis) < 0)
            {
                minIndex = rightMinIndex;
            }

            return minIndex;
        }

#endregion Removal Logic
        
#region Query Logic
        /// <inheritdoc/>
        public bool TryQueryClosest(TDimension element, out QueryResult result)
        {
            result = default;
            if (_nodesCount == 0) return false;

            float bestDistanceSq = float.PositiveInfinity;
            int bestID = -1;

            bool found = TryQueryClosestRecursive(0, 0, element, ref bestDistanceSq, ref bestID);
            if (found)
                result = new QueryResult(bestID, Mathf.Sqrt(bestDistanceSq));

            return found;
        }

        /// <summary>
        /// Recursive nearest-neighbour search.
        /// Visits the near child first to tighten <paramref name="bestDistanceSq"/> quickly, then
        /// crosses to the far child only if the axis-aligned distance to the splitting plane is
        /// less than the current best squared distance.
        /// </summary>
        /// <param name="nodeIndex">Current node to visit, or -1 to return immediately.</param>
        /// <param name="depth">Current depth, used to determine the splitting axis.</param>
        /// <param name="source">Query position.</param>
        /// <param name="bestDistanceSq">Squared distance to the closest element found so far; updated in-place.</param>
        /// <param name="bestID">ID of the closest element found so far; updated in-place.</param>
        /// <returns><see langword="true"/> if a closer element was found in this subtree.</returns>
        private bool TryQueryClosestRecursive(int nodeIndex, int depth, TDimension source, ref float bestDistanceSq, ref int bestID)
        {
            if (nodeIndex == -1) return false;

            bool found = false;
            KDNode currentNode = _nodes[nodeIndex];
            float distanceSq = _dimensionComparer.CalculateDistanceSq(currentNode.Position, source);

            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestID = currentNode.ExternalID;
                found = true;
            }

            int axis = depth % _dimensionComparer.GetDimension();
            float sourceComponent = _dimensionComparer.GetComponentOnAxis(source, axis);
            float nodeComponent   = _dimensionComparer.GetComponentOnAxis(currentNode.Position, axis);

            int nearIndex = sourceComponent < nodeComponent ? currentNode.LeftNodeIndex : currentNode.RightNodeIndex;
            int farIndex  = nearIndex == currentNode.LeftNodeIndex ? currentNode.RightNodeIndex : currentNode.LeftNodeIndex;

            // Search near side first to tighten bestDistanceSq before evaluating the far side
            if (TryQueryClosestRecursive(nearIndex, depth + 1, source, ref bestDistanceSq, ref bestID))
                found = true;

            // Only cross to the far side if the splitting plane is within reach
            float axisDiff = sourceComponent - nodeComponent;
            if (axisDiff * axisDiff < bestDistanceSq)
            {
                if (TryQueryClosestRecursive(farIndex, depth + 1, source, ref bestDistanceSq, ref bestID))
                    found = true;
            }

            return found;
        }
        
        /// <inheritdoc/>
        public int QueryWithinRange_NoAlloc(TDimension source, float range, QueryResult[] results, bool sortResults = true)
        {
            if (results.Length == 0 || _nodesCount == 0) 
                return 0;
            
            int foundCount = 0;
            QueryRangeRecursive(0, 0, source, range * range, results, ref foundCount);

            return QueryRangeUtils.FinaliseResults(results, foundCount, sortResults);
        }

        /// <summary>
        /// Recursive range search. Crosses to the far side only if the splitting plane is
        /// within the search radius. Delegates result management to
        /// <see cref="QueryRangeUtils.TryAddResult"/>; squared distances are stored during
        /// traversal and converted by <see cref="QueryRangeUtils.FinaliseResults"/> at the end.
        /// </summary>
        /// <param name="nodeIndex">Current node to visit, or -1 to return immediately.</param>
        /// <param name="depth">Current depth, used to determine the splitting axis.</param>
        /// <param name="source">Query position.</param>
        /// <param name="rangeSq">Squared search radius.</param>
        /// <param name="results">Pre-allocated result buffer (stores squared distances during search).</param>
        /// <param name="foundCount">Running count of candidates; advanced by <see cref="QueryRangeUtils.TryAddResult"/>.</param>
        private void QueryRangeRecursive(int nodeIndex, int depth, TDimension source, float rangeSq, QueryResult[] results, ref int foundCount)
        {
            if (nodeIndex == -1) return;

            KDNode currentNode = _nodes[nodeIndex];

            float distanceSq = _dimensionComparer.CalculateDistanceSq(currentNode.Position, source);
            if (distanceSq <= rangeSq)
            {
                QueryRangeUtils.TryAddResult(results, ref foundCount, currentNode.ExternalID, distanceSq);
            }

            int axis = depth % _dimensionComparer.GetDimension();
            float sourceComponent = _dimensionComparer.GetComponentOnAxis(source, axis);
            float nodeComponent = _dimensionComparer.GetComponentOnAxis(currentNode.Position, axis);

            // Determine which child is nearer
            int nearIndex = sourceComponent < nodeComponent ? currentNode.LeftNodeIndex : currentNode.RightNodeIndex;
            int farIndex = nearIndex == currentNode.LeftNodeIndex ? currentNode.RightNodeIndex : currentNode.LeftNodeIndex;

            // Always search the near side
            QueryRangeRecursive(nearIndex, depth + 1, source, rangeSq, results, ref foundCount);

            // Only search the far side if the splitting plane intersects our search radius
            float axisDiff = sourceComponent - nodeComponent;
            if (axisDiff * axisDiff <= rangeSq)
            {
                QueryRangeRecursive(farIndex, depth + 1, source, rangeSq, results, ref foundCount);
            }
        }

#endregion Query Logic

#if UNITY_EDITOR
        /// <inheritdoc/>
        public void OnDrawGizmos()
        {
            if (_nodesCount > 0)
                DrawGizmosRecursive(0);
        }

        /// <summary>
        /// Recursively draws a sphere at each node's position and a line to each child,
        /// starting from <paramref name="nodeIndex"/>.
        /// </summary>
        /// <param name="nodeIndex">Index of the current node to draw, or -1 to return immediately.</param>
        private void DrawGizmosRecursive(int nodeIndex)
        {
            if (nodeIndex == -1) return;

            KDNode currentNode = _nodes[nodeIndex];
            Vector3 parentPosition = _dimensionComparer.ToVector3(currentNode.Position);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(parentPosition, 0.1f);

            Gizmos.color = Color.red;
            if (currentNode.HasLeftChild)
            {
                Gizmos.DrawLine(parentPosition, _dimensionComparer.ToVector3(_nodes[currentNode.LeftNodeIndex].Position));
                DrawGizmosRecursive(currentNode.LeftNodeIndex);
            }

            if (currentNode.HasRightChild)
            {
                Gizmos.DrawLine(parentPosition, _dimensionComparer.ToVector3(_nodes[currentNode.RightNodeIndex].Position));
                DrawGizmosRecursive(currentNode.RightNodeIndex);
            }
        }
#endif
    }
}