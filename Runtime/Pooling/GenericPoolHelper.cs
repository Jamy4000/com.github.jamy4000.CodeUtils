using UnityEngine.Pool;

namespace UnityTechnologies.CodeUtils
{
    public abstract class GenericPoolHelper<TPoolable> where TPoolable : class, IGenericPoolable
    {
        private readonly ObjectPool<TPoolable> _objectPool;
        private readonly System.Action<IGenericPoolable> _cachedReleasePoolableCallback;

        public delegate void OnObjectPooledStatusChangedDelegate(TPoolable poolable);
        public event OnObjectPooledStatusChangedDelegate OnObjectWasPooledEvent;
        public event OnObjectPooledStatusChangedDelegate OnObjectWasReturnedEvent;

        protected GenericPoolHelper(int minPoolSize, int maxPoolSize, bool collectionChecks)
        {
            _cachedReleasePoolableCallback = ReleasePoolable;

            _objectPool = new ObjectPool<TPoolable>(CreatePooledItem, OnTakeFromPool,
                OnReturnedToPool, OnDestroyPoolObject, collectionChecks, minPoolSize, maxPoolSize);
        }

        ~GenericPoolHelper()
        {
            _objectPool.Dispose();
        }

        public TPoolable RequestPoolableObject()
        {
            TPoolable poolable = _objectPool.Get();
            poolable.OnShouldReturnToPool += _cachedReleasePoolableCallback;
            OnObjectWasPooledEvent?.Invoke(poolable);
            return poolable;
        }

        public void AddPreplacedPoolable(TPoolable poolable)
        {
            poolable.OnShouldReturnToPool += _cachedReleasePoolableCallback;
        }

        public void ReleasePoolable(TPoolable poolable)
        {
            poolable.OnShouldReturnToPool -= _cachedReleasePoolableCallback;
            OnObjectWasReturnedEvent?.Invoke(poolable);
            _objectPool.Release(poolable);
        }

        // event callback
        protected void ReleasePoolable(IGenericPoolable poolableToRelease)
        {
            TPoolable typed = poolableToRelease as TPoolable;
            if (typed == null)
            {
                UnityEngine.Debug.LogError(
                    $"[GenericPoolHelper] ReleasePoolable called with an object of type " +
                    $"'{poolableToRelease?.GetType().Name}' which cannot be cast to '{typeof(TPoolable).Name}'. Release ignored.");
                return;
            }
            ReleasePoolable(typed);
        }

        /// <summary>
        /// Called when the objects are being created inside the Pool
        /// </summary>
        protected abstract TPoolable CreatePooledItem();

        /// <summary>
        /// Called when an item is taken from the pool using Get
        /// </summary>
        protected virtual void OnTakeFromPool(TPoolable takenObject)
        {
            takenObject.Enable();
        }

        /// <summary>
        /// Called when an item is returned to the pool using Release
        /// </summary>
        /// <param name="system"></param>
        protected virtual void OnReturnedToPool(TPoolable releasedObject)
        {
            releasedObject.Disable();
        }

        /// <summary>
        /// If the pool capacity is reached then any items returned will be destroyed.
        /// We can control what the destroy behavior does, here we destroy the GameObject.
        /// </summary>
        protected virtual void OnDestroyPoolObject(TPoolable destroyedObject)
        {
            destroyedObject.Destroy();
        }
    }

    public interface IGenericPoolable
    {
        System.Action<IGenericPoolable> OnShouldReturnToPool { get; set; }

        void Enable();

        void Disable();

        void Destroy();
    }
}