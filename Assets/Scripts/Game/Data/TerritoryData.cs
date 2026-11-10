using System;
using System.Collections.Generic;
using Core.Services;
using Game.Paperio;
using UnityEngine;

namespace Game.Data
{
    public readonly struct TerritoryChange
    {
        public readonly int X;
        public readonly int Y;
        public readonly uint PreviousOwner;
        public readonly uint NewOwner;

        public TerritoryChange(int x, int y, uint previousOwner, uint newOwner)
        {
            X = x;
            Y = y;
            PreviousOwner = previousOwner;
            NewOwner = newOwner;
        }

        public Vector2Int Position => new Vector2Int(X, Y);
    }

    public interface ITerritoryDataHandler
    {
        void SetData(uint width, uint height, float cellSize, Color32 neutralColor, Func<uint, Color32> colorResolver);
        List<TerritoryChange> ApplyFullState(IEnumerable<TerritoryRow> territoryRows);
        List<TerritoryChange> ApplyDeltaChanges(IEnumerable<TerritoryCell> cellChanges);
        List<TerritoryChange> ClearOwnership(uint playerId);
    }

    public interface ITerritoryDataProvider
    {
        uint Width { get; }
        uint Height { get; }
        int ClaimedCells { get; }
        uint TotalCells { get; }
        float GetOwnershipPercentage(uint playerId);
        bool IsOwnedBy(int x, int y, uint playerId);
    }
    
    public class TerritoryData : IService, ITerritoryDataProvider, ITerritoryDataHandler
    {
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public int ClaimedCells { get; private set; }
        public uint TotalCells => Width * Height;
        
        private uint[] _cells;

        private TerritoryVisualData _visualData;
        public void Initialize(ServiceContainer services)
        {
            _visualData = services.Get<TerritoryVisualData>();
        }

        public void Dispose()
        {
            System.Array.Clear(_cells, 0, _cells.Length);
            ClaimedCells = 0;

            _visualData.RebuildAllColors(_cells);
        }
        
        public void SetData(uint width, uint height, float cellSize, Color32 neutralColor, Func<uint, Color32> colorResolver)
        {
            Width = width;
            Height = height;
            ClaimedCells = 0;
            _cells = new uint[width * height];
            
            _visualData.SetData(Width, Height, cellSize, neutralColor, colorResolver);
        }

        public void SetData(int width, int height, float cellSize, Color32 neutralColor, Func<uint, Color32> colorResolver)
        {
            throw new NotImplementedException();
        }

        public List<TerritoryChange> ApplyFullState(IEnumerable<TerritoryRow> territoryRows)
        {
            var changes = new List<TerritoryChange>();
            var processedRows = new HashSet<int>();

            foreach (var row in territoryRows)
            {
                int y = row.Y;

                if (y < 0 || y >= Height)
                {
                    continue;
                }

                processedRows.Add(y);
                DecodeRow(row, changes);
            }

            for (int y = 0; y < Height; y++)
            {
                if (!processedRows.Contains(y))
                {
                    for (int x = 0; x < Width; x++)
                    {
                        long idx = y * Width + x;
                        if (_cells[idx] != 0)
                        {
                            changes.Add(new TerritoryChange(x, y, _cells[idx], 0));
                            _cells[idx] = 0;
                        }
                    }
                }
            }

            RecalculateClaimedCells();

            _visualData.ApplyChanges(changes);

            return changes;
        }
        
        public List<TerritoryChange> ApplyDeltaChanges(IEnumerable<TerritoryCell> cellChanges)
        {
            var changes = new List<TerritoryChange>();

            foreach (var cell in cellChanges)
            {
                int x = cell.X;
                int y = cell.Y;

                if (!IsInBounds(x, y))
                {
                    Debug.LogWarning($"[TerritoryData] Delta cell ({x},{y}) out of bounds");
                    continue;
                }

                long idx = y * Width + x;
                uint previousOwner = _cells[idx];
                uint newOwner = cell.OwnerId;

                if (previousOwner != newOwner)
                {
                    _cells[idx] = newOwner;

                    if (previousOwner == 0 && newOwner != 0)
                    {
                        ClaimedCells++;
                    }
                    else if (previousOwner != 0 && newOwner == 0)
                    {
                        ClaimedCells--;
                    }

                    changes.Add(new TerritoryChange(x, y, previousOwner, newOwner));
                }
            }

            _visualData.ApplyChanges(changes);

            return changes;
        }
        
        public List<TerritoryChange> ClearOwnership(uint playerId)
        {
            var changes = new List<TerritoryChange>();

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == playerId)
                {
                    long x = i % Width;
                    long y = i / Width;

                    changes.Add(new TerritoryChange((int)x, (int)y, playerId, 0));
                    _cells[i] = 0;
                }
            }

            if (changes.Count > 0)
            {
                RecalculateClaimedCells();
                _visualData.ApplyChanges(changes);
            }

            return changes;
        }
        
        public float GetOwnershipPercentage(uint playerId)
        {
            if (TotalCells == 0 || playerId == 0)
            {
                return 0f;
            }

            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == playerId)
                {
                    count++;
                }
            }

            return (count * 100f) / TotalCells;
        }
        
        public bool IsOwnedBy(int x, int y, uint playerId)
        {
            return GetOwner(x, y) == playerId;
        }
        
        private uint GetOwner(int x, int y)
        {
            if (!IsInBounds(x, y))
            {
                return 0;
            }
            return _cells[y * Width + x];
        }

        private bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
        
        private void DecodeRow(TerritoryRow row, List<TerritoryChange> changes)
        {
            int y = row.Y;
            int x = 0;

            foreach (var run in row.Runs)
            {
                uint ownerId = run.OwnerId;
                int count = (int)run.Count;

                for (int i = 0; i < count && x < Width; i++, x++)
                {
                    long idx = y * Width + x;
                    uint previousOwner = _cells[idx];

                    if (previousOwner != ownerId)
                    {
                        _cells[idx] = ownerId;
                        changes.Add(new TerritoryChange(x, y, previousOwner, ownerId));
                    }
                }
            }
        }

        private void RecalculateClaimedCells()
        {
            ClaimedCells = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != 0)
                {
                    ClaimedCells++;
                }
            }
        }
    }
}