using System.Collections.Generic;

namespace CodeUtils
{
    /// <summary>
    /// An updater that iterates over its entire list each time-slice.
    /// </summary>
    public class CompleteUpdaterFacade<T> : UpdaterFacade<CompleteUpdater<T>, T>
        where T : class
    {
    }

    public class CompleteUpdater<T> : GameUpdateScheduler.IUpdater<T> where T : class
    {
        public T Current => _members[_cursor];
        public int Count => _members.Count;

        private readonly List<T> _members = new List<T>();

        private int _cursor = -1;

        void GameUpdateScheduler.IUpdater<T>.PrepareUpdatePass(float unused) => Start();
        void GameUpdateScheduler.IUpdater<T>.PrepareRepeatUpdatePass() => Start();

        public void Add(T item)
        {
            if (_members.Contains(item) == false)
            {
                AddUnchecked(item);
            }
        }

        public void AddUnchecked(T item)
        {
            _members.Add(item);
        }

        public bool Remove(T item)
        {
            if (_members.TryFindIndex(item, out int found) == false)
            {
                return false;
            }

            if ((found > _cursor) || (_cursor >= _members.Count))
            {
                // Either we aren't iterating or the found element hasn't been
                // updated yet, so it's safe to remove it with a swap-and-pop.

                _members.EraseWithLastSwap(found);
                return true;
            }
            else
            {
                // The found index was already updated during this pass. We 
                // need to do two swaps to make sure we don't skip an update
                // on something later on in the list.

                _members[found] = Current;
                _members.EraseWithLastSwap(_cursor);
                --_cursor;
                return true;
            }
        }

        public void Clear()
        {
            _members.Clear();
        }

        public void Start()
        {
            _cursor = -1;
        }

        public bool MoveNext()
        {
            ++_cursor;
            return _cursor < _members.Count;
        }
    }
}
