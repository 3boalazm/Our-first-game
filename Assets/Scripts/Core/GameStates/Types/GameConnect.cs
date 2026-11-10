using System;
using Core.Services;
using Network;

namespace Core.GameStates.Types
{
    public class GameConnect : GameState
    {
        public override Action TriggerStateSwitch { get; set; }
        
        private NetworkManager _networkManager;
        public override void Initialize(ServiceContainer container)
        {
            _networkManager = container.Get<NetworkManager>();
            _networkManager.OnConnected += OnConnected;
            
            _networkManager.Connect();
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            _networkManager.OnConnected -= OnConnected;
        }

        private void OnConnected()
        {
            TriggerStateSwitch?.Invoke();
        }
    }
}