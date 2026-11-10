using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Subsystems.Input;
using Game.Subsystems.Rendering;
using Network;
using UnityEngine;
using Utils;

namespace Game.Subsystems
{
    public class PredictionSystem : ITickableService
    {
        private uint _estimatedServerTick;
        private float _tickAccumulator;
        
        private ClientPrediction _prediction;
        private IGameSessionDataProvider _sessionDataProvider;
        private ITerritoryDataProvider _territoryData;
        private PlayerVisualsManager _playerVisualsManager;
        private InputService _inputService;
        private GameWorldConfigProvider _config;
        public void Initialize(ServiceContainer services)
        {
            _sessionDataProvider = services.Get<GameSessionData>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _inputService = services.Get<InputService>();
            _territoryData = services.Get<TerritoryData>();
            _config = services.Get<GameWorldConfigProvider>();

            _inputService.OnDirectionChanged += OnDirectionChanged;
        }

        public void Tick()
        {
            if (!_sessionDataProvider.IsGameActive || _prediction == null || _sessionDataProvider.TickRateMs == 0)
            {
                return;
            }

            _tickAccumulator += Time.deltaTime;
            float tickDuration = _sessionDataProvider.TickRateMs / 1000f;

            while (_tickAccumulator >= tickDuration)
            {
                _tickAccumulator -= tickDuration;
                _estimatedServerTick++;
                _prediction.AdvancePrediction(_estimatedServerTick);
            }

            if (_playerVisualsManager.LocalPlayerVisual != null)
            {
                var predictedWorldPos = GridHelper.GridToWorld(
                    _prediction.PredictedPosition.x,
                    _prediction.PredictedPosition.y,
                    _config.Config.CellSize,
                    _playerVisualsManager.LocalPlayerVisual.transform.position.y
                );
                _playerVisualsManager.LocalPlayerVisual.SetPredictedTarget(predictedWorldPos);
            }
        }

        public void Dispose()
        {
            if (_inputService != null)
            {
                _inputService.OnDirectionChanged -= OnDirectionChanged;
            }
            _prediction = null;
        }

        public void InitializeForGame(Vector2Int startPosition, Direction startDirection)
        {
            _prediction = new ClientPrediction(_territoryData.Width, _territoryData.Height);
            _prediction.Initialize(startPosition, startDirection);
            _prediction.SetMoveInterval(_sessionDataProvider.MoveIntervalTicks);

            _estimatedServerTick = 0;
            _tickAccumulator = 0f;
        }

        public void SyncToServerTick(uint serverTick)
        {
            _estimatedServerTick = serverTick;
            _tickAccumulator = 0f;
        }

        public bool Reconcile(uint serverTick, Vector2Int serverPosition, Direction serverDirection)
        {
            if (_prediction == null) return false;

            _inputService.SyncDirection(serverDirection);

            bool corrected = _prediction.Reconcile(serverTick, serverPosition, serverDirection);

            _estimatedServerTick = serverTick;
            _tickAccumulator = 0f;

            return corrected;
        }

        public void ReinitializeAfterRespawn(Vector2Int position, Direction direction)
        {
            _inputService.SyncDirection(direction);
            _prediction?.Initialize(position, direction);
            _prediction?.SetMoveInterval(_sessionDataProvider.MoveIntervalTicks);
        }
        
        private void OnDirectionChanged(Direction direction)
        {
            Debug.Log($"[PredictionSystem] Input: {direction}, estimated tick: {_estimatedServerTick}");
            _prediction?.RecordInput(direction, _estimatedServerTick + 1);
        }
    }
}