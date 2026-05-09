using UnityEngine.Pool;

namespace CodeUtils.SpatialPartitioning
{
    public sealed class OctreePool : System.IDisposable
    {
        private readonly ObjectPool<Octree> _objectPool;
        private readonly UnityEngine.Vector3 _center;
        private readonly UnityEngine.Vector3 _size;
        private readonly System.Func<UnityEngine.Vector3, UnityEngine.Vector3, Octree> _octreeCreationMethod;
        
        // Flag to avoid infinite recursion when calling Dispose on the pool while destroying objects
        private bool _hasBeenDisposed = false;
        
        public OctreePool(UnityEngine.Vector3 center, UnityEngine.Vector3 size, 
            System.Func<UnityEngine.Vector3, UnityEngine.Vector3, Octree> octreeCreationMethod,
            int minPoolSize = 4, int maxPoolSize = 128, bool collectionChecks = false)
        {
            _objectPool = new ObjectPool<Octree>(CreatePooledItem, null,
                null, OnDestroyPoolObject, collectionChecks, minPoolSize, maxPoolSize);
            _center = center;
            _size = size;
            _octreeCreationMethod = octreeCreationMethod;
        }

        public void Dispose()
        {
            if (!_hasBeenDisposed)
            {
                _objectPool.Dispose();
                _hasBeenDisposed = true;
            }
        }

        public Octree RequestOctree()
        {
            return _objectPool.Get();
        }

        public void ReleaseOctree(Octree poolable)
        {
            _objectPool.Release(poolable);
        }

        /// <summary>
        /// Called when the objects are being created inside the Pool
        /// </summary>
        private Octree CreatePooledItem()
        {
            return _octreeCreationMethod.Invoke(_center, _size);
        }
        
        /// <summary>
        /// If the pool capacity is reached then any items returned will be destroyed.
        /// We can control what the destroy behavior does, here we destroy the GameObject.
        /// </summary>
        private void OnDestroyPoolObject(Octree destroyedObject)
        {
            destroyedObject.Dispose();
        }
    }
}