using System.Collections.Generic;

namespace UnityTechnologies.CodeUtils
{
    public sealed class ServiceLocator : IServiceLocator, System.IDisposable
    {
        private readonly Dictionary<System.Type, IService> _services = new(64);
        private readonly List<IServiceConsumer> _consumers = new(64);

        public void AddService<T>(T service) where T : IService
        {
            _services.Add(typeof(T), service);
        }

        public void RemoveService<T>() where T : IService
        {
            _services.Remove(typeof(T));
        }

        public bool TryGetService<T>(out T serviceImpl) where T : class, IService
        {
            if (_services.TryGetValue(typeof(T), out IService service))
            {
                serviceImpl = service as T;
                return true;
            }
            else
            {
                serviceImpl = null;
                return false;
            }
        }


        public T GetService<T>() where T : class, IService
        {
            return _services[typeof(T)] as T;
        }

        public void Dispose()
        {
            _services.Clear();
        }

        public void AddConsumer<T>(T consumer) where T : IServiceConsumer
        {
            _consumers.Add(consumer);
        }

        public void RemoveConsumer<T>(T consumer) where T : IServiceConsumer
        {
            _consumers.Remove(consumer);
        }

        public void InitializeServices()
        {
            foreach (var service in _services)
            {
                service.Value.Initialize(this);
            }
        }

        public void NotifyConsumers()
        {
            foreach (var consumer in _consumers)
            {
                consumer.ConsumeLocator(this);
            }
        }
    }
}