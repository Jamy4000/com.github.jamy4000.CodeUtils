using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace CodeUtils
{
    public interface I_Pausable { };
    public interface I_UpdatePhaseEarly { };
    public interface I_UpdatePhaseLate { };

    public interface I_Startable : GameUpdateScheduler.IScheduled
    {
        // Recommended implementation to avoid any conflicts or issues:
        //     bool I_Startable.HasStarted { get; set; }
        bool HasStarted { get; set; }
        void Start();
    }

    public interface I_UpdateOnly : GameUpdateScheduler.IScheduled
    {
        void ManualUpdate();
    }

    public interface I_LateUpdateOnly : GameUpdateScheduler.IScheduled
    {
        void ManualLateUpdate();
    }

    public interface I_FixedUpdateOnly : GameUpdateScheduler.IScheduled
    {
        void ManualFixedUpdate();
    }

    public interface I_Update1Hz : GameUpdateScheduler.IScheduled
    {
        void ManualUpdate1Hz();
    }

    public interface I_Update3Hz : GameUpdateScheduler.IScheduled
    {
        void ManualUpdate3Hz();
    }

    /// <summary>
    /// <para>
    /// UpdateScheduler is an alternative updater that provides the facility
    /// of Unity's MonoBehavior OnStateStarted, Update, ManualLateUpdate, and FixedUpdate functions
    /// without Unity's overhead. This class also provides support for amortized fixed
    /// interval updates (currently at 1Hz or 3Hz) that automatically spread their
    /// update ticks between frames within the interval to avoid spikes.
    /// </para>
    ///
    /// <para>
    /// Newly added objects are first staged in a pending list that is processed
    /// during the Update pass (same as Unity's Update pass). Objects that have
    /// the start interface are also started at this Time. After this point the
    /// object is activated and will receive all registered update function calls.
    /// An object will not be updated until the first frame (Update pass) that occurs
    /// after the object is added to the scheduler. If a ManualLateUpdate or FixedUpdate
    /// pass begins during that Time, it will not be applied to the pending object.
    /// </para>
    ///
    /// <para>
    /// This scheduler is designed for safe addition and removal of objects during
    /// any start or update pass.
    /// </para>
    ///
    /// <para>
    /// There are 3 phases to each update that are processed in order
    /// from early to late. An object should only register with one phase.
    /// </para>
    ///
    /// <para>
    /// EARLY phase is for objects that schedule async. jobs
    /// that can run in parallel to other object updates e.g. batched raycasts
    /// </para>
    ///
    /// <para>
    /// DEFAULT_MASK phase is for the majority of objects that don't
    /// meet the early or late phase requirements
    /// </para>
    ///
    /// <para>
    /// LATE phase is for objects that can cause async. jobs to be completed
    /// e.g. modifying the physics simulation causes all async. physics queries
    /// to be completed immediatley
    /// </para>
    /// </summary>
    public sealed class GameUpdateScheduler
    {
        private const int UPDATE_PHASE_EARLY = 0;
        private const int UPDATE_PHASE_DEFAULT = 1;
        private const int UPDATE_PHASE_LATE = 2;
        private const int UPDATE_PHASE_COUNT = 3;

        /// <summary>
        /// Generic polymorphism helper interface for anything schedulable.
        /// </summary>
        public interface IScheduled
        {
        };

        /// <summary>
        /// Helper interface for enabling some templating and code reuse.
        /// </summary>
        public interface IUpdater<T>
        {
            /// <summary>
            /// The current element pointed to in the iteration.
            /// Valid only if there is an active pass and MoveNext() returned true.
            /// </summary>
            T Current { get; }

            /// <summary>
            /// Safely adds an element to the updater.
            /// </summary>
            void Add(T item);

            /// <summary>
            /// Adds an element to the updater.
            /// </summary>
            void AddUnchecked(T item);

            /// <summary>
            /// Removes an item and returns true if an only if it exists.
            /// </summary>
            bool Remove(T item);

            /// <summary>
            /// Prepares the list to be iterated with MoveNext().
            /// For interval schedulers, this adds Time to the round robin.
            /// Must be called before a pass.
            /// </summary>
            void PrepareUpdatePass(float time);

            /// <summary>
            /// Reset back to state when PrepareUpdatePass was called to allow repeat passes.
            /// </summary>
            void PrepareRepeatUpdatePass();

            /// <summary>
            /// Retrieves the next element in the current iteration pass.
            /// Returns false if no more elements are available.
            /// </summary>
            bool MoveNext();

            /// <summary>
            /// Clears all elements in the list.
            /// </summary>
            void Clear();
        }

        private readonly Action<IScheduled> _runPendingTransfer;
        private readonly HashSet<IScheduled> _allMembers = new HashSet<IScheduled>();

        private readonly CompleteUpdaterFacade<IScheduled> _pending = new CompleteUpdaterFacade<IScheduled>();

        private readonly CompleteUpdaterFacade<I_UpdateOnly>[] _toUpdateOnly = new CompleteUpdaterFacade<I_UpdateOnly>[UPDATE_PHASE_COUNT];
        private readonly CompleteUpdaterFacade<I_LateUpdateOnly>[] _toLateUpdateOnly = new CompleteUpdaterFacade<I_LateUpdateOnly>[UPDATE_PHASE_COUNT];
        private readonly CompleteUpdaterFacade<I_FixedUpdateOnly>[] _toFixedUpdateOnly = new CompleteUpdaterFacade<I_FixedUpdateOnly>[UPDATE_PHASE_COUNT];

        private readonly IntervalUpdater1Hz<I_Update1Hz> _toUpdate1Hz = new IntervalUpdater1Hz<I_Update1Hz>();
        private readonly IntervalUpdater3Hz<I_Update3Hz> _toUpdate3Hz = new IntervalUpdater3Hz<I_Update3Hz>();

        private readonly Action<IScheduled> _runPendingStart;
        private readonly Action<I_UpdateOnly> _runUpdateOnly;
        private readonly Action<I_LateUpdateOnly> _runLateUpdateOnly;
        private readonly Action<I_FixedUpdateOnly> _runFixedUpdateOnly;
        private readonly Action<I_Update1Hz> _runUpdate1Hz;
        private readonly Action<I_Update3Hz> _runUpdate3Hz;

        public GameUpdateScheduler()
        {
            for (int i = 0; i < UPDATE_PHASE_COUNT; ++i)
            {
                _toUpdateOnly[i] = new CompleteUpdaterFacade<I_UpdateOnly>();
                _toLateUpdateOnly[i] = new CompleteUpdaterFacade<I_LateUpdateOnly>();
                _toFixedUpdateOnly[i] = new CompleteUpdaterFacade<I_FixedUpdateOnly>();
            }

            _runPendingTransfer = (IScheduled item) =>
            {
                RegisterComplete(item);
                RegisterInterval(item);
            };

            _runPendingStart = MakeStartDelegate();
            _runUpdateOnly = MakeUpdateDelegate<I_UpdateOnly>((item) => item.ManualUpdate());
            _runLateUpdateOnly = MakeUpdateDelegate<I_LateUpdateOnly>((item) => item.ManualLateUpdate());
            _runFixedUpdateOnly = MakeUpdateDelegate<I_FixedUpdateOnly>((item) => item.ManualFixedUpdate());
            _runUpdate1Hz = MakeUpdateDelegate<I_Update1Hz>((item) => item.ManualUpdate1Hz());
            _runUpdate3Hz = MakeUpdateDelegate<I_Update3Hz>((item) => item.ManualUpdate3Hz());
        }

        public void Register(IScheduled item)
        {
            if (_allMembers.Add(item))
            {
                _pending.RegisterUnchecked(item);
            }
        }

        public void Unregister(IScheduled item)
        {
            if (_allMembers.Remove(item))
            {
                UnregisterComplete(item);
                UnregisterInterval(item);
            }
        }

        public void UnregisterAll()
        {
            foreach (var item in _allMembers)
            {
                UnregisterComplete(item);
                UnregisterInterval(item);
            }
            _allMembers.Clear();
        }

        internal void RunUpdate(bool isPaused)
        {
            UpdatePending();

            for (int phase = 0; phase < UPDATE_PHASE_COUNT; ++phase)
            {
                int count = 0;
                DoFacadePass(ref count, _toUpdateOnly[phase], Time.unscaledTime, _runUpdateOnly, isPaused, phase);

                UpdateIntervals(ref count, isPaused, phase);

                // there isn't an easy way to check if there are pending jobs
                // so best we can do is check if we actually updated anything
                if ((phase == 0) && (count > 0))
                    JobHandle.ScheduleBatchedJobs();
            }
        }

        internal void RunLateUpdate(bool isPaused)
        {
            for (int phase = 0; phase < UPDATE_PHASE_COUNT; ++phase)
            {
                int count = 0;
                DoFacadePass(ref count, _toLateUpdateOnly[phase], Time.unscaledTime, _runLateUpdateOnly, isPaused, phase);

                // there isn't an easy way to check if there are pending jobs
                // so best we can do is check if we actually updated anything
                if ((phase == 0) && (count > 0))
                    JobHandle.ScheduleBatchedJobs();
            }
        }

        internal void RunFixedUpdate(bool isPaused)
        {
            for (int phase = 0; phase < UPDATE_PHASE_COUNT; ++phase)
            {
                int count = 0;
                DoFacadePass(ref count, _toFixedUpdateOnly[phase], Time.fixedUnscaledDeltaTime, _runFixedUpdateOnly, isPaused, phase);

                // there isn't an easy way to check if there are pending jobs
                // so best we can do is check if we actually updated anything
                if ((phase == 0) && (count > 0))
                    JobHandle.ScheduleBatchedJobs();
            }
        }

        private void UpdatePending()
        {
            // We need to do pending in two passes. First, we need to run any start function if
            // the object has one. Then we need to make a second pass to load the pending into
            // the main update groups. We do this because an object could unregister itself in
            // its start function, and so the second pass is the cheapest way to guard for that.

            int count = 0;
            DoFacadePass(ref count, _pending, Time.unscaledTime, _runPendingStart, false, 0);
            DoFacadePass(ref count, _pending, Time.unscaledTime, _runPendingTransfer, false, 0);
            _pending.Clear();
        }

        private void UpdateIntervals(ref int count, bool isPaused, int phase)
        {
            DoIntervalPass(ref count, _toUpdate1Hz, Time.unscaledTime, _runUpdate1Hz, isPaused, phase);
            DoIntervalPass(ref count, _toUpdate3Hz, Time.unscaledTime, _runUpdate3Hz, isPaused, phase);
        }

        private int GetUpdatePhase(IScheduled item)
        {
            if (item is I_UpdatePhaseEarly)
                return UPDATE_PHASE_EARLY;

            if (item is I_UpdatePhaseLate)
                return UPDATE_PHASE_LATE;

            return UPDATE_PHASE_DEFAULT;
        }

        private void RegisterComplete(IScheduled item)
        {
            int index = GetUpdatePhase(item);

            // These two are mutually exclusive!

            if (item is I_UpdateOnly only)
            {
                _toUpdateOnly[index].RegisterUnchecked(only);
            }

            if (item is I_LateUpdateOnly lateOnly)
            {
                _toLateUpdateOnly[index].RegisterUnchecked(lateOnly);
            }

            if (item is I_FixedUpdateOnly fixedOnly)
            {
                _toFixedUpdateOnly[index].RegisterUnchecked(fixedOnly);
            }
        }

        private void RegisterInterval(IScheduled item)
        {
            if (item is I_Update1Hz item1Hz)
                _toUpdate1Hz.AddUnchecked(item1Hz);
            if (item is I_Update3Hz item3Hz)
                _toUpdate3Hz.AddUnchecked(item3Hz);
        }

        private void UnregisterComplete(IScheduled item)
        {
            int index = GetUpdatePhase(item);

            _pending.Unregister(item);

            if (item is I_UpdateOnly only)
                _toUpdateOnly[index].Unregister(only);
            if (item is I_LateUpdateOnly lateOnly)
                _toLateUpdateOnly[index].Unregister(lateOnly);
            if (item is I_FixedUpdateOnly fixedOnly)
                _toFixedUpdateOnly[index].Unregister(fixedOnly);
        }

        private void UnregisterInterval(IScheduled item)
        {
            if (item is I_Update1Hz item1Hz)
                _toUpdate1Hz.Remove(item1Hz);
            if (item is I_Update3Hz item3Hz)
                _toUpdate3Hz.Remove(item3Hz);
        }

        private void DoFacadePass<TFacade, TItem>(ref int count, TFacade facade, float time, Action<TItem> forEach, bool isPaused, int phase)
            where TFacade : IUpdaterFacade<TItem>
            where TItem : class, IScheduled
        {
            if (phase == 0)
                facade.PrepareUpdatePass(time);
            else
                facade.PrepareRepeatUpdatePass();

            facade.Run(ref count, forEach, isPaused);
        }

        private void DoIntervalPass<TUpdater, TItem>(ref int count, TUpdater updater, float time, Action<TItem> forEach, bool isPaused, int phase)
            where TUpdater : IUpdater<TItem>
            where TItem : class, IScheduled
        {
            if (phase == 0)
                updater.PrepareUpdatePass(time);
            else
                updater.PrepareRepeatUpdatePass();

            while (updater.MoveNext())
            {
                TItem item = updater.Current;

                if (isPaused && item is I_Pausable)
                    continue;

                int itemPhase = GetUpdatePhase(item);
                if (itemPhase != phase)
                    continue;

                forEach.Invoke(item);
                ++count;
            }
        }

        private Action<IScheduled> MakeStartDelegate()
        {
            return (IScheduled item) =>
            {
                if (item is I_Startable startable && !startable.HasStarted)
                {
                    try { startable.Start(); }
                    catch (Exception) { throw; }

                    startable.HasStarted = true;
                }
            };
        }

        private Action<TItem> MakeUpdateDelegate<TItem>(Action<TItem> updateCall)
            where TItem : class, IScheduled
        {
            return (TItem item) =>
            {
                try { updateCall.Invoke(item); }
                catch (Exception) { throw; }
            };
        }
    }
}
