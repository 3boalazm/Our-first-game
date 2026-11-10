using Core.Services;

namespace Game.Data
{
    public class GameWorldConfigProvider : IService
    {
        public GameWorldConfig Config { get; }

        public GameWorldConfigProvider(GameWorldConfig config)
        {
            Config = config;
        }

        public void Initialize(ServiceContainer services) { }
        public void Dispose() { }
    }
}