namespace UnityTechnologies.CodeUtils
{
    public interface IServiceLocator
    {
        void AddService<T>(T service) where T : IService;
        void RemoveService<T>() where T : IService;

        void AddConsumer<T>(T consumer) where T : IServiceConsumer;
        public void RemoveConsumer<T>(T consumer) where T : IServiceConsumer;

        bool TryGetService<T>(out T service) where T : class, IService;
        T GetService<T>() where T : class, IService;

        void InitializeServices();
        void NotifyConsumers();
    }

    public interface IService
    {
        void Initialize(IServiceLocator serviceLocator);
    }

    public interface IServiceConsumer
    {
        void ConsumeLocator(IServiceLocator locator);
    }
}