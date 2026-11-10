using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Effects
{
    public struct EffectData
    {
        public List<TerritoryChange> TerritoryChange;
        public uint PlayerId;
        public Color Color;
        public Vector3 Position;
        
        public EffectData(
            List<TerritoryChange> territoryChange = null,
            uint playerId = default,
            Color color = default,
            Vector3 position = default)
        {
            TerritoryChange = territoryChange;
            PlayerId = playerId;
            Color = color;
            Position = position;
        }
    }
}