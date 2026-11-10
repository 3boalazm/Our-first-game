using Core.Services;
using Game.Data;
using Game.Effects;
using Game.Subsystems.Rendering;
using Utils;

namespace Game.Subsystems
{
    public class EffectsCoordinator : IService
    {
        private EffectsManager _effectsManager;
        private IPlayerDataProvider _playerDataProvider;
        private IGameSessionDataProvider _sessionDataProvider;
        private GameWorldConfigProvider _configProvider;
        public void Initialize(ServiceContainer services)
        {
            _effectsManager = services.Get<EffectsManager>();
            _playerDataProvider = services.Get<PlayersContainer>();
            
            _sessionDataProvider = services.Get<GameSessionData>();
            _configProvider = services.Get<GameWorldConfigProvider>();
        }

        public void Dispose() { }

        public void PreparePools()
        {
            _effectsManager.PreparePools();
        }
        
        public void OnPlayerEliminated(uint playerId)
        {
            bool isLocal = playerId == _sessionDataProvider.LocalPlayerId;

            var playerData = _playerDataProvider.TryGetPlayerById(playerId);
            if (playerData == null)
            {
                return;
            }

            var worldPos = GridHelper.GridToWorld(
                playerData.GridPosition.x,
                playerData.GridPosition.y,
                _configProvider.Config.CellSize
            );

            var effectData = new EffectData(position: worldPos, color: playerData.Color);
            _effectsManager.PlayEffect(Effect.Death, effectData);

            if (isLocal)
            {
                _effectsManager.PlayEffect(Effect.CameraShake, effectData);
            }
        }

        public void OnPlayerRespawned(uint playerId)
        {
            var playerData = _playerDataProvider.TryGetPlayerById(playerId);
            if (playerData == null)
            {
                return;
            }

            var worldPos = GridHelper.GridToWorld(
                playerData.GridPosition.x,
                playerData.GridPosition.y,
                _configProvider.Config.CellSize
            );

            var effectData = new EffectData(position: worldPos, color: playerData.Color);
            _effectsManager.PlayEffect(Effect.Respawn, effectData);
        }
    }
}