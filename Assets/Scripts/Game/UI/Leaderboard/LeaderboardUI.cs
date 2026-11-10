using System;
using System.Collections.Generic;
using Game.Data;
using Game.Paperio;
using Game.Subsystems;
using UnityEngine;

namespace Game.UI.Leaderboard
{
    public class LeaderboardUI : MonoBehaviour
    {
        [SerializeField] private LeaderboardRow rowPrefab;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private int maxRows = 5;
        [SerializeField] private int refreshEveryTicks = 4;

        private LeaderboardRow[] _rows;
        private uint _localPlayerId;
        private uint _totalCells;
        private int _ticksSinceRefresh;
        private bool _isBound;

        private readonly List<LeaderboardEntry> _sortBuffer = new(16);

        private void Awake()
        {
            BuildRows();
        }

        private IGameStateReceiver _stateReceiver;
        private IColorDataProvider _colorData;
        private ITerritoryDataProvider _territoryData;
        private IGameSessionDataProvider _sessionDataProvider;
        public void Setup(
            IGameStateReceiver stateReceiver, 
            IColorDataProvider colorData, 
            ITerritoryDataProvider territoryData,
            IGameSessionDataProvider sessionDataProvider)
        {
            _stateReceiver = stateReceiver;
            _colorData = colorData;
            _territoryData = territoryData;
            _sessionDataProvider = sessionDataProvider;
            
            _localPlayerId = _sessionDataProvider.LocalPlayerId;
            _totalCells = _territoryData.Width * _territoryData.Height;

            _stateReceiver.OnStateProcessed += HandleStateUpdated;
            _isBound = true;
        }

        public void Clear()
        {
            if (!_isBound)
            {
                return;
            }

            if (_stateReceiver != null)
            {
                _stateReceiver.OnStateProcessed -= HandleStateUpdated;
            }

            _stateReceiver = null;
            _colorData = null;
            _sessionDataProvider = null;
            _isBound = false;
        }

        private void BuildRows()
        {
            foreach (Transform child in rowContainer)
            {
                Destroy(child.gameObject);
            }

            _rows = new LeaderboardRow[maxRows];
            for (var i = 0; i < maxRows; i++)
            {
                _rows[i] = Instantiate(rowPrefab, rowContainer);
                _rows[i].gameObject.SetActive(false);
            }
        }

        private void HandleStateUpdated(PaperioState state)
        {
            _ticksSinceRefresh++;
            if (_ticksSinceRefresh < refreshEveryTicks)
            {
                return;
            }
            _ticksSinceRefresh = 0;

            BuildSortBuffer(state);
            RefreshRows();
        }

        private void BuildSortBuffer(PaperioState state)
        {
            _sortBuffer.Clear();

            var divisor = _totalCells > 0 ? _totalCells : 1f;

            foreach (var player in state.Players)
            {
                if (!player.Alive)
                {
                    continue;
                }

                var pct = player.Score / divisor * 100f;
                var color = _colorData.GetColorOf(player.PlayerId);

                _sortBuffer.Add(new LeaderboardEntry(
                    player.PlayerId,
                    player.Name,
                    pct,
                    color,
                    player.PlayerId == _localPlayerId
                ));
            }

            _sortBuffer.Sort((a, b) => b.Percentage.CompareTo(a.Percentage));
        }
        
        private void RefreshRows()
        {
            var visible = Mathf.Min(_sortBuffer.Count, maxRows);

            for (var i = 0; i < maxRows; i++)
            {
                if (i < visible)
                {
                    _rows[i].gameObject.SetActive(true);
                    _rows[i].Populate(i + 1, _sortBuffer[i]);
                }
                else
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }
    }
}