using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Subsystems.Rendering;
using UnityEngine;

namespace Game.Subsystems
{
    public interface ITerritoryStateHandler
    {
        List<TerritoryChange> ProcessStateUpdate(PaperioState state);
    }

    public interface ITerritoryEventsHandler
    {
        Action<int, Color> OnLocalClaim { get; set; }
    }
    
    public class TerritorySystem : IService, ITerritoryStateHandler, ITerritoryEventsHandler
    {
        public Action<int, Color> OnLocalClaim { get; set; }
        
        private ITerritoryDataHandler _territoryDataHandler;
        private TerritoryRenderer _territoryRenderer;
        private TerritoryClaim _territoryClaim;
        private IColorDataProvider _colorDataProvider;
        private IPlayerDataProvider _playerDataProvider;
        private IGameSessionDataProvider _sessionDataProvider;
        private GameWorldConfigProvider _configProvider;
        public void Initialize(ServiceContainer services)
        {
            _territoryDataHandler = services.Get<TerritoryData>();
            _territoryRenderer = services.Get<TerritoryRenderer>();
            _territoryClaim = services.Get<TerritoryClaim>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            _playerDataProvider = services.Get<PlayersContainer>();
            _sessionDataProvider = services.Get<GameSessionData>();

            _configProvider = services.Get<GameWorldConfigProvider>();
        }

        public void Dispose()
        {
            _territoryClaim?.FinishAllImmediately();
        }

        public void InitializeFromState(PaperioState initialState)
        {
            uint width = initialState.GridWidth;
            uint height = initialState.GridHeight;

            _territoryDataHandler.SetData(
                width,
                height,
                _configProvider.Config.CellSize,
                _configProvider.Config.NeutralColor,
                ownerId => _colorDataProvider.GetTerritoryColor(ownerId, _configProvider.Config.NeutralColor));
            
            if (initialState.Territory != null)
            {
                _territoryDataHandler.ApplyFullState(initialState.Territory);
            }

            _territoryRenderer.CreateTerritory();
            _territoryClaim.Prepare();
        }

        public List<TerritoryChange> ProcessStateUpdate(PaperioState state)
        {
            if (_territoryDataHandler == null)
            {
                return new List<TerritoryChange>();
            }

            List<TerritoryChange> changes;

            bool isDelta = state.StateType == StateType.StateDelta;

            if (isDelta && state.TerritoryChanges.Count > 0)
            {
                changes = _territoryDataHandler.ApplyDeltaChanges(state.TerritoryChanges);
            }
            else if (!isDelta && state.Territory != null && state.Territory.Count > 0)
            {
                changes = _territoryDataHandler.ApplyFullState(state.Territory);
            }
            else
            {
                return new List<TerritoryChange>();
            }

            if (changes.Count > 0)
            {
                ApplyVisuals(changes);
            }

            return changes;
        }

        public List<TerritoryChange> ClearOwnership(uint playerId)
        {
            if (_territoryDataHandler == null)
            {
                return new List<TerritoryChange>();
            }

            var changes = _territoryDataHandler.ClearOwnership(playerId);
            if (changes.Count > 0)
            {
                _territoryRenderer.FlushToMesh(changes.Count);
                _territoryClaim.SyncNonAnimatedColors();
            }

            return changes;
        }

        private void ApplyVisuals(List<TerritoryChange> changes)
        {
            _territoryRenderer.FlushToMesh(changes.Count);
            _territoryClaim.SyncNonAnimatedColors();

            var playerData = _playerDataProvider.TryGetPlayerById(changes[0].NewOwner);
            if (playerData != null)
            {
                _territoryClaim.AddWave(changes, playerData.PlayerId, playerData.Color);

                int localClaimCount = 0;
                Color localColor = Color.white;

                foreach (var change in changes)
                {
                    if (change.NewOwner == _sessionDataProvider.LocalPlayerId)
                    {
                        localClaimCount++;
                        if (localColor == Color.white)
                        {
                            localColor = _colorDataProvider.GetColorOf(_sessionDataProvider.LocalPlayerId);
                        }
                    }
                }

                if (localClaimCount > 0)
                {
                    OnLocalClaim?.Invoke(localClaimCount, localColor);
                }
            }
        }
    }
}