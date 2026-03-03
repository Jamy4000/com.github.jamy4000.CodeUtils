using System;
using System.Collections.Generic;

namespace UnityTechnologies.CodeUtils
{
    public static class ListExtensions
    {
        public static void Resize<T>(this List<T> list, int size, T element = default)
        {
            int count = list.Count;
            if (size < count)
            {
                list.RemoveRange(size, count - size);
            }
            else if (size > count)
            {
                if (size > list.Capacity)
                    list.Capacity = size;
                for (int i = count; i < size; i++)
                    list.Add(element);
            }
        }

        public static int EnsureCapacity<T>(this List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
                list.Capacity = capacity;
            return list.Capacity;
        }

        /// <summary>
        /// Returns a random element. List must not be empty.
        /// </summary>
        public static T GetRandomElement<T>(this List<T> list)
        {
            if (list.Count == 0) throw new InvalidOperationException("List is empty.");
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>
        /// Returns a random element using a provided System.Random. List must not be empty.
        /// </summary>
        public static T GetRandomElement<T>(this List<T> list, System.Random random)
        {
            if (list.Count == 0) throw new InvalidOperationException("List is empty.");
            return list[random.Next(0, list.Count)];
        }

        /// <summary>
        /// Shuffles the list in-place using Fisher-Yates with Unity's RNG.
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        /// <summary>
        /// Shuffles the list in-place using Fisher-Yates with a provided System.Random.
        /// </summary>
        public static void Shuffle<T>(this IList<T> list, System.Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public static bool TryFindIndex<T>(this List<T> list, T item, out int foundIndex)
        {
            foundIndex = list.IndexOf(item);
            return foundIndex >= 0;
        }

        /// <summary>
        /// O(1) removal by swapping with the last element. Does not preserve order.
        /// </summary>
        public static void EraseWithLastSwap<T>(this List<T> list, int index)
        {
            if ((uint)index >= (uint)list.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }

        public static bool TryRemoveWithLastSwap<T>(this List<T> list, T item)
        {
            if (list.TryFindIndex(item, out int found))
            {
                list.EraseWithLastSwap(found);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Allocation-free alternative to LINQ Any().
        /// </summary>
        public static bool Any<T>(this List<T> list, Func<T, bool> predicate)
        {
            for (int i = 0; i < list.Count; i++)
                if (predicate(list[i])) return true;
            return false;
        }

        /// <summary>
        /// Allocation-free alternative to LINQ All().
        /// </summary>
        public static bool All<T>(this List<T> list, Func<T, bool> predicate)
        {
            for (int i = 0; i < list.Count; i++)
                if (!predicate(list[i])) return false;
            return true;
        }

        /// <summary>
        /// Returns the first element after sorting by comparer.
        /// WARNING: sorts the list in-place.
        /// </summary>
        public static T SortedFirst<T>(this List<T> list, IComparer<T> comparer)
        {
            if (list.Count == 0) throw new InvalidOperationException("List is empty.");
            list.Sort(comparer);
            return list[0];
        }

        /// <summary>
        /// Returns the last element after sorting by comparer.
        /// WARNING: sorts the list in-place.
        /// </summary>
        public static T SortedLast<T>(this List<T> list, IComparer<T> comparer)
        {
            if (list.Count == 0) throw new InvalidOperationException("List is empty.");
            list.Sort(comparer);
            return list[list.Count - 1];
        }

        /// <summary>
        /// Returns the minimum element without sorting. O(n), no allocation.
        /// </summary>
        public static T FindMin<T>(this List<T> list, IComparer<T> comparer)
        {
            if (list.Count == 0) throw new InvalidOperationException("List is empty.");
            T min = list[0];
            for (int i = 1; i < list.Count; i++)
                if (comparer.Compare(list[i], min) < 0)
                    min = list[i];
            return min;
        }

        /// <summary>
        /// Returns the maximum element without sorting. O(n), no allocation.
        /// </summary>
        public static T FindMax<T>(this List<T> list, IComparer<T> comparer)
        {
            if (list.Count == 0) throw new InvalidOperationException("List is empty.");
            T max = list[0];
            for (int i = 1; i < list.Count; i++)
                if (comparer.Compare(list[i], max) > 0)
                    max = list[i];
            return max;
        }

        /// <summary>
        /// Appends all elements from a ReadOnlySpan without intermediate array allocation.
        /// </summary>
        public static void AddRange<T>(this List<T> list, ReadOnlySpan<T> span)
        {
            if (list.Capacity < list.Count + span.Length)
                list.Capacity = list.Count + span.Length;
            for (int i = 0; i < span.Length; i++)
                list.Add(span[i]);
        }
    }
}
