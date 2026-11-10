using System;
using System.Collections.Generic;
using Core.Services;
using Game.Server;

namespace Game.Data
{
    public interface IPlayersContainer : IPlayerDataProvider
    {
        PlayerData Register(PlayerInfo playerInfo);
        bool Unregister(uint playerID);
    }
    public interface IPlayerDataProvider
    {
        PlayerData TryGetPlayerById(uint playerId);
    }
    public class PlayersContainer : IService, IPlayersContainer
    {
        private readonly Dictionary<uint, PlayerData> _playersContainer = new();

        public void Initialize(ServiceContainer services)
        { }

        public void Dispose()
        {
            Clear();
        }

        public PlayerData Register(PlayerInfo playerInfo)
        {
            var newPlayer = new PlayerData
            {
                PlayerId = playerInfo.PlayerId,
                Name = playerInfo.Name,
                IsReady = playerInfo.Ready
            };
            _playersContainer[playerInfo.PlayerId] = newPlayer;
            
            return _playersContainer[playerInfo.PlayerId];
        }

        public bool Unregister(uint playerID)
        {
            return _playersContainer.Remove(playerID);
        }

        public PlayerData TryGetPlayerById(uint playerId)
        {
            _playersContainer.TryGetValue(playerId, out PlayerData player);
            return player;
        }

        private void Clear()
        {
            _playersContainer.Clear();
        }
    }
}