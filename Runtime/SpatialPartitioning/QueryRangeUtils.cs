using UnityEngine;

namespace CodeUtils.SpatialPartitioning
{
    /// <summary>
    /// Shared utilities for no-alloc range query result management across all spatial partitioners.
    /// <para>
    /// During a search, results are stored with <b>squared</b> distances so that costly
    /// <see cref="Mathf.Sqrt"/> calls are deferred to a single final pass.
    /// The array is maintained as a <b>max-heap</b> once full, so the furthest result is always
    /// at index 0 and can be evicted in O(log n) when a closer element is found.
    /// </para>
    /// </summary>
    internal static class QueryRangeUtils
    {
        /// <summary>
        /// Attempts to add a candidate to the result buffer.
        /// <para>
        /// While the buffer has space, elements are appended directly and the buffer is converted
        /// to a max-heap the moment it becomes full.
        /// Once full, the candidate replaces the current furthest element (index 0) only if it is
        /// closer, then the heap is restored.
        /// </para>
        /// </summary>
        /// <param name="results">The result buffer. Stores squared distances during search.</param>
        /// <param name="foundCount">Running total of candidates seen, including those discarded.</param>
        /// <param name="elementID">ID of the candidate element.</param>
        /// <param name="distSq">Squared distance from the query point to the candidate.</param>
        internal static void TryAddResult(QueryResult[] results, ref int foundCount, int elementID, float distSq)
        {
            if (foundCount < results.Length)
            {
                // Buffer not yet full — append and build heap when it fills
                results[foundCount] = new QueryResult(elementID, distSq);
                foundCount++;

                if (foundCount == results.Length)
                {
                    // Convert to max-heap so results[0] is always the furthest
                    for (int j = results.Length / 2 - 1; j >= 0; j--)
                        MaxHeapify(results, j, results.Length);
                }
            }
            else
            {
                foundCount++; // Keep counting even if discarded, so callers know the true total

                // Buffer full — only replace if closer than the current furthest (index 0)
                if (distSq < results[0].Distance)
                {
                    results[0] = new QueryResult(elementID, distSq);
                    MaxHeapify(results, 0, results.Length);
                }
            }
        }

        /// <summary>
        /// Finalises the result buffer after the search is complete:
        /// optionally heap-sorts from closest to furthest, then converts all squared
        /// distances to actual distances via a single <see cref="Mathf.Sqrt"/> pass.
        /// </summary>
        /// <param name="results">The result buffer filled during search.</param>
        /// <param name="foundCount">Total candidates seen (may exceed results.Length).</param>
        /// <param name="shouldSort">If true, results are sorted closest-first.</param>
        /// <returns>The number of results written into <paramref name="results"/>.</returns>
        internal static int FinaliseResults(QueryResult[] results, int foundCount, bool shouldSort)
        {
            int actualCount = Mathf.Min(foundCount, results.Length);
            if (actualCount == 0) return 0;

            if (shouldSort && actualCount > 1)
            {
                // Build heap first if the buffer was never completely filled during the search
                if (foundCount < results.Length)
                {
                    for (int j = actualCount / 2 - 1; j >= 0; j--)
                        MaxHeapify(results, j, actualCount);
                }

                // In-place heap sort: repeatedly swap the max (furthest) to the end
                for (int i = actualCount - 1; i > 0; i--)
                {
                    (results[0], results[i]) = (results[i], results[0]);
                    MaxHeapify(results, 0, i);
                }
            }

            // Single Sqrt pass — deferred from the search loop to minimise total Sqrt calls
            for (int i = 0; i < actualCount; i++)
                results[i] = new QueryResult(results[i].ElementID, Mathf.Sqrt(results[i].Distance));

            return actualCount;
        }

        /// <summary>
        /// Restores the max-heap property for the sub-tree rooted at <paramref name="index"/>.
        /// Heap is keyed on <see cref="QueryResult.Distance"/> (which holds squared distance during search).
        /// </summary>
        private static void MaxHeapify(QueryResult[] arr, int index, int heapSize)
        {
            int largest = index;

            while (true)
            {
                int left  = 2 * index + 1;
                int right = 2 * index + 2;

                if (left  < heapSize && arr[left].Distance  > arr[largest].Distance) largest = left;
                if (right < heapSize && arr[right].Distance > arr[largest].Distance) largest = right;

                if (largest != index)
                {
                    (arr[index], arr[largest]) = (arr[largest], arr[index]);
                    index = largest;
                }
                else break;
            }
        }
    }
}

