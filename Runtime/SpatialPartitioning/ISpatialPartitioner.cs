using System.Collections.Generic;
using UnityEngine;

namespace UnityTechnologies.CodeUtils.SpatialPartionning
{
    /// <summary>
    /// Represents a single result from a spatial query, pairing an element ID with its distance
    /// from the query point.
    /// </summary>
    public readonly struct QueryResult
    {
        /// <summary>The external ID of the found element, as supplied during insertion.</summary>
        public readonly int ElementID;

        /// <summary>
        /// Distance from the query point to this element.
        /// Holds a <b>squared</b> distance internally during an active search;
        /// converted to actual distance by <see cref="QueryRangeUtils.FinaliseResults"/> before being returned to callers.
        /// </summary>
        public readonly float Distance;

        /// <param name="elementID">External ID of the element. Defaults to <see cref="int.MinValue"/> (invalid).</param>
        /// <param name="distance">Distance (or squared distance during search). Defaults to <see cref="Mathf.Infinity"/>.</param>
        public QueryResult(int elementID = int.MinValue, float distance = Mathf.Infinity)
        {
            ElementID = elementID;
            Distance = distance;
        }
    }

    /// <summary>
    /// Common interface for all spatial partitioners (KD-Tree, Quadtree, Octree, …).
    /// <para>
    /// Using a generic interface allows transparent swapping between 2D and 3D implementations
    /// without changing call sites.
    /// </para>
    /// </summary>
    /// <typeparam name="TData">
    /// The position type used by this partitioner (e.g. <see cref="UnityEngine.Vector2"/> for 2D,
    /// <see cref="UnityEngine.Vector3"/> for 3D).
    /// </typeparam>
    public interface ISpatialPartitioner<TData> : System.IDisposable
        where TData : struct
    {
        /// <summary>
        /// Inserts a single element into the partitioner.
        /// </summary>
        /// <param name="element">World-space position of the element.</param>
        /// <param name="elementID">Caller-supplied identifier used to retrieve the element from query results.</param>
        void Insert(TData element, int elementID);

        /// <summary>
        /// Bulk-inserts a slice of elements into the partitioner.
        /// </summary>
        /// <param name="elements">Source array of position.</param>
        /// <param name="elementsIDs">Source array of IDs. Needs to match elements length.</param>
        /// <param name="start">At which index should the insert start on both arrays</param>
        /// <param name="count">How many elements should be inserted. Keep to -1 for all elements.</param>
        void InsertRange(TData[] elements, int[] elementsIDs, int start = 0, int count = -1);

        /// <summary>
        /// Removes a single element from the partitioner.
        /// </summary>
        /// <param name="element">World-space position of the element to remove.</param>
        /// <param name="elementID">Identifier of the element to remove.</param>
        void Remove(TData element, int elementID);

        /// <summary>Removes all elements from the partitioner, resetting it to an empty state.</summary>
        void RemoveAll();

        /// <summary>
        /// Rebuilds the internal data structure to restore optimal balance and reclaim memory
        /// from fragmented or deleted node slots.
        /// <para>
        /// Call this periodically after many <see cref="Remove"/> operations, or after mixing
        /// <see cref="Insert"/> calls into a tree originally built via <see cref="InsertRange"/>,
        /// to restore O(log n) query performance.
        /// </para>
        /// </summary>
        void Rebuild();

        /// <summary>
        /// Finds the single closest element to <paramref name="element"/>.
        /// </summary>
        /// <param name="element">Query position in world space.</param>
        /// <param name="result">
        /// When this method returns <see langword="true"/>, contains the closest element and its distance.
        /// When <see langword="false"/>, the value is undefined.
        /// </param>
        /// <returns><see langword="true"/> if at least one element exists in the partitioner; otherwise <see langword="false"/>.</returns>
        bool TryQueryClosest(TData element, out QueryResult result);

        /// <summary>
        /// Finds all elements within <paramref name="range"/> of <paramref name="source"/>, writing
        /// up to <c>results.Length</c> results into the caller-supplied array without any heap allocation.
        /// </summary>
        /// <param name="source">Query position in world space.</param>
        /// <param name="range">Search radius in world units.</param>
        /// <param name="results">
        /// Pre-allocated array to receive results. Its length caps the number of results returned.
        /// If more elements are found than the array can hold, only the closest ones are kept.
        /// </param>
        /// <param name="sortResults">
        /// When <see langword="true"/> (default), results are sorted from closest to furthest before returning.
        /// Pass <see langword="false"/> to skip sorting for a minor performance gain when order is irrelevant.
        /// </param>
        /// <returns>The number of results written into <paramref name="results"/>.</returns>
        int QueryWithinRange_NoAlloc(TData source, float range, QueryResult[] results, bool sortResults = true);

#if UNITY_EDITOR
        /// <summary>Draws a debug visualisation of the partitioner's internal structure using <see cref="Gizmos"/>.</summary>
        void OnDrawGizmos();
#endif
    }
}