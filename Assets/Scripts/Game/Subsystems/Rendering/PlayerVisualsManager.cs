using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using UnityEngine;
using Utils;

namespace Game.Subsystems.Rendering
{
    public interface IPlayerVisualsManager : IPlayerVisualsDataProvider
    {
        void UpdateFromState(PaperioState state, uint localPlayerId);
        void SpawnPlayers();
        void DespawnPlayer(uint playerId);
    }
    public interface IPlayerVisualsDataProvider
    {
        PlayerVisual LocalPlayerVisual { get; }
        IReadOnlyDictionary<uint, PlayerVisual> ActiveVisuals { get; }
    }
    public class PlayerVisualsManager : MonoBehaviour, IService, IPlayerVisualsManager
    {
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private PlayerVisual playerVisualPrefab;
        
        [SerializeField] private Transform visualsContainer;
        
        [SerializeField] private int initialPoolSize = 8;
        [SerializeField] private bool usePooling = true;
        
        private readonly Dictionary<uint, PlayerVisual> _activeVisuals = new();

        private readonly Queue<PlayerVisual> _pool = new();
        
        private PlayerVisual _localPlayerVisual;
        
        private uint _localPlayerId;
        private uint _currentTick;
        public int ActiveCount => _activeVisuals.Count;
        public PlayerVisual LocalPlayerVisual => _localPlayerVisual;
        public IReadOnlyDictionary<uint, PlayerVisual> ActiveVisuals => _activeVisuals;
        
        private IColorDataProvider _colorDataProvider;
        private IPlayerDataProvider _playerDataProvider;
        private GameWorldConfigProvider _gameWorldConfigProvider;
        public void Initialize(ServiceContainer services)
        {
            _playerDataProvider = services.Get<PlayersContainer>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            _gameWorldConfigProvider = services.Get<GameWorldConfigProvider>();
            
            // Create container for player game objects if not assigned
            if (visualsContainer == null)
            {
                var containerGO = new GameObject("PlayerVisuals");
                containerGO.transform.SetParent(transform);
                visualsContainer = containerGO.transform;
            }
            Debug.Log($"[PlayerVisualsManager] Initialized with pool size {_pool.Count}");
        }

        public void Dispose()
        {
            ClearAll();
            
            while (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                if (pooled != null)
                {
                    Destroy(pooled.gameObject);
                }
            }
        }

        public void SpawnPlayers()
        {
            if (usePooling)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    var visual = CreateNewVisual();
                    visual.gameObject.SetActive(false);
                    _pool.Enqueue(visual);
                }
            }
        }
        private void ActivateOrUpdatePlayer(PaperioPlayer protoPlayer)
        {
            uint playerId = protoPlayer.PlayerId;
            
            if (_activeVisuals.TryGetValue(playerId, out var existingVisual))
            {
                UpdateVisualFromProto(existingVisual, protoPlayer);
            }
            else
            {
                ActivatePlayer(protoPlayer); 
            }
        }

        private void ActivatePlayer(PaperioPlayer protoPlayer)
        {
            if (playerVisualPrefab == null)
            {
                Debug.LogError("[PlayerVisualsManager] PlayerVisual prefab not assigned!");
                return;
            }
            
            PlayerVisual visual = GetFromPoolOrCreate();
            
            Vector3 worldPos = CalculateWorldPosition(protoPlayer);

            Color color = _colorDataProvider.GetColorOf(protoPlayer.PlayerId);
            
            bool isLocal = protoPlayer.PlayerId == _localPlayerId;
            
            visual.Initialize(
                protoPlayer.PlayerId,
                protoPlayer.Name,
                color,
                worldPos,
                isLocal
            );
            
            _activeVisuals[protoPlayer.PlayerId] = visual;
            
            if (isLocal)
            {
                _localPlayerVisual = visual;
            }
            
            Debug.Log($"[PlayerVisualsManager] Activated player: {protoPlayer.Name} (ID: {protoPlayer.PlayerId})" +
                      (isLocal ? " [LOCAL]" : ""));
        }
        
        public void DespawnPlayer(uint playerId)
        {
            if (!_activeVisuals.TryGetValue(playerId, out var visual))
            {
                return;
            }
            
            _activeVisuals.Remove(playerId);
            
            if (visual == _localPlayerVisual)
            {
                _localPlayerVisual = null;
            }
            
            if (usePooling)
            {
                visual.ResetForPool();
                _pool.Enqueue(visual);
            }
            else
            {
                Destroy(visual.gameObject);
            }
            
            Debug.Log($"[PlayerVisualsManager] Despawned player: {playerId}");
        }

        private void UpdateVisualFromProto(PlayerVisual visual, PaperioPlayer protoPlayer)
        {
            var playerData = _playerDataProvider.TryGetPlayerById(protoPlayer.PlayerId);
            if (playerData == null)
            {
                return;
            }
            
            Vector3 worldPos = CalculateWorldPosition(protoPlayer);
            
            visual.UpdateFromData(playerData, worldPos, _currentTick);
        }

        public void UpdateFromState(PaperioState state, uint localPlayerId)
        {
            _localPlayerId = localPlayerId;
            _currentTick = state.Tick;
            var currentPlayers = new HashSet<uint>();
    
            foreach (var protoPlayer in state.Players)
            {
                currentPlayers.Add(protoPlayer.PlayerId);
                ActivateOrUpdatePlayer(protoPlayer);
            }
    
            var toRemove = new List<uint>();
            foreach (var playerId in _activeVisuals.Keys)
            {
                if (!currentPlayers.Contains(playerId))
                {
                    toRemove.Add(playerId);
                }
            }
    
            foreach (var playerId in toRemove)
            {
                DespawnPlayer(playerId);
            }
        }
        
        public void UpdateInterpolation(float tickProgress)
        {
            foreach (var visual in _activeVisuals.Values)
            {
                visual.UpdateInterpolation(tickProgress);
            }
        }

        private void ClearAll()
        {
            foreach (var visual in _activeVisuals.Values)
            {
                if (usePooling)
                {
                    visual.ResetForPool();
                    _pool.Enqueue(visual);
                }
                else
                {
                    Destroy(visual.gameObject);
                }
            }
            
            _activeVisuals.Clear();
            _localPlayerVisual = null;
            
            Debug.Log("[PlayerVisualsManager] Cleared all visuals");
        }
        
        private Vector3 CalculateWorldPosition(PaperioPlayer protoPlayer)
        {
            if (protoPlayer.Position == null)
            {
                return Vector3.zero;
            }

            return GridHelper.GridToWorld(
                protoPlayer.Position.X,
                protoPlayer.Position.Y,
                _gameWorldConfigProvider.Config.CellSize,
                playerConfig.PlayerHeight
            );
        }

        private PlayerVisual GetFromPoolOrCreate()
        {
            if (usePooling && _pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            
            return CreateNewVisual();
        }

        private PlayerVisual CreateNewVisual()
        {
            var createdPlayer = Instantiate(playerVisualPrefab, visualsContainer);
            return createdPlayer.GetComponent<PlayerVisual>();
        }
    }
}