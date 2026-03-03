using UnityEngine.Pool;

namespace UnityTechnologies.CodeUtils.SpatialPartionning
{
    public sealed class QuadtreePool : System.IDisposable
    {
        private readonly ObjectPool<Quadtree> _objectPool;
        private readonly UnityEngine.Vector2 _center;
        private readonly UnityEngine.Vector2 _size;
        private readonly System.Func<UnityEngine.Vector2, UnityEngine.Vector2, Quadtree> _quadtreeCreationMethod;
        
        // Flag to avoid infinite recursion when calling Dispose on the pool while destroying objects
        private bool _hasBeenDisposed = false;
        
        public QuadtreePool(UnityEngine.Vector2 center, UnityEngine.Vector2 size, 
            System.Func<UnityEngine.Vector2, UnityEngine.Vector2, Quadtree> quadtreeCreationMethod,
            int minPoolSize = 4, int maxPoolSize = 128, bool collectionChecks = false)
        {
            _objectPool = new ObjectPool<Quadtree>(CreatePooledItem, null,
                null, OnDestroyPoolObject, collectionChecks, minPoolSize, maxPoolSize);
            _center = center;
            _size = size;
            _quadtreeCreationMethod = quadtreeCreationMethod;
        }

        public void Dispose()
        {
            if (!_hasBeenDisposed)
            {
                _objectPool.Dispose();
                _hasBeenDisposed = true;
            }
        }

        public Quadtree RequestQuadtree()
        {
            return _objectPool.Get();
        }

        public void ReleaseQuadtree(Quadtree poolable)
        {
            _objectPool.Release(poolable);
        }

        /// <summary>
        /// Called when the objects are being created inside the Pool
        /// </summary>
        private Quadtree CreatePooledItem()
        {
            return _quadtreeCreationMethod.Invoke(_center, _size);
        }
        
        /// <summary>
        /// If the pool capacity is reached then any items returned will be destroyed.
        /// We can control what the destroy behavior does, here we destroy the GameObject.
        /// </summary>
        private void OnDestroyPoolObject(Quadtree destroyedObject)
        {
            destroyedObject.Dispose();
        }
    }
}