using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UnityTechnologies.CodeUtils
{
    public static class SpanExtensions
    {
        /// <summary>
        /// Performs an in-place introsort (quicksort + heapsort fallback) on the span.
        /// Avoids allocations entirely.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T>(this Span<T> span) where T : IComparable<T>
        {
            if (span.Length <= 1) return;
            IntroSort(span, 0, span.Length - 1, 2 * FloorLog2(span.Length));
        }

        /// <summary>
        /// Performs an in-place introsort using a custom comparer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T>(this Span<T> span, Comparison<T> comparison)
        {
            if (span.Length <= 1) return;
            IntroSort(span, 0, span.Length - 1, 2 * FloorLog2(span.Length), comparison);
        }

        /// <summary>
        /// Returns true if the span contains the given value.
        /// Uses SIMD-friendly sequential search.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            return span.IndexOf(value) >= 0;
        }

        /// <summary>
        /// Fills the span using a factory delegate, avoiding per-element boxing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(this Span<T> span, Func<int, T> factory)
        {
            ref T start = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < span.Length; i++)
                Unsafe.Add(ref start, i) = factory(i);
        }

        /// <summary>
        /// Returns the index of the minimum element. Returns -1 if empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfMin<T>(this ReadOnlySpan<T> span) where T : IComparable<T>
        {
            if (span.IsEmpty) return -1;
            int minIndex = 0;
            ref T start = ref MemoryMarshal.GetReference(span);
            for (int i = 1; i < span.Length; i++)
            {
                if (Unsafe.Add(ref start, i).CompareTo(Unsafe.Add(ref start, minIndex)) < 0)
                    minIndex = i;
            }
            return minIndex;
        }

        /// <summary>
        /// Returns the index of the maximum element. Returns -1 if empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfMax<T>(this ReadOnlySpan<T> span) where T : IComparable<T>
        {
            if (span.IsEmpty) return -1;
            int maxIndex = 0;
            ref T start = ref MemoryMarshal.GetReference(span);
            for (int i = 1; i < span.Length; i++)
            {
                if (Unsafe.Add(ref start, i).CompareTo(Unsafe.Add(ref start, maxIndex)) > 0)
                    maxIndex = i;
            }
            return maxIndex;
        }

        /// <summary>
        /// Reverses the span in-place using direct memory swaps.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reverse<T>(this Span<T> span)
        {
            int left = 0, right = span.Length - 1;
            ref T start = ref MemoryMarshal.GetReference(span);
            while (left < right)
            {
                T temp = Unsafe.Add(ref start, left);
                Unsafe.Add(ref start, left) = Unsafe.Add(ref start, right);
                Unsafe.Add(ref start, right) = temp;
                left++;
                right--;
            }
        }

        /// <summary>
        /// Binary search on a sorted span. Returns index or -1 if not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ReadOnlySpan<T> span, T value) where T : IComparable<T>
        {
            int lo = 0, hi = span.Length - 1;
            ref T start = ref MemoryMarshal.GetReference(span);
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                int cmp = Unsafe.Add(ref start, mid).CompareTo(value);
                if (cmp == 0) return mid;
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return -1;
        }

        /// <summary>
        /// Counts occurrences of a value in the span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Count<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            int count = 0;
            ref T start = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < span.Length; i++)
            {
                if (Unsafe.Add(ref start, i).Equals(value))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Swaps two elements in a span by index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this Span<T> span, int indexA, int indexB)
        {
            ref T start = ref MemoryMarshal.GetReference(span);
            T temp = Unsafe.Add(ref start, indexA);
            Unsafe.Add(ref start, indexA) = Unsafe.Add(ref start, indexB);
            Unsafe.Add(ref start, indexB) = temp;
        }

        /// <summary>
        /// Returns true if the span is sorted in ascending order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSorted<T>(this ReadOnlySpan<T> span) where T : IComparable<T>
        {
            ref T start = ref MemoryMarshal.GetReference(span);
            for (int i = 1; i < span.Length; i++)
            {
                if (Unsafe.Add(ref start, i).CompareTo(Unsafe.Add(ref start, i - 1)) < 0)
                    return false;
            }
            return true;
        }

        #region Introsort Internals

        private static void IntroSort<T>(Span<T> span, int lo, int hi, int depthLimit) where T : IComparable<T>
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= 16)
                {
                    InsertionSort(span, lo, hi);
                    return;
                }
                if (depthLimit == 0)
                {
                    HeapSort(span, lo, hi);
                    return;
                }
                depthLimit--;
                int pivot = Partition(span, lo, hi);
                IntroSort(span, pivot + 1, hi, depthLimit);
                hi = pivot - 1;
            }
        }

        private static void IntroSort<T>(Span<T> span, int lo, int hi, int depthLimit, Comparison<T> comparison)
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= 16)
                {
                    InsertionSort(span, lo, hi, comparison);
                    return;
                }
                if (depthLimit == 0)
                {
                    HeapSort(span, lo, hi, comparison);
                    return;
                }
                depthLimit--;
                int pivot = Partition(span, lo, hi, comparison);
                IntroSort(span, pivot + 1, hi, depthLimit, comparison);
                hi = pivot - 1;
            }
        }

        private static int Partition<T>(Span<T> span, int lo, int hi) where T : IComparable<T>
        {
            // Median-of-three pivot selection
            int mid = lo + ((hi - lo) >> 1);
            if (span[mid].CompareTo(span[lo]) < 0) Swap(span, mid, lo);
            if (span[hi].CompareTo(span[lo]) < 0) Swap(span, hi, lo);
            if (span[mid].CompareTo(span[hi]) < 0) Swap(span, mid, hi);

            T pivot = span[hi];
            int i = lo - 1;
            for (int j = lo; j < hi; j++)
            {
                if (span[j].CompareTo(pivot) <= 0)
                {
                    i++;
                    Swap(span, i, j);
                }
            }
            Swap(span, i + 1, hi);
            return i + 1;
        }

        private static int Partition<T>(Span<T> span, int lo, int hi, Comparison<T> comparison)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (comparison(span[mid], span[lo]) < 0) Swap(span, mid, lo);
            if (comparison(span[hi], span[lo]) < 0) Swap(span, hi, lo);
            if (comparison(span[mid], span[hi]) < 0) Swap(span, mid, hi);

            T pivot = span[hi];
            int i = lo - 1;
            for (int j = lo; j < hi; j++)
            {
                if (comparison(span[j], pivot) <= 0)
                {
                    i++;
                    Swap(span, i, j);
                }
            }
            Swap(span, i + 1, hi);
            return i + 1;
        }

        private static void InsertionSort<T>(Span<T> span, int lo, int hi) where T : IComparable<T>
        {
            for (int i = lo + 1; i <= hi; i++)
            {
                T key = span[i];
                int j = i - 1;
                while (j >= lo && span[j].CompareTo(key) > 0)
                {
                    span[j + 1] = span[j];
                    j--;
                }
                span[j + 1] = key;
            }
        }

        private static void InsertionSort<T>(Span<T> span, int lo, int hi, Comparison<T> comparison)
        {
            for (int i = lo + 1; i <= hi; i++)
            {
                T key = span[i];
                int j = i - 1;
                while (j >= lo && comparison(span[j], key) > 0)
                {
                    span[j + 1] = span[j];
                    j--;
                }
                span[j + 1] = key;
            }
        }

        private static void HeapSort<T>(Span<T> span, int lo, int hi) where T : IComparable<T>
        {
            int n = hi - lo + 1;
            for (int i = n / 2 - 1; i >= 0; i--)
                Heapify(span, n, i, lo);
            for (int i = n - 1; i > 0; i--)
            {
                Swap(span, lo, lo + i);
                Heapify(span, i, 0, lo);
            }
        }

        private static void Heapify<T>(Span<T> span, int n, int i, int lo) where T : IComparable<T>
        {
            int largest = i, left = 2 * i + 1, right = 2 * i + 2;
            if (left < n && span[lo + left].CompareTo(span[lo + largest]) > 0) largest = left;
            if (right < n && span[lo + right].CompareTo(span[lo + largest]) > 0) largest = right;
            if (largest != i)
            {
                Swap(span, lo + i, lo + largest);
                Heapify(span, n, largest, lo);
            }
        }

        private static void HeapSort<T>(Span<T> span, int lo, int hi, Comparison<T> comparison)
        {
            int n = hi - lo + 1;
            for (int i = n / 2 - 1; i >= 0; i--)
                Heapify(span, n, i, lo, comparison);
            for (int i = n - 1; i > 0; i--)
            {
                Swap(span, lo, lo + i);
                Heapify(span, i, 0, lo, comparison);
            }
        }

        private static void Heapify<T>(Span<T> span, int n, int i, int lo, Comparison<T> comparison)
        {
            int largest = i, left = 2 * i + 1, right = 2 * i + 2;
            if (left < n && comparison(span[lo + left], span[lo + largest]) > 0) largest = left;
            if (right < n && comparison(span[lo + right], span[lo + largest]) > 0) largest = right;
            if (largest != i)
            {
                Swap(span, lo + i, lo + largest);
                Heapify(span, n, largest, lo, comparison);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorLog2(int n)
        {
            int result = 0;
            while (n > 1) { n >>= 1; result++; }
            return result;
        }

        #endregion
    }
}
