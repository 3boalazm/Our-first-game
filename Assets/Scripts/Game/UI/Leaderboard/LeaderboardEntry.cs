using UnityEngine;

namespace Game.UI.Leaderboard
{
    public readonly struct LeaderboardEntry
    {
        public readonly uint PlayerId;
        public readonly string Name;
        public readonly float Percentage;
        public readonly Color Color;
        public readonly bool IsLocal;

        public LeaderboardEntry(uint playerId, string name, float percentage, Color color, bool isLocal)
        {
            PlayerId = playerId;
            Name = name;
            Percentage = percentage;
            Color = color;
            IsLocal = isLocal;
        }
    }
}