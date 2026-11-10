using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Data
{
    [CreateAssetMenu(fileName = "New Game World Config", menuName = "Game/Data/Game World Config", order = 1)]
    public class GameWorldConfig : ScriptableObject
    {
        [Tooltip("Size of each grid cell in world units")]
        public float CellSize = 1.0f;
        
        [Tooltip("Neutral territory color (unclaimed cells)")]
        public Color NeutralColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        
        [Tooltip("Interval in ticks for player movement updates (server tick rate)")]
        public uint moveIntervalTicks = 3;
    }
}