using System.Collections.Generic;
using UnityEngine;

namespace CodeUtils
{
    /// <summary>
    /// Catch the Unity Messages and call the corresponding method in the GameUpdateScheduler.
    /// This is mostly a MonoBehaviour facade for our deeper logic stored in the GameUpdateScheduler.
    /// </summary>
    public sealed class GameUpdater : MonoBehaviour
    {
        // Singleton pattern, but we hide the instance; no other class should access this.
        private static GameUpdater _instance;

        private bool _isPaused;

        private readonly GameUpdateScheduler _scheduler = new();

        private static readonly List<GameUpdateScheduler.IScheduled> _pendingRegistration = new List<GameUpdateScheduler.IScheduled>(64);
        private static readonly List<GameUpdateScheduler.IScheduled> _pendingUnregistration = new List<GameUpdateScheduler.IScheduled>(64);

        private void Awake()
        {
            Debug.Assert(_instance == null, "There is more than one GameUpdater in the scene, this is not allowed.");
            _instance = this;

            for (int i = 0; i < _pendingRegistration.Count; i++)
            {
                _scheduler.Register(_pendingRegistration[i]);
            }
            _pendingRegistration.Clear();

            for (int i = 0; i < _pendingUnregistration.Count; i++)
            {
                _scheduler.Unregister(_pendingUnregistration[i]);
            }
            _pendingUnregistration.Clear();
        }

        private void Update()
        {
            _scheduler.RunUpdate(_isPaused);
        }

        private void LateUpdate()
        {
            _scheduler.RunLateUpdate(_isPaused);
        }

        private void FixedUpdate()
        {
            _scheduler.RunFixedUpdate(_isPaused);
        }

        private void OnDestroy()
        {
            _scheduler.UnregisterAll();
            _pendingRegistration.Clear();
            _pendingUnregistration.Clear();
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
        }

        public static void Register(GameUpdateScheduler.IScheduled item)
        {
            if (_instance != null)
                _instance._scheduler.Register(item);
            else
                _pendingRegistration.Add(item);
        }

        public static void Unregister(GameUpdateScheduler.IScheduled item)
        {
            if (_instance != null)
                _instance._scheduler.Unregister(item);
            else
                _pendingUnregistration.Add(item);
        }
    }
}
