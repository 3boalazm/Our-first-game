using System;
using Core.Services;
using Game.Server;
using Network;

namespace Core.GameStates.Types
{
    public class GameJoinRoom : GameState
    {
        public override Action TriggerStateSwitch { get; set; }
        
        private NetworkManager _networkManager;
        public override void Initialize(ServiceContainer container)
        {
             _networkManager = container.Get<NetworkManager>();
             
             _networkManager.SendJoinRoom();
             
             _networkManager.OnRoomJoined += OnRoomJoined;
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            _networkManager.OnRoomJoined -= OnRoomJoined;
        }
        
        private void OnRoomJoined(RoomJoined obj)
        {
            TriggerStateSwitch?.Invoke();
        }
    }
}