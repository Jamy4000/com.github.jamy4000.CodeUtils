using UnityEngine;

namespace UnityTechnologies.CodeUtils
{
    /// <summary>
    /// This is just a wrapper to have the service locator in a scene
    /// </summary>
    public sealed class ServiceLocatorWrapper : MonoBehaviour , IServiceLocator
    {
        private readonly ServiceLocator _serviceLocator = new();

        private void Start()
        {
            InitializeServices();
            NotifyConsumers();
        }

        private void OnDestroy()
        {
            _serviceLocator.Dispose();
        }

        public void AddService<T>(T service) where T : IService
        {
            _serviceLocator.AddService(service);
        }

        public void RemoveService<T>() where T : IService
        {
            _serviceLocator.RemoveService<T>();
        }

        public void AddConsumer<T>(T consumer) where T : IServiceConsumer
        {
            _serviceLocator.AddConsumer(consumer);
        }

        public void RemoveConsumer<T>(T consumer) where T : IServiceConsumer
        {
            _serviceLocator.RemoveConsumer(consumer);
        }

        bool IServiceLocator.TryGetService<T>(out T service)
        {
            return _serviceLocator.TryGetService(out service);
        }

        T IServiceLocator.GetService<T>()
        {
            return _serviceLocator.GetService<T>();
        }

        public void InitializeServices()
        {
            _serviceLocator.InitializeServices();
        }

        public void NotifyConsumers()
        {
            _serviceLocator.NotifyConsumers();
        }
    }
}