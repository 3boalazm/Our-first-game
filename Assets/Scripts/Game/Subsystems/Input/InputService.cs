using System;
using Core.Services;
using Game.Paperio;
using Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Subsystems.Input
{
    public class InputService : IService
    {
        private const float InputCooldown = 0.15f;
        public event Action<Direction> OnDirectionChanged;
        
        private Direction _currentDirection = Direction.None;
        private Direction _lastSentDirection = Direction.None;
        private float _lastInputTime;
        
        private NetworkManager _networkManager;
        private PlayerInputActions _playerInputActions;
        
        public void Initialize(ServiceContainer services)
        {
            _networkManager = services.Get<NetworkManager>();
        
            _playerInputActions = new PlayerInputActions();
            
            _playerInputActions.Player.Move.performed += OnMovePerformed;
        }
        
        public void Dispose()
        {
            DisableInput();
            if (_playerInputActions != null)
            {
                _playerInputActions.Player.Move.performed -= OnMovePerformed;
                _playerInputActions.Dispose();
            }
        }
    
        public void EnableInput()
        {
            _playerInputActions.Player.Enable();
        }

        public void DisableInput()
        {
            _playerInputActions.Player.Disable();
        }
        
        public void SyncDirection(Direction serverDirection)
        {
            if (_currentDirection != serverDirection)
            {
                Debug.Log($"[InputService] SyncDirection: {_currentDirection} -> {serverDirection}");
                _currentDirection = serverDirection;
                _lastSentDirection = serverDirection;
            }
        }
        
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            var input = context.ReadValue<Vector2>();
            var newDirection = VectorToDirection(input);
            
            TryChangeDirection(newDirection);
        }
      
        
        private void TryChangeDirection(Direction newDirection)
        {
            if (newDirection == Direction.None)
            {
                return;
            }
            if (newDirection == _currentDirection)
            {
                return;
            }
            if (Time.time - _lastInputTime < InputCooldown)
            {
                return;
            }
            if (IsOppositeDirection(_currentDirection, newDirection))
            {
                return;
            }
            
            _currentDirection = newDirection;
            _lastInputTime = Time.time;
            
            OnDirectionChanged?.Invoke(newDirection);
            
            if (_networkManager != null && _networkManager.IsConnected && newDirection != _lastSentDirection)
            {
                _networkManager.SendDirection(newDirection);
                _lastSentDirection = newDirection;
                Debug.Log($"[InputService] Sent direction: {newDirection}");
            }
        }
        
        private Direction VectorToDirection(Vector2 input)
        {
            if (input.magnitude < 0.1f)
            {
                return Direction.None;
            }
        
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                return input.x > 0 ? Direction.Right : Direction.Left;
            }
            return input.y > 0 ? Direction.Up : Direction.Down;
        }
        
        private bool IsOppositeDirection(Direction a, Direction b)
        {
            return (a == Direction.Up && b == Direction.Down) ||
                   (a == Direction.Down && b == Direction.Up) ||
                   (a == Direction.Left && b == Direction.Right) ||
                   (a == Direction.Right && b == Direction.Left);
        }
    }
}