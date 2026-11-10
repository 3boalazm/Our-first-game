using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Subsystems.Input;
using Game.Subsystems.Rendering;
using Game.Subsystems.UI;
using UnityEngine;
using Utils;

namespace Game.Subsystems
{
    public class GameSessionCoordinator : ITickableService
    {
        private CameraController _cameraController;
        
        private GameSessionData _sessionData;
        private GameWorldConfigProvider _configProvider;
        private ITerritoryDataProvider _territoryDataProvider;
        private IColorRegistry _colorRegistry;
        private IPlayerVisualsManager _playerVisualsManager;
        private IPlayerDataProvider _playerDataProvider;
        private TrailVisualsManager _trailVisualsManager;
        private TerritorySystem _territorySystem;
        private EffectsCoordinator _effectsCoordinator;
        private PredictionSystem _predictionSystem;
        private InputService _inputService;
        private IGameUICoordinator _gameUICoordinator;
        private IGameTickHandler _gameTickHandler;
        private IGameStateProcessor _gameStateProcessor;
        public void Initialize(ServiceContainer services)
        {
            _sessionData = services.Get<GameSessionData>();
            _configProvider = services.Get<GameWorldConfigProvider>();
            _territoryDataProvider = services.Get<TerritoryData>();
            _colorRegistry = services.Get<ColorsRegistry>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _trailVisualsManager = services.Get<TrailVisualsManager>();
            _territorySystem = services.Get<TerritorySystem>();
            _effectsCoordinator = services.Get<EffectsCoordinator>();
            _predictionSystem = services.Get<PredictionSystem>();
            _inputService = services.Get<InputService>();
            _gameUICoordinator = services.Get<GameUICoordinator>();
            _gameStateProcessor = services.Get<GameStateReceiver>();
            _gameTickHandler = services.Get<GameWorld>();
            _playerDataProvider = services.Get<PlayersContainer>();
        }

        public void TickLate()
        {
            if (_sessionData.IsGameActive && _cameraController != null)
            {
                _cameraController.TickLate();
            }
        }

        public void Dispose()
        {
            _inputService.DisableInput();
            _sessionData.SetEndGameData();
            _cameraController.Clear();
        }
        
        public void OnJoinedGame(PaperioJoinResponse response)
        {
            _sessionData.SetData(
                response.YourPlayerId,
                response.TickRateMs,
                response.MoveIntervalTicks
            );

            if (response.InitialState != null)
            {
                foreach (var player in response.InitialState.Players)
                {
                    _colorRegistry.Register(player.PlayerId);
                }

                _playerVisualsManager.UpdateFromState(response.InitialState, _sessionData.LocalPlayerId);
                _playerVisualsManager.SpawnPlayers();
                _sessionData.SetLocalPlayerCamera(FindLocalPlayerCamera());

                LocateCamera();
                
                if (_playerVisualsManager.LocalPlayerVisual != null)
                {
                    _playerVisualsManager.LocalPlayerVisual.SetMoveDuration(_sessionData.MoveDuration);
                }

                _cameraController.Initialize(
                    _territoryDataProvider.Width,
                    _territoryDataProvider.Height,
                    _configProvider.Config.CellSize
                );

                if (_playerVisualsManager.LocalPlayerVisual != null)
                {
                    _cameraController.SetLocalTarget(_playerVisualsManager.LocalPlayerVisual.transform);
                }

                _territorySystem.InitializeFromState(response.InitialState);

                _effectsCoordinator.PreparePools();

                foreach (var player in response.InitialState.Players)
                {
                    if (player.PlayerId == _sessionData.LocalPlayerId && player.Position != null)
                    {
                        _predictionSystem.InitializeForGame(
                            new Vector2Int(player.Position.X, player.Position.Y),
                            player.Direction
                        );
                        break;
                    }
                }

                _predictionSystem.SyncToServerTick(response.InitialState.Tick);
            }

            _gameUICoordinator.CreateAndInitializeGameUI(_territoryDataProvider);

            _inputService.EnableInput();
            _gameTickHandler.ResetTickTime();
            _sessionData.SetStartGameData();

            Debug.Log($"[GameSessionCoordinator] Game started! " +
                      $"LocalPlayer={_sessionData.LocalPlayerId}, " +
                      $"Grid={_territoryDataProvider.Width}x{_territoryDataProvider.Height}, " +
                      $"TickRate={_sessionData.TickRateMs}ms");
        }

        public void OnServerStateUpdated(PaperioState state)
        {
            _gameTickHandler.ResetTickTime();
            _gameStateProcessor.ProcessState(state);
        }

        public void OnPlayerEliminated(uint playerId)
        {
            if (playerId == _sessionData.LocalPlayerId)
                _inputService.DisableInput();

            _trailVisualsManager.RemoveTrail(playerId);
            _effectsCoordinator.OnPlayerEliminated(playerId);
        }

        public void OnPlayerRespawned(uint playerId)
        {
            bool isLocal = playerId == _sessionData.LocalPlayerId;

            _effectsCoordinator.OnPlayerRespawned(playerId);

            if (isLocal)
            {
                if (_cameraController != null && _playerVisualsManager.LocalPlayerVisual != null)
                {
                    _cameraController.SetLocalTarget(_playerVisualsManager.LocalPlayerVisual.transform);

                    var playerData = _playerDataProvider.TryGetPlayerById(playerId);
                    if (playerData != null)
                    {
                        _predictionSystem.ReinitializeAfterRespawn(playerData.GridPosition, Direction.None);
                        _playerVisualsManager.LocalPlayerVisual.SetMoveDuration(_sessionData.MoveDuration);
                    }
                }

                _inputService.EnableInput();
            }
        }

        public void OnPlayerDisconnected(uint playerId)
        {
            Debug.Log($"[GameSessionCoordinator] Player {playerId} disconnected — cleaning up visuals");

            _inputService.DisableInput();
            _trailVisualsManager.RemoveTrail(playerId);
            _playerVisualsManager.DespawnPlayer(playerId);

            _territorySystem.ClearOwnership(playerId);
        }
        
        private Camera FindLocalPlayerCamera()
        {
            var camObj = GameObject.FindWithTag(Constants.Tags.LocalPlayerCamera);
            return camObj != null ? camObj.GetComponent<Camera>() : null;
        }
        
        private void LocateCamera()
        {
            _cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>();
            if (_cameraController == null)
            {
                Debug.LogError("[GameSessionCoordinator] CameraController not found in scene after player spawn!");
            }
        }
    }
}