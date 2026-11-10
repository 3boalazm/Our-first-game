namespace Core.Services
{
    public interface ITickableService : IService
    {
        /// <summary>
        /// Called in Update() of highest class in chain.
        /// </summary>
        void Tick() {}
        
        /// <summary>
        /// Called in LateUpdate() of highest class in chain.
        /// </summary>
        void TickLate() {}
    }
    public interface IService
    {
        void Initialize(ServiceContainer services);
        void Dispose();
    }
}