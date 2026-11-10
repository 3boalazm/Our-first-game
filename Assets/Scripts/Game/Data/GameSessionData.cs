using System;
using Core.Services;
using UnityEngine;
using Utils;

namespace Game.Data
{
    public interface IGameSessionDataProvider
    {
        public uint LocalPlayerId { get; }
        public Camera LocalPlayerCamera { get; }
        public uint TickRateMs { get; }
        public uint MoveIntervalTicks { get; }
        public bool IsGameActive { get; }
        public float MoveDuration { get; }
        public Action OnGameStarted { get; set; }
        public Action OnGameEnded { get; set; }
    }
    public class GameSessionData : IService, IGameSessionDataProvider
    {
        public uint LocalPlayerId { get; private set; }
        public Camera LocalPlayerCamera { get; private set; }
        public uint TickRateMs { get; private set; }
        public uint MoveIntervalTicks { get; private set; }
        public bool IsGameActive { get; private set; }
        public Action OnGameStarted { get; set; }
        public Action OnGameEnded { get; set; }

        public float MoveDuration => MoveIntervalTicks * (TickRateMs / 1000f);
        
        public void Initialize(ServiceContainer services)
        { }             

        public void Dispose()
        {
            Reset();
        }
        
        public void SetData(uint playerId, uint tickRateMs, uint moveIntervalTicks)
        {
            LocalPlayerId = playerId;
            TickRateMs = tickRateMs;
            MoveIntervalTicks = moveIntervalTicks;
        }

        public void SetLocalPlayerCamera(Camera camera)
        {
            LocalPlayerCamera = camera;
        }

        public void SetStartGameData()
        {
            IsGameActive = true;
            OnGameStarted?.Invoke();
        }

        public void SetEndGameData()
        {
            IsGameActive = false;
            OnGameEnded?.Invoke();
        }


        private void Reset()
        {
            LocalPlayerId = 0;
            TickRateMs = 0;
            MoveIntervalTicks = 0;
            IsGameActive = false;
        }
    }
}