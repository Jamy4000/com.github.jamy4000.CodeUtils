using System.Collections.Generic;
using UnityEngine;

namespace UnityTechnologies.CodeUtils
{
    /// <summary>
    /// Helper class for type safety and to enable some generic templating.
    /// </summary>
    public class IntervalUpdater1Hz<T> : IntervalUpdater<T> where T : class
    {
        public IntervalUpdater1Hz() : base(1) { }
    }

    /// <summary>
    /// Helper class for type safety and to enable some generic templating.
    /// </summary>
    public class IntervalUpdater3Hz<T> : IntervalUpdater<T> where T : class
    {
        public IntervalUpdater3Hz() : base(3) { }
    }

    /// <summary>
    /// An updater that iterates over small portions of its list each Time-slice.
    /// </summary>
    public class IntervalUpdater<T> : GameUpdateScheduler.IUpdater<T>
    {
        public T Current => _members[_cursor];
        public int Count => _members.Count;

        private readonly List<T> _members;
        private readonly float _timeSpan;

        private int _cursor;
        private int _cursorAtStart;

        private float _accumulator;
        private float _accumulatorAtStart;

        private float _interval;

        //If you are using this as a container for structs then be sure to override the == and != 
        //or else you will cause boxing/unboxing and a whole load of other unknown issues
        public IntervalUpdater(int frequency, int initialCapacity = 25)
        {
            Debug.Assert(frequency > 0.0f);

            _members = new List<T>(initialCapacity);
            _timeSpan = 1.0f / frequency;

            _cursorAtStart = _cursor = 0;
            _accumulatorAtStart = _accumulator = 0.0f;
            UpdateInterval();
        }

        public void PrepareUpdatePass(float time)
        {
            AddTime(time);

            _accumulatorAtStart = _accumulator;
            _cursorAtStart = _cursor;
        }

        public void PrepareRepeatUpdatePass()
        {
            _accumulator = _accumulatorAtStart;
            _cursor = _cursorAtStart;
        }

        public void Add(T item)
        {
            if (!_members.Contains(item))
            {
                AddUnchecked(item);
            }
        }

        public void AddUnchecked(T item)
        {
            _members.Add(item);
            UpdateInterval();
        }

        public bool Remove(T item)
        {
            if (!_members.TryFindIndex(item, out int found))
            {
                return false;
            }

            // TODO PERF: This is linear. We could use an in-place free list, or some sort of
            // array-backed linked list to avoid a lot of shifting here, but after doing some 
            // testing I couldn't get an implementation that consistently outperformed naive 
            // O(n) array-shifting here, especially for small arrays. The benefit of this 
            // very naive approach is that adding is fast, which we do just as often. We also
            // don't want to swap-and-pop here because doing so might cause us to skip or even
            // starve a given element in the round-robin process. This is deceptively tricky.
            // Given that Array.Copy is VERY optimized, I think it's the best option here.
            _members.RemoveAt(found);

            // Avoid skipping an element if we remove from earlier in the list
            if (found <= _cursor)
            {
                --_cursor; // This may set the cursor back to -1, but that's okay
            }

            if (found <= _cursorAtStart)
            {
                if (--_cursorAtStart < 0)
                {
                    _cursorAtStart += _members.Count;
                }
            }

            UpdateInterval();
            return true;
        }

        public void Clear()
        {
            _members.Clear();
            UpdateInterval();
        }

        public void AddTime(float time)
        {
            // We need to cap the Time added to the maximum Time span 
            // to avoid updating any member element more than once
            _accumulator = Mathf.Min(_accumulator + time, _timeSpan);
        }

        /// <summary>
        /// Moves the list cursor to the next element of the list.
        /// The IntervalUpdater's list has no "end" and will constantly
        /// loop as long as there is Time left in the accumulator.
        /// This process does not need to be explicitly started.
        /// </summary>
        public bool MoveNext()
        {
            // Even if the list is empty we still want to consume the 
            // accumulator Time to avoid a Time pile-up and spike later
            if (_accumulator.Consume(_interval))
            {
                ++_cursor;
                if (_cursor >= _members.Count)
                    _cursor = 0;

                // We need to make sure we won't OOB on an empty list
                return _cursor < _members.Count;
            }

            return false;
        }

        private void UpdateInterval()
        {
            if (_members.Count == 0)
                _interval = float.PositiveInfinity;
            else
                _interval = _timeSpan / _members.Count;
        }
    }
}
