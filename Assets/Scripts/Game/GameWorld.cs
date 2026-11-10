using Core.Services;
using Game.Data;
using Game.Subsystems;
using Game.Subsystems.Rendering;
using UnityEngine;

namespace Game
{
    public interface IGameTickHandler
    {
        void ResetTickTime();
    }
    
    public class GameWorld : MonoBehaviour, ITickableService, IGameTickHandler
    {
        [SerializeField] private GameWorldConfig config;

        private float _lastTickTime;

        public GameWorldConfig Config => config;

        private float TickProgress
        {
            get
            {
                if (_sessionData.TickRateMs == 0)
                {
                    return 1f;
                }

                float moveDuration = _sessionData.MoveDuration;
                float elapsed = Time.time - _lastTickTime;
                return Mathf.Clamp01(elapsed / moveDuration);
            }
        }

        private IGameSessionDataProvider _sessionData;
        private PredictionSystem _predictionSystem;
        private PlayerVisualsManager _playerVisualsManager;
        public void Initialize(ServiceContainer services)
        {
            _sessionData = services.Get<GameSessionData>();
            _predictionSystem = services.Get<PredictionSystem>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
        }

        public void Tick()
        {
            if (!_sessionData.IsGameActive)
            {
                return;
            }

            _predictionSystem.Tick();
            _playerVisualsManager.UpdateInterpolation(TickProgress);
        }

        public void Dispose() { }

        public void ResetTickTime()
        {
            _lastTickTime = Time.time;
        }
    }
}