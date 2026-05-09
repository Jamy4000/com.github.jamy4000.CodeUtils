using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CodeUtils
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Checks if there is enough space in an array, and if not, grows it by doubling.
        /// The ref parameter ensures the caller's array field is updated to the new array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacity<T>(ref T[] array, int requiredCapacity)
        {
            if (array.Length >= requiredCapacity)
                return;

            // Double the size, or jump straight to the required capacity if doubling isn't enough
            int newCapacity = Math.Max(array.Length * 2, requiredCapacity);
            Array.Resize(ref array, newCapacity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EraseWithLastSwap<T>(this T[] array, int index, ref int count)
        {
            if ((uint)index >= (uint)count)
                throw new ArgumentOutOfRangeException(nameof(index));
            array[index] = array[count - 1];
            array[--count] = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfMin<T>(this T[] array, int count = -1) where T : IComparable<T>
        {
            if (count < 0) count = array.Length;
            if (count == 0) return -1;
            int minIndex = 0;
            ref T start = ref MemoryMarshal.GetReference(array.AsSpan());
            for (int i = 1; i < count; i++)
            {
                if (Unsafe.Add(ref start, i).CompareTo(Unsafe.Add(ref start, minIndex)) < 0)
                    minIndex = i;
            }
            return minIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfMax<T>(this T[] array, int count = -1) where T : IComparable<T>
        {
            if (count < 0) count = array.Length;
            if (count == 0) return -1;
            int maxIndex = 0;
            ref T start = ref MemoryMarshal.GetReference(array.AsSpan());
            for (int i = 1; i < count; i++)
            {
                if (Unsafe.Add(ref start, i).CompareTo(Unsafe.Add(ref start, maxIndex)) > 0)
                    maxIndex = i;
            }
            return maxIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSorted<T>(this T[] array, int count = -1) where T : IComparable<T>
        {
            if (count < 0) count = array.Length;
            ref T start = ref MemoryMarshal.GetReference(array.AsSpan());
            for (int i = 1; i < count; i++)
            {
                if (Unsafe.Add(ref start, i).CompareTo(Unsafe.Add(ref start, i - 1)) < 0)
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains<T>(this T[] array, T value) where T : IEquatable<T>
            => ((ReadOnlySpan<T>)array).IndexOf(value) >= 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Count<T>(this T[] array, T value) where T : IEquatable<T>
        {
            int count = 0;
            ref T start = ref MemoryMarshal.GetReference(array.AsSpan());
            for (int i = 0; i < array.Length; i++)
            {
                if (Unsafe.Add(ref start, i).Equals(value))
                    count++;
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Any<T>(this T[] array, Func<T, bool> predicate)
        {
            foreach (var t in array)
                if (predicate(t)) return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool All<T>(this T[] array, Func<T, bool> predicate)
        {
            foreach (var t in array)
                if (!predicate(t)) return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(this T[] array, Func<int, T> factory)
        {
            ref T start = ref MemoryMarshal.GetReference(array.AsSpan());
            for (int i = 0; i < array.Length; i++)
                Unsafe.Add(ref start, i) = factory(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this T[] array, int indexA, int indexB)
        {
            ref T start = ref MemoryMarshal.GetReference(array.AsSpan());
            ref T a = ref Unsafe.Add(ref start, indexA);
            ref T b = ref Unsafe.Add(ref start, indexB);
            (a, b) = (b, a);
        }

        public static void Shuffle<T>(this T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                array.Swap(n, k);
            }
        }

        public static void Shuffle<T>(this T[] array, Random random)
        {
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = random.Next(0, n + 1);
                array.Swap(n, k);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetRandomElement<T>(this T[] array)
        {
            if (array.Length == 0) throw new InvalidOperationException("Array is empty.");
            return array[UnityEngine.Random.Range(0, array.Length)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetRandomElement<T>(this T[] array, Random random)
        {
            if (array.Length == 0) throw new InvalidOperationException("Array is empty.");
            return array[random.Next(0, array.Length)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this T[] array, int start, int length)
            => MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(array.AsSpan()), start), length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(this T[] array, Span<T> destination)
            => ((ReadOnlySpan<T>)array).CopyTo(destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContentEquals<T>(this T[] array, T[] other) where T : IEquatable<T>
            => ((ReadOnlySpan<T>)array).SequenceEqual((ReadOnlySpan<T>)other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindIndex<T>(this T[] array, Func<T, bool> predicate)
        {
            for (int i = 0; i < array.Length; i++)
                if (predicate(array[i])) return i;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastIndex<T>(this T[] array, Func<T, bool> predicate)
        {
            for (int i = array.Length - 1; i >= 0; i--)
                if (predicate(array[i])) return i;
            return -1;
        }
    }
}
