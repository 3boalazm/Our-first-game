using System;
using Core.Services;
using Game.Subsystems.UI;
using Game.UI.Menu;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameMenu : GameState
    {
        public override Action TriggerStateSwitch { get; set; }
        
        private NetworkManager _networkManager;
        private bool _isMainMenuCreated = false;
        
        private IGameUICoordinator _gameUICoordinator;
        private IMainMenuEventsHandler _mainMenuEvents;
        public override void Initialize(ServiceContainer container)
        {
            _networkManager = container.Get<NetworkManager>();
            _gameUICoordinator = container.Get<GameUICoordinator>();
        }

        public override void Tick()
        {
            if (!_isMainMenuCreated)
            {
                _gameUICoordinator.CreateMainMenu();
                _isMainMenuCreated = true;
            }
            
            if (_mainMenuEvents == null)
            {
                _mainMenuEvents = _gameUICoordinator.GameUIEventsProvider.MainMenuEventsHandler;
                _mainMenuEvents.OnPlayButtonClicked += OnChangeState;
            }
        }

        public override void Stop()
        {
            _mainMenuEvents.OnPlayButtonClicked -= OnChangeState;
            _gameUICoordinator.ClearMainMenu();
            _isMainMenuCreated = false;
        }

        private void OnChangeState(string playerName)
        {
            _networkManager.SetPlayerName(playerName);

            Debug.Log($"[GameMenu] Player name set to '{playerName}', transitioning to JoinRoom");
            TriggerStateSwitch?.Invoke();
        }
    }
}