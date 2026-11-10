using Game.Paperio;
using UnityEngine;

namespace Game.Data
{
    public class PlayerData
    {
        public uint PlayerId { get; set; }
        public string Name { get; set; } = "";
        public Vector2Int GridPosition { get; set; }
        public Direction Direction { get; set; } = Direction.None;
        public bool Alive { get; set; } = true;
        public uint Score { get; set; }
        public Color Color { get; set; } = Color.white;
        public bool IsReady { get; set; }
    }
}