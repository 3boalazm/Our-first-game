using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Subsystems;
using Game.Subsystems.Rendering;
using UnityEngine;

namespace Game.UI.Territory
{
    public class TerritoryClaimPopupManager : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private TerritoryClaimPopup popupPrefab;

        [Header("Pool")]
        [SerializeField] private int initialPoolSize = 4;
        [SerializeField] private int maxPoolSize = 10;

        [SerializeField] private int minCellsToShow = 3;

        private Canvas _overlayCanvas;

        private readonly Queue<TerritoryClaimPopup> _pool = new();
        private readonly List<TerritoryClaimPopup> _active = new();

        
        private Camera _localPlayerCamera;
        private ITerritoryEventsHandler _territoryEventsHandler;
        private ITerritoryDataProvider _territoryData;
        private IPlayerVisualsDataProvider _playerVisualsData;
        public void Setup(
            ITerritoryEventsHandler territoryEventsHandler,
            ITerritoryDataProvider territoryData,
            IGameSessionDataProvider gameSessionData,
            IPlayerVisualsDataProvider playerVisualsData)
        {
            _territoryData = territoryData;
            _territoryEventsHandler = territoryEventsHandler;
            _playerVisualsData = playerVisualsData;
            _localPlayerCamera = gameSessionData.LocalPlayerCamera;
            
            territoryEventsHandler.OnLocalClaim += ShowClaimPopup;
            CreateOverlayCanvas();
            PopulatePopupPool();
        }

        private void PopulatePopupPool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                var popup = CreatePopup();
                popup.gameObject.SetActive(false);
                _pool.Enqueue(popup);
            }
        }

        public void Tick()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_active[i].IsPlaying)
                {
                    var popup = _active[i];
                    popup.ResetForPool();
                    _pool.Enqueue(popup);
                    _active.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            _territoryEventsHandler.OnLocalClaim -= ShowClaimPopup;
            foreach (var popup in _active)
            {
                popup.ForceFinish();
            }
            _active.Clear();
            _pool.Clear();
        }

        private void ShowClaimPopup(int cellsClaimed, Color playerColor)
        {
            if (cellsClaimed < minCellsToShow)
            {
                return;
            }

            var localVisual = _playerVisualsData.LocalPlayerVisual;
            
            var popup = GetPopup();
            popup.Show(
                localVisual.transform,
                cellsClaimed,
                _territoryData.TotalCells,
                playerColor,
                _localPlayerCamera
            );
            _active.Add(popup);
        }

        private void CreateOverlayCanvas()
        {
            var canvasGO = new GameObject("ClaimPopupCanvas");
            canvasGO.transform.SetParent(transform);

            _overlayCanvas = canvasGO.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 100;

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        }

        private TerritoryClaimPopup GetPopup()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            if (_active.Count < maxPoolSize)
                return CreatePopup();

            var oldest = _active[0];
            oldest.ForceFinish();
            _active.RemoveAt(0);
            return oldest;
        }

        private TerritoryClaimPopup CreatePopup()
        {
            var popup = Instantiate(popupPrefab, _overlayCanvas.transform);
            popup.gameObject.SetActive(false);
            return popup;
        }
    }
}
