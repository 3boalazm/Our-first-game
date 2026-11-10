using UnityEngine;

namespace Utils
{
    public static class GridHelper
    {
        public static Vector3 GridToWorld(int gridX, int gridY, float cellSize, float height = 0f)
        {
            return new Vector3(
                (gridX + 0.5f) * cellSize,
                height,
                (gridY + 0.5f) * cellSize
            );
        }

        public static Vector3 GetGridCenter(uint gridWidth, uint gridHeight, float cellSize)
        {
            return new Vector3(
                (gridWidth * cellSize) / 2f,
                0f,
                (gridHeight * cellSize) / 2f
            );
        }

        public static Bounds GetGridBounds(uint gridWidth, uint gridHeight, float cellSize)
        {
            var center = GetGridCenter(gridWidth, gridHeight, cellSize);
            var size = new Vector3(gridWidth * cellSize, 10f, gridHeight * cellSize);
            return new Bounds(center, size);
        }
    }
}