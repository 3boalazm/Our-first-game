using System.Collections.Generic;
using Core.GameStates.Types;
using Core.Services;
using UnityEngine;

namespace Core.GameStates
{
    public class GameStatesManager : MonoBehaviour, ITickableService
    {
        [SerializeField] private GameConnect gameConnect;
        [SerializeField] private GameMenu gameMenu;
        [SerializeField] private GameReconnect gameReconnect;
        [SerializeField] private GameJoinRoom gameJoinRoom;
        [SerializeField] private GameRunning gameRunning;

        private Queue<GameState> _pendingStates;
        private GameState _currentState;
        private ServiceContainer _serviceContainer;

        public void Initialize(ServiceContainer services)
        {
            _serviceContainer = services;

            _pendingStates = new Queue<GameState>();
            _pendingStates.Enqueue(gameConnect);

            if (GameReconnect.HasSavedToken())
            {
                _pendingStates.Enqueue(gameReconnect);
            }
            else
            {
                _pendingStates.Enqueue(gameMenu);
                _pendingStates.Enqueue(gameJoinRoom);
            }

            _pendingStates.Enqueue(gameRunning);

            _currentState = _pendingStates.Peek();
            Debug.Log("[GameStatesManager] Current state: " + _currentState.GetType().Name);

            _currentState.TriggerStateSwitch += SwitchState;
            _currentState.Initialize(_serviceContainer);
        }

        public void Tick()
        {
            if (_currentState == null)
            {
                Debug.Log("[GameStatesManager] Current state is null.");
                return;
            }
            _currentState.Tick();
        }

        public void Dispose() { }

        private void SwitchState()
        {
            var exitingState = _currentState;

            _currentState.Stop();
            _currentState.TriggerStateSwitch -= SwitchState;
            _pendingStates.Dequeue();

            if (exitingState == gameReconnect && !exitingState.Succeeded)
            {
                Debug.Log("[GameStatesManager] Reconnect failed, rerouting to Menu flow");
                _pendingStates.Clear();
                _pendingStates.Enqueue(gameMenu);
                _pendingStates.Enqueue(gameJoinRoom);
                _pendingStates.Enqueue(gameRunning);
            }

            if (_pendingStates.Count == 0)
            {
                Debug.LogWarning("[GameStatesManager] No more states in queue");
                _currentState = null;
                return;
            }

            var nextState = _pendingStates.Peek();
            Debug.Log($"[GameStatesManager] Switch: {exitingState.GetType().Name} → {nextState.GetType().Name}");

            _currentState = nextState;
            _currentState.TriggerStateSwitch += SwitchState;
            _currentState.Initialize(_serviceContainer);
        }
    }
}