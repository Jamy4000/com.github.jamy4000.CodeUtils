using UnityEngine;

namespace UnityTechnologies.CodeUtils.SpatialPartionning
{
    /// <summary>
    /// A 3-D spatial partitioner that recursively subdivides space into 8 axis-aligned octants.
    /// <para>
    /// Each leaf node stores up to <c>maxElementPerNode</c> elements. When a leaf overflows it is
    /// subdivided and its elements are pushed into the appropriate children. Nodes are managed
    /// through an <see cref="OctreePool"/> to avoid per-subdivision heap allocations.
    /// </para>
    /// <para>
    /// Use <see cref="Insert"/> for single insertions and <see cref="InsertRange"/> for bulk
    /// insertion, which partitions elements in-place by octant at every level for improved
    /// cache locality. Query operations are allocation-free.
    /// </para>
    /// </summary>
    public class Octree : ISpatialPartitioner<Vector3>
    {
        private Bounds _boundary;
        
        // cannot be readonly as we may resize it when reaches MaxDepth
        private OctreeElement[] _elements;
        private int _elementsCount = 0;
        private readonly Octree[] _children;
        private readonly OctreePool _octreePool;
        private bool HasChildren => _children[0] != null;

        private readonly int _maxElementsCountPerNode;
        private readonly int _maxDepth;

        private const int _MAX_CHILDREN_COUNT = 8;
        private int _currentDepth = 0;

        /// <summary>
        /// Constructs a new Octree rooted at <paramref name="center"/> with the given <paramref name="size"/>.
        /// </summary>
        /// <param name="center">World-space centre of the root boundary.</param>
        /// <param name="size">Full extents of the root boundary on each axis. All components must be positive.</param>
        /// <param name="pool">
        /// Optional node pool to reuse. When <see langword="null"/>, a new <see cref="OctreePool"/> is created
        /// and owned by this instance.
        /// </param>
        /// <param name="maxDepth">
        /// Maximum subdivision depth. Elements that would overflow a node at this depth are stored
        /// directly, growing the leaf array if needed. Defaults to 8.
        /// </param>
        /// <param name="maxElementPerNode">
        /// Number of elements a leaf can hold before it is subdivided. Defaults to 8.
        /// </param>
        public Octree(Vector3 center, Vector3 size, OctreePool pool = null,
            int maxDepth = 8, int maxElementPerNode = 8)
        {
            Debug.Assert(size is { x: > 0f, y: > 0f } && size.z > 0f, "Size must be greater than zero");

            _maxElementsCountPerNode = maxElementPerNode;
            _maxDepth = maxDepth;
            _octreePool = pool ?? new OctreePool(center, size, CreateOctree);

            _boundary = new Bounds(center, size);
            _elements = new OctreeElement[maxElementPerNode];
            _children = new Octree[_MAX_CHILDREN_COUNT];
        }
        
        /// <summary>
        /// Convenience constructor that builds the tree immediately from the provided arrays.
        /// Equivalent to calling the primary constructor followed by <see cref="InsertRange"/>.
        /// </summary>
        /// <param name="center">World-space centre of the root boundary.</param>
        /// <param name="size">Full extents of the root boundary on each axis. All components must be positive.</param>
        /// <param name="elements">Positions of elements to insert. Must be the same length as <paramref name="elementsIDs"/>.</param>
        /// <param name="elementsIDs">Caller-supplied identifiers. Must be the same length as <paramref name="elements"/>.</param>
        /// <param name="pool">Optional node pool. See primary constructor.</param>
        /// <param name="maxDepth">Maximum subdivision depth. Defaults to 8.</param>
        /// <param name="maxElementsCountPerNode">Leaf capacity before subdivision. Defaults to 8.</param>
        public Octree(Vector3 center, Vector3 size, Vector3[] elements, int[] elementsIDs,
            OctreePool pool = null, int maxDepth = 8, int maxElementsCountPerNode = 8) :
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
            _octreePool.Dispose();
        }

        /// <summary>
        /// Factory method used by <see cref="OctreePool"/> to construct pool instances with
        /// the same depth and capacity settings as this tree.
        /// </summary>
        /// <param name="center">Centre of the new node's boundary.</param>
        /// <param name="size">Size of the new node's boundary.</param>
        /// <returns>A new <see cref="Octree"/> sharing this tree's pool and settings.</returns>
        private Octree CreateOctree(Vector3 center, Vector3 size)
        {
            return new Octree(center, size, _octreePool, _maxDepth, _maxElementsCountPerNode);
        }

#region Rebuild Logic
        /// <summary>
        /// Rebuilds the Octree to defragment memory and trim empty branches.
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

            var positions = System.Buffers.ArrayPool<Vector3>.Shared.Rent(totalElements);
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
                System.Buffers.ArrayPool<Vector3>.Shared.Return(positions);
                System.Buffers.ArrayPool<int>.Shared.Return(ids);
            }
        }

        /// <summary>
        /// Recursively counts all elements stored in leaf nodes of the subtree rooted at <paramref name="node"/>.
        /// </summary>
        /// <param name="node">Root of the subtree to count.</param>
        /// <returns>Total number of elements in the subtree.</returns>
        private static int CountElementsRecursive(Octree node)
        {
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
        private static void ExtractElementsRecursive(Octree node, Vector3[] positions, int[] ids, ref int writeIndex)
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
        public void Insert(Vector3 element, int elementID)
        {
            Insert_Internal(this, element, elementID);
        }
        
        /// <summary>
        /// Recursive single-element insert.
        /// Rejects positions outside the node boundary, delegates to children when subdivided,
        /// and subdivides the node when it overflows at non-maximum depth.
        /// </summary>
        /// <param name="node">The node to insert into.</param>
        /// <param name="position">World-space position to insert.</param>
        /// <param name="elementID">Caller-supplied identifier.</param>
        /// <returns><see langword="true"/> if the element was accepted by this node or one of its children.</returns>
        private static bool Insert_Internal(Octree node, Vector3 position, int elementID)
        {
            // if this is the wrong Octant
            if (!node._boundary.Contains(position))
                return false;

            // If node has children, insert the point into the appropriate Octant
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

            // We have reached max amount of elements in the node, so we divide it in 8 Octants
            Subdivide(node);

            for (int elementIndex = 0; elementIndex < node._elementsCount; elementIndex++)
            {
                var element = node._elements[elementIndex];
                // Move the existing point to the children
                InsertIntoChildren(node, element.Position, element.ElementID);
            }

            // Clear the elements from the parent node as they are now in the children
            node._elementsCount = 0;
            
            return InsertIntoChildren(node, position, elementID);                // Insert the new point into the children
        }
#endregion Single Insert

#region Multiple Insert
        /// <inheritdoc/>
        /// <remarks>
        /// Elements are partitioned in-place by octant at every tree level before recursing,
        /// so spatially adjacent elements are inserted together. No extra allocation is made.
        /// </remarks>
        public void InsertRange(Vector3[] elements, int[] elementsIDs, int start = 0, int count = -1)
        {
            if (elements == null || elements.Length == 0 ||
                elementsIDs == null || elementsIDs.Length != elements.Length)
            {
                throw new System.ArgumentException("Elements and IDs must be non-empty and of the same length.");
            }

            int actualCount = count < 0 ? elements.Length - start : count;
            if (actualCount == 0) return;

            // 1. Rent working buffers to protect the caller's original arrays
            var workingPos = System.Buffers.ArrayPool<Vector3>.Shared.Rent(actualCount);
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
                System.Buffers.ArrayPool<Vector3>.Shared.Return(workingPos);
                System.Buffers.ArrayPool<int>.Shared.Return(workingIds);
            }
        }

        /// <summary>
        /// Recursive bulk-insert. If the node is a leaf that can absorb the batch it writes
        /// directly; otherwise it partitions the batch by Z, then Y, then X (matching the
        /// child bit pattern used by <see cref="Subdivide"/>) and recurses into each octant.
        /// </summary>
        /// <param name="node">Current node being populated.</param>
        /// <param name="elements">Source array of positions.</param>
        /// <param name="elementsIDs">Source array of element IDs, parallel to <paramref name="elements"/>.</param>
        /// <param name="start">Inclusive start index of the current slice.</param>
        /// <param name="count">Number of elements in the current slice.</param>
        private static void InsertRange_Recursive(Octree node,
            Vector3[] elements, int[] elementsIDs, int start, int count)
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
            Vector3 center = node._boundary.center;

            // Step A: Partition by Z (Bit 2) -> Splits into Z- (0..3) and Z+ (4..7)
            int midZ = PartitionByAxis(elements, elementsIDs, start, count, center.z, 2);
            int countZ0 = midZ - start;
            int countZ1 = count - countZ0;

            // Step B: Partition by Y (Bit 1) -> Splits into 4 quarters
            int midY0 = PartitionByAxis(elements, elementsIDs, start, countZ0, center.y, 1);
            int midY1 = PartitionByAxis(elements, elementsIDs, midZ, countZ1, center.y, 1);

            // Step C: Partition by X (Bit 0) -> Yields the final 8 blocks
            System.Span<int> starts = stackalloc int[8];
            System.Span<int> counts = stackalloc int[8];

            // Block 0: Z-, Y- (Children 0 & 1)
            starts[0] = start;
            int len0 = midY0 - start;
            starts[1] = PartitionByAxis(elements, elementsIDs, starts[0], len0, center.x, 0);
            counts[0] = starts[1] - starts[0];
            counts[1] = len0 - counts[0];

            // Block 1: Z-, Y+ (Children 2 & 3)
            starts[2] = midY0;
            int len1 = midZ - midY0;
            starts[3] = PartitionByAxis(elements, elementsIDs, starts[2], len1, center.x, 0);
            counts[2] = starts[3] - starts[2];
            counts[3] = len1 - counts[2];

            // Block 2: Z+, Y- (Children 4 & 5)
            starts[4] = midZ;
            int len2 = midY1 - midZ;
            starts[5] = PartitionByAxis(elements, elementsIDs, starts[4], len2, center.x, 0);
            counts[4] = starts[5] - starts[4];
            counts[5] = len2 - counts[4];

            // Block 3: Z+, Y+ (Children 6 & 7)
            starts[6] = midY1;
            int len3 = (start + count) - midY1;
            starts[7] = PartitionByAxis(elements, elementsIDs, starts[6], len3, center.x, 0);
            counts[6] = starts[7] - starts[6];
            counts[7] = len3 - counts[6];

            // 3. Recurse into the children with their respective partitioned ranges
            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
            {
                if (counts[i] > 0)
                {
                    InsertRange_Recursive(node._children[i], elements, elementsIDs, starts[i], counts[i]);
                }
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
        /// <param name="threshold">Splitting value (typically the node centre on this axis).</param>
        /// <param name="axis">0 = X, 1 = Y, 2 = Z.</param>
        /// <returns>The index of the first element in the right-hand (≥ threshold) partition.</returns>
        private static int PartitionByAxis(Vector3[] elements, int[] elementsIDs, int start, int count, float threshold, int axis)
        {
            int left = start;
            int right = start + count - 1;

            while (left <= right)
            {
                float val = axis switch
                {
                    0 => elements[left].x,
                    1 => elements[left].y,
                    _ => elements[left].z
                };

                if (val < threshold)
                {
                    left++;
                }
                else
                {
                    // Swap both position and ID together to keep them in sync
                    (elements[left], elements[right]) = (elements[right], elements[left]);
                    (elementsIDs[left], elementsIDs[right]) = (elementsIDs[right], elementsIDs[left]);
                    right--;
                }
            }

            return left;
        }
#endregion Multiple Insert

        /// <summary>
        /// Computes the child octant index for <paramref name="position"/> using the same bit
        /// pattern as <see cref="Subdivide"/> (bit0=X, bit1=Y, bit2=Z) and forwards the insert
        /// directly to that child, eliminating up to 7 redundant boundary checks.
        /// </summary>
        /// <param name="parent">The subdivided parent node.</param>
        /// <param name="position">Position to insert.</param>
        /// <param name="elementID">Caller-supplied identifier.</param>
        /// <returns><see langword="true"/> if the element was successfully inserted.</returns>
        private static bool InsertIntoChildren(Octree parent, Vector3 position, int elementID)
        {
            // A point belongs to exactly one octant — compute its index directly
            // from the position relative to the parent center using the same bit pattern
            // as Subdivide(): bit0=X, bit1=Y, bit2=Z
            Vector3 center = parent._boundary.center;
            int childIndex = 0;
            if (position.x >= center.x) childIndex |= 1;
            if (position.y >= center.y) childIndex |= 2;
            if (position.z >= center.z) childIndex |= 4;
            return Insert_Internal(parent._children[childIndex], position, elementID);
        }
        
        /// <summary>
        /// Splits <paramref name="node"/> into 8 child octants by slicing its boundary at the
        /// midpoint on each axis. Child boundaries are computed from the parent min/max directly
        /// (rather than centre ± half-size) to avoid floating-point drift at deep levels.
        /// Child indices follow the bit pattern: bit0=X, bit1=Y, bit2=Z (0=negative, 1=positive half).
        /// </summary>
        /// <param name="node">The leaf node to subdivide.</param>
        private static void Subdivide(Octree node)
        {
            Vector3 min = node._boundary.min;
            Vector3 max = node._boundary.max;
            Vector3 mid = node._boundary.center; // == (min + max) * 0.5f

            for (int childIndex = 0; childIndex < _MAX_CHILDREN_COUNT; childIndex++)
            {
                float minX = (childIndex & 1) == 0 ? min.x : mid.x;
                float maxX = (childIndex & 1) == 0 ? mid.x : max.x;
                float minY = (childIndex & 2) == 0 ? min.y : mid.y;
                float maxY = (childIndex & 2) == 0 ? mid.y : max.y;
                float minZ = (childIndex & 4) == 0 ? min.z : mid.z;
                float maxZ = (childIndex & 4) == 0 ? mid.z : max.z;

                Vector3 childCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
                Vector3 childSize   = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);

                node._children[childIndex] = node._octreePool.RequestOctree();
                node._children[childIndex].Initialize(new Bounds(childCenter, childSize), node._currentDepth + 1);
            }
        }

        /// <summary>
        /// Resets a pooled node to a clean state for reuse.
        /// Called by <see cref="Subdivide"/> via the pool after requesting a node.
        /// </summary>
        /// <param name="boundary">New axis-aligned boundary for this node.</param>
        /// <param name="currentDepth">Depth of this node within the tree.</param>
        private void Initialize(Bounds boundary, int currentDepth)
        {
            _boundary = boundary;
            _currentDepth = currentDepth;
            
            // Reset field as we are Pooling nodes
            _elementsCount = 0;
            for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
                _children[i] = null;
        }
#endregion Insert Logic

#region Element Removal Logic
#region Single Remove
        /// <inheritdoc/>
        public void Remove(Vector3 element, int elementID)
        {
            Remove_Internal(this, element, elementID);
        }
         
        /// <summary>
        /// Recursive single-element removal.
        /// Uses O(1) swap-with-last deletion on leaf arrays to avoid shifting.
        /// After a successful removal, checks whether the parent should merge its now-empty children.
        /// </summary>
        /// <param name="node">Current node to search.</param>
        /// <param name="position">Position of the element to remove, used for boundary pruning.</param>
        /// <param name="elementID">Identifier of the element to remove.</param>
        /// <returns><see langword="true"/> if the element was found and removed.</returns>
        private static bool Remove_Internal(Octree node, Vector3 position, int elementID)
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
                            node._octreePool.ReleaseOctree(node._children[childIndex]);
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
        private static bool ShouldMerge(Octree node)
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
                    if (_children[childIndex] == null)
                        continue;
                    
                    _children[childIndex].RemoveAll();
                    _octreePool.ReleaseOctree(_children[childIndex]);
                    _children[childIndex] = null;
                }
            }

            _elementsCount = 0;
        }
#endregion Multiple Remove
#endregion Element Removal Logic

#region Query Logic
        /// <inheritdoc/>
        public bool TryQueryClosest(Vector3 position, out QueryResult result)
        {
            int bestElementID = -1;
            float bestDistanceSq = float.MaxValue;
            
            bool hasFoundElement = TryQueryClosest_Internal(this, position, ref bestElementID, ref bestDistanceSq);

            result = new(bestElementID, Mathf.Sqrt(bestDistanceSq));
            return hasFoundElement;
        }
        
        /// <summary>
        /// Recursive nearest-neighbour search.
        /// Prunes subtrees whose boundary is already further than the current best distance.
        /// Visits the octant the query point naturally falls into first to tighten the best
        /// distance early and maximise pruning of the remaining children.
        /// </summary>
        /// <param name="node">Current node to search.</param>
        /// <param name="source">Query position.</param>
        /// <param name="bestElementID">ID of the closest element found so far; updated in-place.</param>
        /// <param name="bestDistSq">Squared distance to the closest element found so far; updated in-place.</param>
        /// <returns><see langword="true"/> if a closer element was found in this subtree.</returns>
        private static bool TryQueryClosest_Internal(Octree node, Vector3 source,
            ref int bestElementID, ref float bestDistSq)
        {
            if (node._boundary.SqrDistance(source) > bestDistSq)
                return false;

            bool hasFoundElement = false;
            if (node.HasChildren)
            {
                // Heuristic: Find which child the source point is naturally inside/closest to.
                // We can do a quick check to find the primary octant index:
                Vector3 center = node._boundary.center;
                int closestChildIndex = 0;
                if (source.x >= center.x) closestChildIndex |= 1;
                if (source.y >= center.y) closestChildIndex |= 2;
                if (source.z >= center.z) closestChildIndex |= 4;

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
                // No element when children are present (it's a leaf), check if it's closer
                
                for (int elementIndex = 0; elementIndex < node._elementsCount; elementIndex++)
                {
                    var element = node._elements[elementIndex];
                    float distSq = Vector3.SqrMagnitude(element.Position - source);
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
        public int QueryWithinRange_NoAlloc(Vector3 source, float range,
            QueryResult[] results, bool sortResults = true)
        {
            if (results.Length == 0) return 0;

            int foundCount = 0;
            QueryWithinRange_NoAlloc_Internal(this, source, range * range, results, ref foundCount);

            return QueryRangeUtils.FinaliseResults(results, foundCount, sortResults);
        }
        
        /// <summary>
        /// Recursive range search. Prunes octants whose boundary is entirely outside the search
        /// radius, then delegates result management to <see cref="QueryRangeUtils.TryAddResult"/>.
        /// Squared distances are accumulated during traversal; <see cref="QueryRangeUtils.FinaliseResults"/>
        /// converts them and optionally sorts before returning.
        /// </summary>
        /// <param name="node">Current node to search.</param>
        /// <param name="source">Query position.</param>
        /// <param name="sqRange">Squared search radius.</param>
        /// <param name="results">Pre-allocated result buffer (stores squared distances during search).</param>
        /// <param name="foundCount">Running count of candidates found, including those evicted from the buffer.</param>
        private static void QueryWithinRange_NoAlloc_Internal(Octree node, Vector3 source,
            float sqRange, QueryResult[] results, ref int foundCount)
        {
            if (node._boundary.SqrDistance(source) > sqRange)
                return;

            if (node.HasChildren)
            {
                for (int i = 0; i < _MAX_CHILDREN_COUNT; i++)
                    QueryWithinRange_NoAlloc_Internal(node._children[i], source, sqRange, results, ref foundCount);
            }
            else
            {
                for (int i = 0; i < node._elementsCount; i++)
                {
                    var element = node._elements[i];
                    float distSq = Vector3.SqrMagnitude(element.Position - source);
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
            Gizmos.DrawWireCube(_boundary.center, _boundary.size);
            
            // Recursively draw the children
            if (HasChildren)
            {
                for (int childIndex = 0; childIndex < _MAX_CHILDREN_COUNT; childIndex++)
                {
                    _children[childIndex].OnDrawGizmos();
                }
            }
        }
#endif
    }
}

