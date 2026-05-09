using UnityEngine;

namespace CodeUtils.SpatialPartitioning
{
    /// <summary>
    /// A 2-D spatial partitioner that recursively subdivides space into 4 axis-aligned quadrants
    /// on the XY plane.
    /// <para>
    /// Each leaf node stores up to <c>maxElementPerNode</c> elements. When a leaf overflows it is
    /// subdivided and its elements pushed into the appropriate children. Nodes are managed through
    /// a <see cref="QuadtreePool"/> to avoid per-subdivision heap allocations.
    /// </para>
    /// <para>
    /// Use <see cref="Insert"/> for single insertions and <see cref="InsertRange"/> for bulk
    /// insertion, which partitions elements in-place by quadrant at every level for improved
    /// cache locality. Query operations are allocation-free.
    /// </para>
    /// </summary>
    public class Quadtree : ISpatialPartitioner<Vector2>
    {
        private AABB _boundary;

        // Cannot be readonly as we may resize it when reaching MaxDepth
        private QuadtreeElement[] _elements;
        private int _elementsCount = 0;
        private readonly Quadtree[] _children;
        private bool HasChildren => _children[0] != null;
        private readonly QuadtreePool _quadtreePool;

        private readonly int _maxElementsCountPerNode;
        private readonly int _maxDepth;
        private const int _MAX_CHILDREN_COUNT = 4;
        private int _currentDepth = 0;

        /// <summary>
        /// Constructs a new Quadtree rooted at <paramref name="center"/> with the given <paramref name="size"/>.
        /// </summary>
        /// <param name="center">World-space centre of the root boundary on the XY plane.</param>
        /// <param name="size">Full extents of the root boundary on X and Y. Both components must be positive.</param>
        /// <param name="pool">
        /// Optional node pool to reuse. When <see langword="null"/>, a new <see cref="QuadtreePool"/> is created
        /// and owned by this instance.
        /// </param>
        /// <param name="maxDepth">
        /// Maximum subdivision depth. Elements that would overflow a node at this depth are stored
        /// directly, growing the leaf array if needed. Defaults to 8.
        /// </param>
        /// <param name="maxElementPerNode">
        /// Number of elements a leaf can hold before it is subdivided. Defaults to 8.
        /// </param>
        public Quadtree(Vector2 center, Vector2 size, QuadtreePool pool = null,
            int maxDepth = 8, int maxElementPerNode = 8)
        {
            Debug.Assert(size.x > 0f && size.y > 0f, "Size must be greater than zero");

            _maxElementsCountPerNode = maxElementPerNode;
            _maxDepth = maxDepth;
            _quadtreePool = pool ?? new QuadtreePool(center, size, CreateQuadtree);

            Vector2 halfSize = size * 0.5f;
            _boundary = new AABB(
                center.x - halfSize.x,
                center.y - halfSize.y,
                center.x + halfSize.x,
                center.y + halfSize.y);

            _elements = new QuadtreeElement[maxElementPerNode];
            _children = new Quadtree[_MAX_CHILDREN_COUNT];
        }
        
        /// <summary>
        /// Convenience constructor that builds the tree immediately from the provided arrays.
        /// Equivalent to calling the primary constructor followed by <see cref="InsertRange"/>.
        /// </summary>
        /// <param name="center">World-space centre of the root boundary on the XY plane.</param>
        /// <param name="size">Full extents of the root boundary on X and Y. Both components must be positive.</param>
        /// <param name="elements">Positions of elements to insert. Must be the same length as <paramref name="elementsIDs"/>.</param>
        /// <param name="elementsIDs">Caller-supplied identifiers. Must be the same length as <paramref name="elements"/>.</param>
        /// <param name="pool">Optional node pool. See primary constructor.</param>
        /// <param name="maxDepth">Maximum subdivision depth. Defaults to 8.</param>
        /// <param name="maxElementsCountPerNode">Leaf capacity before subdivision. Defaults to 8.</param>
        public Quadtree(Vector2 center, Vector2 size, Vector2[] elements, int[] elementsIDs,
            QuadtreePool pool = null, int maxDepth = 8, int maxElementsCountPerNode = 8) :
            this(center, size, pool, maxDepth, maxElementsCountPerNode)
        {
            InsertRange(elements, elementsIDs);
        }

        /// <summary>
        /// Releases all child nodes back to the pool and clears child references.
        /// Does not affect the root node itself.
        /// </summary>
        public void Dispose()
        {
            RemoveAll();
            _quadtreePool.Dispose();
        }

        /// <summary>
        /// Factory method used by <see cref="QuadtreePool"/> to construct pool instances with
        /// the same depth and capacity settings as this tree.
        /// </summary>
        /// <param name="center">Centre of the new node's boundary.</param>
        /// <param name="size">Size of the new node's boundary.</param>
        /// <returns>A new <see cref="Quadtree"/> sharing this tree's pool and settings.</returns>
        private Quadtree CreateQuadtree(Vector2 center, Vector2 size)
        {
            return new Quadtree(center, size, _quadtreePool, _maxDepth, _maxElementsCountPerNode);
        }

#region Rebuild Logic
        /// <summary>
        /// Rebuilds the Quadtree to defragment memory and trim empty branches.
        /// Extracts all active elements, completely clears the tree structure,
        /// and bulk-reinserts them using the optimised in-place partitioner.
        /// <para>
        /// Call this periodically after many <see cref="Remove"/> operations to recover memory
        /// held by now-empty branches that were never merged due to sibling activity.
        /// </para>
        /// </summary>
        public void Rebuild()
        {
            int totalElements = CountElementsRecursive(this);
            if (totalElements == 0)
            {
                RemoveAll();
                return;
            }

            var positions = System.Buffers.ArrayPool<Vector2>.Shared.Rent(totalElements);
            var ids       = System.Buffers.ArrayPool<int>.Shared.Rent(totalElements);

            try
            {
                int writeIndex = 0;
                ExtractElementsRecursive(this, positions, ids, ref writeIndex);

                // Wipe the current tree structure, returning all child nodes to the pool
                RemoveAll();

                // Bulk-reinsert using the internal partitioner — bypasses the public
                // argument validation and operates directly on the rented buffers
                InsertRange_Recursive(this, positions, ids, 0, totalElements);
            }
            finally
            {
                System.Buffers.ArrayPool<Vector2>.Shared.Return(positions);
                System.Buffers.ArrayPool<int>.Shared.Return(ids);
            }
        }

        /// <summary>
        /// Recursively counts all elements stored in leaf nodes of the subtree rooted at <paramref name="node"/>.
        /// </summary>
        /// <param name="node">Root of the subtree to count.</param>
        /// <returns>Total number of elements in the subtree.</returns>
        private static int CountElementsRecursive(Quadtree node)
        {
            // Elements only exist in leaves
            if (!node.HasChildren)
                return node._elementsCount;

            int count = 0;
            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
            {
                if (node._children[i] != null)
                    count += CountElementsRecursive(node._children[i]);
            }
            return count;
        }

        /// <summary>
        /// Recursively copies all elements from leaf nodes of the subtree rooted at <paramref name="node"/>
        /// into the provided output arrays, advancing <paramref name="writeIndex"/> as elements are written.
        /// </summary>
        /// <param name="node">Root of the subtree to extract from.</param>
        /// <param name="positions">Output array for element positions. Must be at least <c>writeIndex + elementCount</c> long.</param>
        /// <param name="ids">Output array for element IDs. Must be the same length as <paramref name="positions"/>.</param>
        /// <param name="writeIndex">Current write cursor; advanced in-place for each element written.</param>
        private static void ExtractElementsRecursive(Quadtree node, Vector2[] positions, int[] ids, ref int writeIndex)
        {
            if (!node.HasChildren)
            {
                for (int i = 0; i < node._elementsCount; i++)
                {
                    positions[writeIndex] = node._elements[i].Position;
                    ids[writeIndex]       = node._elements[i].ElementID;
                    writeIndex++;
                }
                return;
            }

            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
            {
                if (node._children[i] != null)
                    ExtractElementsRecursive(node._children[i], positions, ids, ref writeIndex);
            }
        }
#endregion Rebuild Logic

#region Insert Logic

#region Single Insert
        /// <inheritdoc/>
        public void Insert(Vector2 element, int elementID)
        {
            Insert_Internal(this, element, elementID);
        }

        /// <summary>
        /// Recursive single-element insert.
        /// Rejects positions outside the node boundary, delegates to children when subdivided,
        /// and subdivides the node when it overflows at non-maximum depth.
        /// </summary>
        /// <param name="node">The node to insert into.</param>
        /// <param name="position">World-space 2-D position to insert.</param>
        /// <param name="elementID">Caller-supplied identifier.</param>
        /// <returns><see langword="true"/> if the element was accepted by this node or one of its children.</returns>
        private static bool Insert_Internal(Quadtree node, Vector2 position, int elementID)
        {
            // If this is the wrong quadrant
            if (!node._boundary.Contains(position))
                return false;

            // If node has children, insert the point into the appropriate quadrant
            if (node.HasChildren)
                return InsertIntoChildren(node, position, elementID);

            // If node is not full yet OR we have reached the maximum depth
            if (node._elementsCount < node._maxElementsCountPerNode || node._currentDepth == node._maxDepth)
            {
                // At max depth the array may overflow — grow it in-place via ref
                if (node._elementsCount + 1 > node._maxElementsCountPerNode)
                    ArrayExtensions.EnsureCapacity(ref node._elements, node._elementsCount + 1);

                node._elements[node._elementsCount] = new(elementID, position);
                node._elementsCount++;
                return true;
            }

            // We have reached max amount of elements in the node, so we divide it in 4 quadrants
            Subdivide(node);

            for (int elementIndex = 0; elementIndex < node._elementsCount; elementIndex++)
            {
                var element = node._elements[elementIndex];
                InsertIntoChildren(node, element.Position, element.ElementID);
            }

            // Clear the elements from the parent node as they are now in the children
            node._elementsCount = 0;

            return InsertIntoChildren(node, position, elementID);
        }
#endregion Single Insert

#region Multiple Insert
        /// <inheritdoc/>
        /// <remarks>
        /// Elements are partitioned in-place by quadrant at every tree level before recursing,
        /// so spatially adjacent elements are inserted together. No extra allocation is made.
        /// </remarks>
        public void InsertRange(Vector2[] elements, int[] elementsIDs, int start = 0, int count = -1)
        {
            if (elements == null || elements.Length == 0 ||
                elementsIDs == null || elementsIDs.Length != elements.Length)
            {
                throw new System.ArgumentException("Elements and IDs must be non-empty and of the same length.");
            }

            int actualCount = count < 0 ? elements.Length - start : count;
            if (actualCount == 0) return;

            // 1. Rent working buffers to protect the caller's original arrays
            var workingPos = System.Buffers.ArrayPool<Vector2>.Shared.Rent(actualCount);
            var workingIds = System.Buffers.ArrayPool<int>.Shared.Rent(actualCount);

            try
            {
                // 2. Safely copy the data
                System.Array.Copy(elements, start, workingPos, 0, actualCount);
                System.Array.Copy(elementsIDs, start, workingIds, 0, actualCount);

                // 3. Partition the working buffers safely
                InsertRange_Recursive(this, workingPos, workingIds, 0, actualCount);
            }
            finally
            {
                // 4. Always return to the pool
                System.Buffers.ArrayPool<Vector2>.Shared.Return(workingPos);
                System.Buffers.ArrayPool<int>.Shared.Return(workingIds);
            }
        }

        /// <summary>
        /// Recursive bulk-insert. If the node is a leaf that can absorb the batch it writes
        /// directly; otherwise it partitions the batch by Y, then X (matching the child bit
        /// pattern used by <see cref="Subdivide"/>) and recurses into each quadrant.
        /// </summary>
        /// <param name="node">Current node being populated.</param>
        /// <param name="elements">Source array of positions.</param>
        /// <param name="elementsIDs">Source array of element IDs, parallel to <paramref name="elements"/>.</param>
        /// <param name="start">Inclusive start index of the current slice.</param>
        /// <param name="count">Number of elements in the current slice.</param>
        private static void InsertRange_Recursive(Quadtree node,
            Vector2[] elements, int[] elementsIDs, int start, int count)
        {
            if (count == 0) return;

            // 1. If this node is a leaf, check if we can absorb the elements
            if (!node.HasChildren)
            {
                // If they fit, or we've hit the depth limit, add them directly
                if (node._elementsCount + count <= node._maxElementsCountPerNode || node._currentDepth == node._maxDepth)
                {
                    // At max depth the array may overflow — grow it in-place via ref
                    if (node._elementsCount + count > node._maxElementsCountPerNode)
                        ArrayExtensions.EnsureCapacity(ref node._elements, node._elementsCount + count);

                    for (int i = 0; i < count; i++)
                    {
                        node._elements[node._elementsCount] = new(elementsIDs[start + i], elements[start + i]);
                        node._elementsCount++;
                    }
                    return;
                }

                // Otherwise, subdivide and push EXISTING elements down
                Subdivide(node);

                for (int elementIndex = 0; elementIndex < node._elementsCount; elementIndex++)
                {
                    var element = node._elements[elementIndex];
                    InsertIntoChildren(node, element.Position, element.ElementID);
                }

                node._elementsCount = 0;
            }

            // 2. The node now has children. Partition the incoming batch in-place.
            // Step A: Partition by Y (Bit 1) -> Splits into Y- (children 0&1) and Y+ (children 2&3)
            float midX = (node._boundary.MinX + node._boundary.MaxX) * 0.5f;
            float midY = (node._boundary.MinY + node._boundary.MaxY) * 0.5f;

            int midYIdx = PartitionByAxis(elements, elementsIDs, start, count, midY, 1);
            int countY0 = midYIdx - start;
            int countY1 = count - countY0;

            // Step B: Partition by X (Bit 0) -> Yields the final 4 blocks
            // Children bit pattern: bit0=X (0=left/-, 1=right/+), bit1=Y (0=bottom/-, 1=top/+)
            //   child 0: X-, Y-  (SW)
            //   child 1: X+, Y-  (SE)
            //   child 2: X-, Y+  (NW)
            //   child 3: X+, Y+  (NE)
            System.Span<int> starts = stackalloc int[_MAX_CHILDREN_COUNT];
            System.Span<int> counts = stackalloc int[_MAX_CHILDREN_COUNT];

            // Block Y- (children 0 & 1)
            starts[0] = start;
            starts[1] = PartitionByAxis(elements, elementsIDs, starts[0], countY0, midX, 0);
            counts[0] = starts[1] - starts[0];
            counts[1] = countY0 - counts[0];

            // Block Y+ (children 2 & 3)
            starts[2] = midYIdx;
            starts[3] = PartitionByAxis(elements, elementsIDs, starts[2], countY1, midX, 0);
            counts[2] = starts[3] - starts[2];
            counts[3] = countY1 - counts[2];

            // 3. Recurse into the children with their respective partitioned ranges
            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
            {
                if (counts[i] > 0)
                    InsertRange_Recursive(node._children[i], elements, elementsIDs, starts[i], counts[i]);
            }
        }

        /// <summary>
        /// Two-pointer in-place partition of <c>elements[start .. start+count-1]</c> around
        /// <paramref name="threshold"/> on the given <paramref name="axis"/>.
        /// Elements below the threshold are moved to the left half; elements at or above to the right.
        /// </summary>
        /// <param name="elements">Array of positions to partition in-place.</param>
        /// <param name="elementsIDs">Array of IDs parallel to <paramref name="elements"/>; swapped in sync to maintain alignment.</param>
        /// <param name="start">Inclusive start of the slice.</param>
        /// <param name="count">Length of the slice.</param>
        /// <param name="threshold">Splitting value (the node centre on this axis).</param>
        /// <param name="axis">0 = X, 1 = Y.</param>
        /// <returns>The index of the first element in the right-hand (≥ threshold) partition.</returns>
        private static int PartitionByAxis(Vector2[] elements, int[] elementsIDs, int start, int count, float threshold, int axis)
        {
            int left  = start;
            int right = start + count - 1;

            while (left <= right)
            {
                float val = axis == 0 ? elements[left].x : elements[left].y;

                if (val < threshold)
                {
                    left++;
                }
                else
                {
                    // Swap both position and ID together to keep them in sync
                    (elements[left], elements[right])    = (elements[right], elements[left]);
                    (elementsIDs[left], elementsIDs[right]) = (elementsIDs[right], elementsIDs[left]);
                    right--;
                }
            }

            return left;
        }
#endregion Multiple Insert

        /// <summary>
        /// Computes the child quadrant index for <paramref name="position"/> using the same bit
        /// pattern as <see cref="Subdivide"/> (bit0=X, bit1=Y) and forwards the insert directly
        /// to that child, eliminating up to 3 redundant boundary checks.
        /// </summary>
        /// <param name="parent">The subdivided parent node.</param>
        /// <param name="position">Position to insert.</param>
        /// <param name="elementID">Caller-supplied identifier.</param>
        /// <returns><see langword="true"/> if the element was successfully inserted.</returns>
        private static bool InsertIntoChildren(Quadtree parent, Vector2 position, int elementID)
        {
            // A point belongs to exactly one quadrant — compute its index directly
            // from the position relative to the parent center using the same bit pattern
            // as Subdivide(): bit0=X, bit1=Y
            float midX = (parent._boundary.MinX + parent._boundary.MaxX) * 0.5f;
            float midY = (parent._boundary.MinY + parent._boundary.MaxY) * 0.5f;
            int childIndex = 0;
            if (position.x >= midX) childIndex |= 1;
            if (position.y >= midY) childIndex |= 2;
            return Insert_Internal(parent._children[childIndex], position, elementID);
        }

        /// <summary>
        /// Splits <paramref name="node"/> into 4 child quadrants by slicing its <see cref="AABB"/>
        /// at the midpoint on each axis. Child boundaries are computed from parent min/max directly
        /// to avoid floating-point drift at deep levels.
        /// Child indices follow the bit pattern: bit0=X, bit1=Y (0=negative, 1=positive half):
        /// 0=SW, 1=SE, 2=NW, 3=NE.
        /// </summary>
        /// <param name="node">The leaf node to subdivide.</param>
        private static void Subdivide(Quadtree node)
        {
            float midX = (node._boundary.MinX + node._boundary.MaxX) * 0.5f;
            float midY = (node._boundary.MinY + node._boundary.MaxY) * 0.5f;

            for (int childIndex = 0; childIndex < _MAX_CHILDREN_COUNT; childIndex++)
            {
                float minX = (childIndex & 1) == 0 ? node._boundary.MinX : midX;
                float maxX = (childIndex & 1) == 0 ? midX : node._boundary.MaxX;
                float minY = (childIndex & 2) == 0 ? node._boundary.MinY : midY;
                float maxY = (childIndex & 2) == 0 ? midY : node._boundary.MaxY;

                node._children[childIndex] = node._quadtreePool.RequestQuadtree();
                node._children[childIndex].Initialize(new AABB(minX, minY, maxX, maxY), node._currentDepth + 1);
            }
        }

        /// <summary>
        /// Resets a pooled node to a clean state for reuse.
        /// Called by <see cref="Subdivide"/> via the pool after requesting a node.
        /// </summary>
        /// <param name="boundary">New axis-aligned 2-D boundary for this node.</param>
        /// <param name="currentDepth">Depth of this node within the tree.</param>
        private void Initialize(AABB boundary, int currentDepth)
        {
            _boundary = boundary;
            _currentDepth = currentDepth;

            // Reset fields as we are pooling nodes
            _elementsCount = 0;
            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
                _children[i] = null;
        }
#endregion Insert Logic

#region Element Removal Logic
#region Single Remove
        /// <inheritdoc/>
        public void Remove(Vector2 element, int elementID)
        {
            Remove_Internal(this, element, elementID);
        }

        /// <summary>
        /// Recursive single-element removal.
        /// Uses O(1) swap-with-last deletion on leaf arrays to avoid shifting.
        /// After a successful removal checks whether the parent should merge its now-empty children.
        /// </summary>
        /// <param name="node">Current node to search.</param>
        /// <param name="position">Position of the element to remove, used for boundary pruning.</param>
        /// <param name="elementID">Identifier of the element to remove.</param>
        /// <returns><see langword="true"/> if the element was found and removed.</returns>
        private static bool Remove_Internal(Quadtree node, Vector2 position, int elementID)
        {
            if (!node._boundary.Contains(position))
                return false;

            if (node.HasChildren)
            {
                for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
                {
                    if (!Remove_Internal(node._children[i], position, elementID))
                        continue;

                    // After removal, check if the parent node should merge its children back
                    if (ShouldMerge(node))
                    {
                        for (int childIndex = 0; childIndex < _MAX_CHILDREN_COUNT; childIndex++)
                        {
                            node._quadtreePool.ReleaseQuadtree(node._children[childIndex]);
                            node._children[childIndex] = null;
                        }
                    }
                    return true;
                }
            }
            else
            {
                int elementCount = node._elementsCount;
                for (int elementIndex = 0; elementIndex < elementCount; elementIndex++)
                {
                    if (node._elements[elementIndex].ElementID == elementID)
                    {
                        // O(1) removal: swap with last, remove last
                        node._elements[elementIndex] = node._elements[elementCount - 1];
                        node._elementsCount--;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> if all children of <paramref name="node"/> are empty
        /// leaves, meaning the node can safely collapse back into a single leaf.
        /// </summary>
        /// <param name="node">The subdivided node to check.</param>
        /// <returns><see langword="true"/> if all children have no elements and no grandchildren.</returns>
        private static bool ShouldMerge(Quadtree node)
        {
            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
            {
                var child = node._children[i];
                if (child != null && (child.HasChildren || child._elementsCount > 0))
                    return false;
            }
            return true;
        }
#endregion Single Remove

#region Multiple Remove
        /// <inheritdoc/>
        public void RemoveAll()
        {
            if (HasChildren)
            {
                for (int childIndex = 0; childIndex < _MAX_CHILDREN_COUNT; childIndex++)
                {
                    _children[childIndex].RemoveAll();
                    _quadtreePool.ReleaseQuadtree(_children[childIndex]);
                    _children[childIndex] = null;
                }
            }

            _elementsCount = 0;
        }
#endregion Multiple Remove
#endregion Element Removal Logic

#region Query Logic
        /// <inheritdoc/>
        public bool TryQueryClosest(Vector2 position, out QueryResult result)
        {
            int bestElementID = -1;
            float bestDistanceSq = float.MaxValue;

            bool hasFoundElement = TryQueryClosest_Internal(this, position, ref bestElementID, ref bestDistanceSq);

            if (hasFoundElement)
            {
                result = new(bestElementID, Mathf.Sqrt(bestDistanceSq));
                return true;
            }

            result = default; // Return a clean default if tree is empty
            return false;
        }

        /// <summary>
        /// Recursive nearest-neighbour search.
        /// Prunes subtrees whose boundary is already further than the current best distance.
        /// Visits the quadrant the query point naturally falls into first to tighten the best
        /// distance early and maximise pruning of the remaining children.
        /// </summary>
        /// <param name="node">Current node to search.</param>
        /// <param name="source">Query position.</param>
        /// <param name="bestElementID">ID of the closest element found so far; updated in-place.</param>
        /// <param name="bestDistSq">Squared distance to the closest element found so far; updated in-place.</param>
        /// <returns><see langword="true"/> if a closer element was found in this subtree.</returns>
        private static bool TryQueryClosest_Internal(Quadtree node, Vector2 source,
            ref int bestElementID, ref float bestDistSq)
        {
            if (node._boundary.SqrDistance(source) > bestDistSq)
                return false;

            bool hasFoundElement = false;
            if (node.HasChildren)
            {
                // Heuristic: visit the quadrant the source naturally falls into first
                // to tighten bestDistSq early and prune the remaining children.
                float midX = (node._boundary.MinX + node._boundary.MaxX) * 0.5f;
                float midY = (node._boundary.MinY + node._boundary.MaxY) * 0.5f;
                int closestChildIndex = 0;
                if (source.x >= midX) closestChildIndex |= 1;
                if (source.y >= midY) closestChildIndex |= 2;

                // Visit the closest child FIRST
                if (TryQueryClosest_Internal(node._children[closestChildIndex], source, ref bestElementID, ref bestDistSq))
                    hasFoundElement = true;

                // Then visit the rest
                for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
                {
                    if (i == closestChildIndex) continue;
                    if (TryQueryClosest_Internal(node._children[i], source, ref bestElementID, ref bestDistSq))
                        hasFoundElement = true;
                }
            }
            else
            {
                for (int elementIndex = 0; elementIndex < node._elementsCount; elementIndex++)
                {
                    var element = node._elements[elementIndex];
                    float distSq = Vector2.SqrMagnitude(element.Position - source);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestElementID = element.ElementID;
                        hasFoundElement = true;
                    }
                }
            }

            return hasFoundElement;
        }

        /// <inheritdoc/>
        public int QueryWithinRange_NoAlloc(Vector2 source, float range,
            QueryResult[] results, bool sortResults = true)
        {
            if (results.Length == 0) return 0;

            int foundCount = 0;
            QueryRange_Recursive(this, source, range * range, results, ref foundCount);

            return QueryRangeUtils.FinaliseResults(results, foundCount, sortResults);
        }

        /// <summary>
        /// Recursive range search. Prunes quadrants whose boundary is entirely outside the search
        /// radius, then delegates result management to <see cref="QueryRangeUtils.TryAddResult"/>.
        /// Squared distances are accumulated during traversal; <see cref="QueryRangeUtils.FinaliseResults"/>
        /// converts them and optionally sorts before returning.
        /// </summary>
        /// <param name="node">Current node to search.</param>
        /// <param name="source">Query position.</param>
        /// <param name="sqRange">Squared search radius.</param>
        /// <param name="results">Pre-allocated result buffer (stores squared distances during search).</param>
        /// <param name="foundCount">Running count of candidates found, including those evicted from the buffer.</param>
        private static void QueryRange_Recursive(Quadtree node, Vector2 source,
            float sqRange, QueryResult[] results, ref int foundCount)
        {
            if (node._boundary.SqrDistance(source) > sqRange)
                return;

            if (node.HasChildren)
            {
                for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
                    QueryRange_Recursive(node._children[i], source, sqRange, results, ref foundCount);
            }
            else
            {
                for (int i = 0; i < node._elementsCount; i++)
                {
                    var element = node._elements[i];
                    float distSq = Vector2.SqrMagnitude(element.Position - source);
                    if (distSq <= sqRange)
                        QueryRangeUtils.TryAddResult(results, ref foundCount, element.ElementID, distSq);
                }
            }
        }
#endregion Query Logic

#if UNITY_EDITOR
        /// <inheritdoc/>
        public void OnDrawGizmos()
        {
            // Draw the boundary of the node
            float t = _maxDepth > 0 ? (float)_currentDepth / _maxDepth : 0f;
            Gizmos.color = Color.Lerp(Color.green, Color.red, t);
            Gizmos.DrawLine(new Vector3(_boundary.MinX, 0f, _boundary.MinY), new Vector3(_boundary.MaxX, 0f, _boundary.MinY));
            Gizmos.DrawLine(new Vector3(_boundary.MaxX, 0f, _boundary.MinY), new Vector3(_boundary.MaxX, 0f, _boundary.MaxY));
            Gizmos.DrawLine(new Vector3(_boundary.MaxX, 0f, _boundary.MaxY), new Vector3(_boundary.MinX, 0f, _boundary.MaxY));
            Gizmos.DrawLine(new Vector3(_boundary.MinX, 0f, _boundary.MaxY), new Vector3(_boundary.MinX, 0f, _boundary.MinY));

            // Recursively draw the children
            if (HasChildren)
            {
                for (int childIndex = 0; childIndex < _MAX_CHILDREN_COUNT; childIndex++)
                    _children[childIndex].OnDrawGizmos();
            }
        }
#endif
    }
}