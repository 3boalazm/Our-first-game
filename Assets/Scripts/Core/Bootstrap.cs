using Core.GameStates;
using Core.Services;
using Game;
using Game.Data;
using Game.Effects;
using Game.Subsystems;
using Game.Subsystems.Input;
using Game.Subsystems.Rendering;
using Game.Subsystems.UI;
using Network;
using UnityEngine;
using UnityEngine.Serialization;

namespace Core
{
    public class Bootstrap : MonoBehaviour
    {
        [FormerlySerializedAs("messageSender")] [SerializeField] private NetworkManager networkManager;
        [SerializeField] private GameStatesManager gameStatesManager;
        [SerializeField] private ServerStateHandler serverStateHandler;
        [SerializeField] private GameWorld gameWorld;
        [SerializeField] private PlayerVisualsManager playerVisualsManager;
        [SerializeField] private EffectsManager effectsManager;
        [SerializeField] private TerritoryRenderer territoryRenderer;
        [SerializeField] private TrailVisualsManager trailVisualsManager;
        [SerializeField] private TerritoryClaim territoryClaim;
        [SerializeField] private GameUICoordinator gameUICoordinator;
        
        private PlayersContainer _playersContainer;
        private InputService _inputService;
        
        private GameSessionData _gameSessionData;
        private TerritoryVisualData _territoryVisualData;
        private TerritoryData _territoryData;
        private GameWorldConfigProvider _gameWorldConfigProvider;
        
        private ColorsRegistry _colorRegistry;
        private GameStateReceiver _stateReceiver;
        private PredictionSystem _predictionSystem;
        private TerritorySystem _territorySystem;
        
        private GameSessionCoordinator _gameSessionCoordinator;
        private EffectsCoordinator _effectsCoordinator;
        
        private ServiceContainer _services;

        private void Awake()
        {
            _services = new ServiceContainer();
            
            _playersContainer = new PlayersContainer();
            _inputService = new InputService();
            _colorRegistry = new ColorsRegistry();
            _stateReceiver = new GameStateReceiver();
            _predictionSystem = new PredictionSystem();
            _territorySystem = new TerritorySystem();
            _effectsCoordinator = new EffectsCoordinator();
            _gameSessionData = new GameSessionData();
            _territoryVisualData = new TerritoryVisualData();
            _territoryData = new TerritoryData();
            _gameWorldConfigProvider = new GameWorldConfigProvider(gameWorld.Config);
            _gameSessionCoordinator = new GameSessionCoordinator();
            
            _services.Register(_gameWorldConfigProvider);
            _services.Register(_territoryVisualData);
            _services.Register(_territoryData);
            _services.Register(_gameSessionData);
            _services.Register(networkManager);
            _services.Register(_playersContainer);
            _services.Register(serverStateHandler);
            _services.Register(gameStatesManager);
            _services.Register(playerVisualsManager);
            _services.Register(_inputService);
            _services.Register(_colorRegistry);
            _services.Register(effectsManager);
            _services.Register(territoryRenderer);
            _services.Register(trailVisualsManager);
            _services.Register(territoryClaim);
            
            // Game subsystems (depend on the above)
            RegisterGameSubsystems();
            
            _services.Register(gameWorld);
            
            _services.InitDanglingServices();
            
            return;

            void RegisterGameSubsystems()
            {
                _services.Register(_territorySystem);
                _services.Register(_effectsCoordinator);
                _services.Register(_predictionSystem);
                _services.Register(_stateReceiver);
                _services.Register(gameUICoordinator);
                _services.Register(_gameSessionCoordinator);
            }
        }

        private void Update()
        {
            _services.TickAll();
        }

        private void LateUpdate()
        {
            _services.TickLateAll();
        }

        private void OnDestroy()
        {
            _services.DisposeAll();
        }
    }
}