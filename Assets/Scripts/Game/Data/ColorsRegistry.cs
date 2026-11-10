using System.Collections.Generic;
using Core.Services;
using UnityEngine;

namespace Game.Data
{
    public interface IColorRegistry : IColorDataProvider
    {
        void Register(uint playerId);
    }
    
    public interface IColorDataProvider
    {
        IReadOnlyDictionary<uint, Color> Colors { get; }
        Color GetColorOf(uint playerId);
        Color32 GetTerritoryColor(uint ownerId, Color32 neutralColor);
    }
    
    public class ColorsRegistry : IService, IColorRegistry
    {
        private readonly Color32[] _playerColors = {
            new(255, 77, 77, 255),   // Red
            new(77, 153, 255, 255),  // Blue  
            new(77, 255, 77, 255),   // Green
            new(255, 255, 77, 255),  // Yellow
            new(255, 77, 255, 255),  // Magenta
            new(77, 255, 255, 255),  // Cyan
            new(255, 153, 77, 255),  // Orange
            new(153, 77, 255, 255),  // Purple
        };
        
        private readonly Dictionary<uint, Color> _colors = new();
        
        public IReadOnlyDictionary<uint, Color> Colors => _colors;

        private IPlayerDataProvider _playersDataProvider;
        private GameWorldConfigProvider _gameWorldConfigProvider;
        public void Initialize(ServiceContainer services)
        {
            _gameWorldConfigProvider = services.Get<GameWorldConfigProvider>();
            _playersDataProvider = services.Get<PlayersContainer>();
        }

        public void Dispose()
        {
            _colors.Clear();
        }

        public void Register(uint playerId)
        {
            if (playerId == 0 || _colors.ContainsKey(playerId))
            {
                return;
            }

            Color color = GetPlayerColor(playerId);
            _colors[playerId] = color;
        }

        public Color GetColorOf(uint playerId)
        {
            if (playerId == 0)
            {
                return Color.white;
            }
            
            if (_colors.TryGetValue(playerId, out Color color))
            {
                return color;
            }
            
            Color resolved = GetPlayerColor(playerId);
            _colors[playerId] = resolved;
            return resolved;
        }

        public Color32 GetTerritoryColor(uint ownerId, Color32 neutralColor)
        {
            if (ownerId == 0)
            {
                return neutralColor;
            }

            Color playerColor = GetColorOf(ownerId);
            return new Color(
                playerColor.r * 0.7f,
                playerColor.g * 0.7f,
                playerColor.b * 0.7f,
                1f
            );
        }

        private Color GetPlayerColor(uint playerId)
        {
            if (playerId == 0)
            {
                return _gameWorldConfigProvider.Config.NeutralColor;
            }
            
            var playerData = _playersDataProvider.TryGetPlayerById(playerId);
            if (playerData != null && playerData.Color != default)
            {
                return playerData.Color;
            }
            
            int index = (int)((playerId - 1) % _playerColors.Length);
            return _playerColors[index];
        }
    }
}