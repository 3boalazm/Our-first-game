using Core.Services;
using Game.Data;
using Game.Subsystems.Rendering;
using Game.UI.Leaderboard;
using Game.UI.Menu;
using Game.UI.Territory;
using UnityEngine;
using Utils;

namespace Game.Subsystems.UI
{
    public interface IGameUIEventsProvider
    {
        public IMainMenuEventsHandler MainMenuEventsHandler { get; }
    }
    public interface IGameUICoordinator
    {
        IGameUIEventsProvider GameUIEventsProvider { get; }
        void CreateMainMenu();
        void CreateAndInitializeGameUI(ITerritoryDataProvider territoryDataProvider);
        void ClearMainMenu();
    }
    
    public class GameUICoordinator : MonoBehaviour, ITickableService, IGameUICoordinator, IGameUIEventsProvider
    {
        [SerializeField] private LeaderboardUI leaderboardUIPrefab;
        [SerializeField] private TerritoryClaimPopupManager territoryClaimPopupManagerPrefab;
        [SerializeField] private MinimapUI minimapUIPrefab;
        [SerializeField] private MainMenu mainMenuPrefab;
        
        private LeaderboardUI _leaderboardUI;
        private TerritoryClaimPopupManager _territoryClaimPopupManager;
        private MinimapUI _minimapUI;

        private GameObject _hud;
        private bool _gameUiInitialized = false;
        
        public IGameUIEventsProvider GameUIEventsProvider { get; private set; }
        public IMainMenuEventsHandler MainMenuEventsHandler { get; private set; }
        
        private IGameStateReceiver _stateReceiver;
        private IColorDataProvider _colorDataProvider;
        private IGameSessionDataProvider _gameSessionData;
        private ITerritoryEventsHandler _territoryEventsHandler;
        private ITerritoryDataProvider _territoryData;
        private IPlayerVisualsDataProvider _playerVisualsData;
        public void Initialize(ServiceContainer services)
        {
            GameUIEventsProvider = this;
            
            _stateReceiver = services.Get<GameStateReceiver>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            _gameSessionData = services.Get<GameSessionData>();
            _territoryEventsHandler = services.Get<TerritorySystem>();
            _playerVisualsData = services.Get<PlayerVisualsManager>();
            _territoryData = services.Get<TerritoryData>();
        }

        private MainMenu _mainMenu;

        public void CreateMainMenu()
        {
            _hud = GameObject.FindWithTag(Constants.Tags.HUD);
            _mainMenu = Instantiate(mainMenuPrefab, _hud.transform);
            MainMenuEventsHandler = _mainMenu;
            
            _mainMenu.Setup();
        }
        
        public void CreateAndInitializeGameUI(ITerritoryDataProvider territoryDataProvider)
        {
            _hud = GameObject.FindWithTag(Constants.Tags.HUD);
            
            _leaderboardUI = Instantiate(leaderboardUIPrefab, _hud.transform);
            _territoryClaimPopupManager = Instantiate(territoryClaimPopupManagerPrefab, _hud.transform);
            _minimapUI = Instantiate(minimapUIPrefab, _hud.transform);
            
            _leaderboardUI.Setup(_stateReceiver, _colorDataProvider, _territoryData, _gameSessionData);
            _territoryClaimPopupManager.Setup(_territoryEventsHandler, _territoryData, _gameSessionData, _playerVisualsData);
            _minimapUI.Setup(_gameSessionData, territoryDataProvider, _playerVisualsData, _colorDataProvider);
            
            _gameUiInitialized = true;
        }

        public void ClearMainMenu()
        {
            _mainMenu.Clear();
        }
        
        public void Tick()
        {
            if (!_gameUiInitialized)
            {
                return;
            }
            _territoryClaimPopupManager.Tick();
            _minimapUI.Tick();
        }

        public void TickLate()
        {
            if (!_gameUiInitialized)
            {
                return;
            }
            _minimapUI.TickLate();
        }

        public void Dispose()
        {
            _gameUiInitialized = false;
            _leaderboardUI.Clear();
            _territoryClaimPopupManager.Clear();
            _minimapUI.Clear();
        }
    }
}