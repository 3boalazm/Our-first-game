using System;
using System.Collections.Generic;
using Core.Services;
using UnityEngine;

namespace Game.Data
{
    public interface ITerritoryVisualDataProvider
    {
        Color32[] Colors { get; }
        Vector3[] Vertices { get; }
        int[] Triangles { get; }
        Vector2[] UVs { get; }
        int VertexCount { get; }
        Color32 GetCellColor(long cellIndex);
    }
    
    public class TerritoryVisualData : IService, ITerritoryVisualDataProvider
    {
        public Color32[] Colors { get; private set; }
        public Vector3[] Vertices { get; private set; }
        public int[] Triangles { get; private set; }
        public Vector2[] UVs { get; private set; }

        public int VertexCount => Colors?.Length ?? 0;
        public bool IsInitialized => Colors != null;

        private uint _width;
        private uint _height;
        private float _cellSize;
        private Color32 _neutralColor;

        private Func<uint, Color32> _colorResolver;

        public void Initialize(ServiceContainer services)
        { }

        public void Dispose()
        { }

        public void SetData(uint width, uint height, float cellSize, Color32 neutralColor, Func<uint, Color32> colorResolver)
        {
            _width = width;
            _height = height;
            _cellSize = cellSize;
            _neutralColor = neutralColor;
            _colorResolver = colorResolver;

            long cellCount = (long)width * height;
            long vertexCount = cellCount * 4;
            long triangleCount = cellCount * 6;

            Colors = new Color32[vertexCount];
            Vertices = new Vector3[vertexCount];
            Triangles = new int[triangleCount];
            UVs = new Vector2[vertexCount];
            
            GenerateGeometry();
        }
        
        public void ApplyChanges(List<TerritoryChange> changes)
        {
            if (!IsInitialized || changes == null)
            {
                return;
            }

            foreach (var change in changes)
            {
                if (change.X < 0 || change.X >= _width || change.Y < 0 || change.Y >= _height)
                    continue;

                long cellIndex = (long)change.Y * _width + change.X;
                SetCellColor(cellIndex, change.NewOwner);
            }
        }

        public void RebuildAllColors(uint[] cells)
        {
            if (!IsInitialized || cells == null)
            {
                return;
            }

            float cellCount = Mathf.Min(cells.Length, _width * _height);
            for (int i = 0; i < cellCount; i++)
            {
                SetCellColor(i, cells[i]);
            }
        }

        public Color32 GetCellColor(long cellIndex)
        {
            long vertexBase = cellIndex * 4;
            if (vertexBase < 0 || vertexBase >= Colors.Length)
                return _neutralColor;

            return Colors[vertexBase];
        }

        private void SetCellColor(long cellIndex, uint ownerId)
        {
            Color32 color = _colorResolver != null ? _colorResolver(ownerId) : _neutralColor;

            long vertexBase = cellIndex * 4;
            Colors[vertexBase + 0] = color;
            Colors[vertexBase + 1] = color;
            Colors[vertexBase + 2] = color;
            Colors[vertexBase + 3] = color;
        }

        private void GenerateGeometry()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    long cellIndex = (long)y * _width + x;
                    long vertexBase = cellIndex * 4;
                    long triangleBase = cellIndex * 6;

                    float x0 = x * _cellSize;
                    float x1 = (x + 1) * _cellSize;
                    long flippedY = _height - 1 - y;
                    float z0 = flippedY * _cellSize;
                    float z1 = (flippedY + 1) * _cellSize;

                    Vertices[vertexBase + 0] = new Vector3(x0, 0, z1);
                    Vertices[vertexBase + 1] = new Vector3(x1, 0, z1);
                    Vertices[vertexBase + 2] = new Vector3(x0, 0, z0);
                    Vertices[vertexBase + 3] = new Vector3(x1, 0, z0);

                    UVs[vertexBase + 0] = new Vector2(0, 1);
                    UVs[vertexBase + 1] = new Vector2(1, 1);
                    UVs[vertexBase + 2] = new Vector2(0, 0);
                    UVs[vertexBase + 3] = new Vector2(1, 0);

                    Triangles[triangleBase + 0] = (int)vertexBase + 0;
                    Triangles[triangleBase + 1] = (int)vertexBase + 2;
                    Triangles[triangleBase + 2] = (int)vertexBase + 1;
                    Triangles[triangleBase + 3] = (int)vertexBase + 1;
                    Triangles[triangleBase + 4] = (int)vertexBase + 2;
                    Triangles[triangleBase + 5] = (int)vertexBase + 3;

                    Colors[vertexBase + 0] = _neutralColor;
                    Colors[vertexBase + 1] = _neutralColor;
                    Colors[vertexBase + 2] = _neutralColor;
                    Colors[vertexBase + 3] = _neutralColor;
                }
            }
        }
    }
}