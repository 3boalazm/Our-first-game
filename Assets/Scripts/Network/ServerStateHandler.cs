using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Server;
using UnityEngine;

namespace Network
{
    public class ServerStateHandler : MonoBehaviour, IService
    {
        public event Action<PaperioJoinResponse> OnJoinedGame;
        public event Action<PaperioState> OnStateUpdated;
        public event Action<uint> OnPlayerEliminated;
        public event Action<uint> OnPlayerRespawned;
        
        private uint _lastReceivedTick;
        private uint _localPlayerId;
        private uint _tickRateMs;
        private uint _gridWidth;
        private uint _gridHeight;

        private PaperioState _currentState;
        
        private NetworkManager _networkManager;
        private IPlayersContainer _playersContainer;

        private bool _hasValidBaseline;
        
        private int _deltasSkipped;
        
        public bool HasJoinedGame { get; private set; }

        public void Initialize(ServiceContainer services)
        {
            _networkManager = services.Get<NetworkManager>();
            _playersContainer = services.Get<PlayersContainer>();
            
            _networkManager.OnPaperioStateReceived += HandleStateReceived;
            _networkManager.OnPaperioJoinResponse += HandleJoinResponse;
        }

        public void Dispose()
        {
            if (_networkManager != null)
            {
                _networkManager.OnPaperioStateReceived -= HandleStateReceived;
                _networkManager.OnPaperioJoinResponse -= HandleJoinResponse;
            }
            
            HasJoinedGame = false;
            _hasValidBaseline = false;
            _currentState = null;
        }

        public void ResetForReconnect()
        {
            _lastReceivedTick = 0;
            _localPlayerId = 0;
            _tickRateMs = 0;
            _gridWidth = 0;
            _gridHeight = 0;
            HasJoinedGame = false;
            _hasValidBaseline = false;
            _currentState = null;
            _deltasSkipped = 0;

            Debug.Log("[ServerStateHandler] Reset for reconnect");
        }
        
        private void HandleStateReceived(PaperioState state)
        {
            // Ignore old states (can happen due to UDP packet reordering)
            if (state.Tick <= _lastReceivedTick && _lastReceivedTick != 0)
            {
                Debug.LogWarning($"[ServerStateHandler] Received old state: tick {state.Tick}, current {_lastReceivedTick}");
                return;
            }
            
            ApplyState(state);
        }
        
        private void HandleJoinResponse(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            _tickRateMs = response.TickRateMs;
            HasJoinedGame = true;
            
            if (response.InitialState != null)
            {
                _gridWidth = response.InitialState.GridWidth;
                _gridHeight = response.InitialState.GridHeight;
                
                _hasValidBaseline = true;
                
                ApplyState(response.InitialState);
            }
            
            Debug.Log($"[ServerStateHandler] Joined game: " +
                      $"player={_localPlayerId}, " +
                      $"grid={_gridWidth}x{_gridHeight}, " +
                      $"tickRate={_tickRateMs}ms");
            
            OnJoinedGame?.Invoke(response);
        }

        private void ApplyState(PaperioState state)
        {
            var previousState = _currentState;
            
            bool isDeltaChange = state.StateType == StateType.StateDelta;
            
            if (isDeltaChange)
            {
                if (!_hasValidBaseline)
                {
                    _deltasSkipped++;
                    if (_deltasSkipped % 10 == 1)
                    {
                        Debug.LogWarning($"[ServerStateHandler] Skipping delta tick {state.Tick}: no valid baseline (skipped {_deltasSkipped} total)");
                    }
                    return;
                }
                
                if (_currentState != null)
                {
                    state.Territory.Clear();
                    state.Territory.AddRange(_currentState.Territory);
                    state.GridWidth = _gridWidth;
                    state.GridHeight = _gridHeight;
                }
            }
            else
            {
                _hasValidBaseline = true;
                _deltasSkipped = 0;
            }
            
            _currentState = state;
            _lastReceivedTick = state.Tick;
            
            UpdatePlayersFromState(state, previousState);
            
            if (previousState != null)
            {
                DetectStateChanges(previousState, state);
            }
            
            OnStateUpdated?.Invoke(state);
        }
        
        private void UpdatePlayersFromState(PaperioState state, PaperioState previousState)
        {
            if (_playersContainer == null)
            {
                return;
            }
            
            var currentPlayerIds = new HashSet<uint>();
            
            foreach (var protoPlayer in state.Players)
            {
                currentPlayerIds.Add(protoPlayer.PlayerId);
                
                var playerData = _playersContainer.TryGetPlayerById(protoPlayer.PlayerId);
                if (playerData == null)
                {
                    var info = new PlayerInfo
                    {
                        PlayerId = protoPlayer.PlayerId,
                        Name = protoPlayer.Name
                    };
                    playerData = _playersContainer.Register(info);
                    Debug.Log($"[ServerStateHandler] New player registered: {protoPlayer.Name} (ID: {protoPlayer.PlayerId})");
                }
                
                UpdatePlayerData(playerData, protoPlayer);
            }
            
            if (previousState != null)
            {
                foreach (var prevPlayer in previousState.Players)
                {
                    if (!currentPlayerIds.Contains(prevPlayer.PlayerId))
                    {
                        _playersContainer.Unregister(prevPlayer.PlayerId);
                        Debug.Log($"[ServerStateHandler] Player removed: {prevPlayer.Name} (ID: {prevPlayer.PlayerId})");
                    }
                }
            }
        }
        
        private void UpdatePlayerData(PlayerData playerData, PaperioPlayer protoPlayer)
        {
            if (protoPlayer.Position != null)
            {
                playerData.GridPosition = new Vector2Int(protoPlayer.Position.X, protoPlayer.Position.Y);
            }
            
            playerData.Direction = protoPlayer.Direction;
            
            playerData.Alive = protoPlayer.Alive;
            playerData.Score = protoPlayer.Score;
            playerData.Color = UIntToColor(protoPlayer.Color);
        }
        
        private void DetectStateChanges(PaperioState previous, PaperioState current)
        {
            var prevPlayers = new Dictionary<uint, PaperioPlayer>();
            foreach (var p in previous.Players)
            {
                prevPlayers[p.PlayerId] = p;
            }
            
            foreach (var player in current.Players)
            {
                if (prevPlayers.TryGetValue(player.PlayerId, out var prevPlayer))
                {
                    if (prevPlayer.Alive && !player.Alive)
                    {
                        Debug.Log($"[ServerStateHandler] Player {player.PlayerId} ({player.Name}) eliminated");
                        OnPlayerEliminated?.Invoke(player.PlayerId);
                    }
                    else if (!prevPlayer.Alive && player.Alive)
                    {
                        Debug.Log($"[ServerStateHandler] Player {player.PlayerId} ({player.Name}) respawned");
                        OnPlayerRespawned?.Invoke(player.PlayerId);
                    }
                }
            }
        }
        
        private static Color UIntToColor(uint color)
        {
            float r = ((color >> 24) & 0xFF) / 255f;
            float g = ((color >> 16) & 0xFF) / 255f;
            float b = ((color >> 8) & 0xFF) / 255f;
            float a = (color & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }
    }
}