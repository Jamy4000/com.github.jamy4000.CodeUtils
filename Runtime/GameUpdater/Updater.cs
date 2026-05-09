using System;

namespace CodeUtils
{
    public interface IUpdaterFacade<TItem>
        where TItem : class
    {
        public void PrepareUpdatePass(float time);
        public void PrepareRepeatUpdatePass();
        public void Run(ref int count, Action<TItem> forEach, bool isPaused = false);
    }

    public class UpdaterFacade<TUpdater, TItem> : IUpdaterFacade<TItem>
           where TUpdater : GameUpdateScheduler.IUpdater<TItem>, new()
           where TItem : class
    {
        private readonly TUpdater Normal = new TUpdater();
        private readonly TUpdater Pausable = new TUpdater();

        public void Clear()
        {
            Normal.Clear();
            Pausable.Clear();
        }

        public void Register(TItem item)
        {
            if (item is I_Pausable)
            {
                Pausable.Add(item);
            }
            else
            {
                Normal.Add(item);
            }
        }

        public void RegisterUnchecked(TItem item)
        {
            if (item is I_Pausable)
            {
                Pausable.AddUnchecked(item);
            }
            else
            {
                Normal.AddUnchecked(item);
            }
        }

        public void Unregister(TItem item)
        {
            if (item is I_Pausable)
            {
                Pausable.Remove(item);
            }
            else
            {
                Normal.Remove(item);
            }
        }

        public void PrepareUpdatePass(float time)
        {
            Normal.PrepareUpdatePass(time);
            Pausable.PrepareUpdatePass(time);
        }

        public void PrepareRepeatUpdatePass()
        {
            Normal.PrepareRepeatUpdatePass();
            Pausable.PrepareRepeatUpdatePass();
        }

        public void Run(ref int count, Action<TItem> forEach, bool isPaused = false)
        {
            RunPrivate(ref count, Normal, forEach);

            if (!isPaused)
            {
                RunPrivate(ref count, Pausable, forEach);
            }
        }

        private void RunPrivate(ref int count, TUpdater updater, Action<TItem> forEach)
        {
            while (updater.MoveNext())
            {
                forEach.Invoke(updater.Current);
                ++count;
            }
        }
    }
}