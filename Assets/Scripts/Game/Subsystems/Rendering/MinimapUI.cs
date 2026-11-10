using System.Collections.Generic;
using Game.Data;
using TMPro;
using UnityEngine;
using Utils;

namespace Game.Subsystems.Rendering
{
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private GameWorldConfig config;
        
        [SerializeField] private Material indicatorMaterial;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private float gridPadding = 2f;
        [SerializeField] private float cameraHeight = 200f;
        [SerializeField] private float indicatorSize = 2.5f;
        [SerializeField] private float localIndicatorSize = 3.5f;
        [SerializeField] private float indicatorHeight = 5f;
        [SerializeField] private bool pulseLocalIndicator = true;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmplitude = 0.3f;
        [SerializeField] private Color viewBoundsColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private float viewBoundsLineWidth = 0.5f;
        [SerializeField] private int indicatorLayer = 31;

        [SerializeField] private TMP_Text percentageText;
        [SerializeField] private string percentageFormat = "{0:F1}%";
        
        private readonly Dictionary<uint, GameObject> _indicators = new();
        private readonly List<uint> _staleIndicators = new();

        private float _pulseTimer;

        private GameObject _viewBoundsObj;
        private LineRenderer _viewBoundsLine;
        private Camera _mainCamera;

        private bool _isInitialized;

        public RenderTexture MinimapTexture => minimapCamera != null ? minimapCamera.targetTexture : null;
        public bool IsReady => _isInitialized && MinimapTexture != null;
        
        private IGameSessionDataProvider _gameSessionDataProvider;
        private ITerritoryDataProvider _territoryDataProvider;
        private IPlayerVisualsDataProvider _playerVisualsDataProvider;
        private IColorDataProvider _colorDataProvider;
        public void Setup(
            IGameSessionDataProvider gameSessionDataProvider,
            ITerritoryDataProvider territoryDataProvider,
            IPlayerVisualsDataProvider playerVisualsDataProvider,
            IColorDataProvider colorDataProvider)
        {
            _gameSessionDataProvider = gameSessionDataProvider;
            _territoryDataProvider = territoryDataProvider;
            _playerVisualsDataProvider = playerVisualsDataProvider;
            _colorDataProvider = colorDataProvider;
            
            if (minimapCamera == null)
            {
                Debug.LogError("[MinimapSystem] No camera assigned! Drag your minimap camera into the Inspector.");
                return;
            }

            if (minimapCamera.targetTexture == null)
            {
                Debug.LogError("[MinimapSystem] Minimap camera has no Target Texture. " +
                               "Create a RenderTexture and assign it on the camera.");
                return;
            }

            if (minimapCamera.transform.parent != null && minimapCamera.transform.parent is RectTransform)
            {
                minimapCamera.transform.SetParent(null, true);
            }
            
            _gameSessionDataProvider.OnGameStarted += OnGameStarted;
        }

        public void Tick()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateIndicators();

            if (pulseLocalIndicator)
            {
                _pulseTimer += Time.deltaTime * pulseSpeed;
            }
        }

        public void TickLate()
        {
            if (!_isInitialized)
            {
                return;
            }
            
            UpdatePercentage();
        }
        
        public void Clear()
        {
            Cleanup();

            _gameSessionDataProvider.OnGameStarted -= OnGameStarted;
        }
        
        private void OnGameStarted()
        {
            FitCameraToGrid();

            // Ensure camera sees the indicator layer
            minimapCamera.cullingMask |= (1 << indicatorLayer);

            _isInitialized = true;
        }

        private void UpdatePercentage()
        {
            float pct = _territoryDataProvider.GetOwnershipPercentage(_gameSessionDataProvider.LocalPlayerId);
            percentageText.text = string.Format(percentageFormat, pct);
        }
        
        private void FitCameraToGrid()
        {
            float gridWorldWidth = _territoryDataProvider.Width * config.CellSize;
            float gridWorldHeight = _territoryDataProvider.Height * config.CellSize;

            Vector3 gridCenter = GridHelper.GetGridCenter(
                _territoryDataProvider.Width, _territoryDataProvider.Height, config.CellSize);

            minimapCamera.transform.position = new Vector3(gridCenter.x, cameraHeight, gridCenter.z);

            float verticalHalf = (gridWorldHeight + gridPadding * 2f) / 2f;
            float horizontalHalf = (gridWorldWidth + gridPadding * 2f) / 2f;

            minimapCamera.orthographicSize = Mathf.Max(verticalHalf, horizontalHalf);
        }

        private void UpdateIndicators()
        {
            var allVisuals = _playerVisualsDataProvider.ActiveVisuals;

            var activePlayers = new HashSet<uint>();

            foreach (var kvp in allVisuals)
            {
                uint playerId = kvp.Key;
                PlayerVisual visual = kvp.Value;
                activePlayers.Add(playerId);

                if (visual == null || !visual.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool isLocal = playerId == _gameSessionDataProvider.LocalPlayerId;

                if (!_indicators.TryGetValue(playerId, out var indicator) || indicator == null)
                {
                    indicator = CreateIndicator(playerId, isLocal);
                    _indicators[playerId] = indicator;
                }

                Vector3 playerPos = visual.transform.position;
                indicator.transform.position = new Vector3(playerPos.x, indicatorHeight, playerPos.z);

                if (isLocal && pulseLocalIndicator)
                {
                    float pulse = 1f + Mathf.Sin(_pulseTimer) * pulseAmplitude;
                    float size = localIndicatorSize * pulse;
                    indicator.transform.localScale = new Vector3(size, 0.1f, size);
                }
            }

            _staleIndicators.Clear();
            foreach (var kvp in _indicators)
            {
                if (!activePlayers.Contains(kvp.Key))
                {
                    _staleIndicators.Add(kvp.Key);
                }
            }

            foreach (uint staleId in _staleIndicators)
            {
                if (_indicators.TryGetValue(staleId, out var staleObj) && staleObj != null)
                {
                    Destroy(staleObj);
                }
                _indicators.Remove(staleId);
            }
        }

        private GameObject CreateIndicator(uint playerId, bool isLocal)
        {
            float size = isLocal ? localIndicatorSize : indicatorSize;
            Color color = _colorDataProvider.GetColorOf(playerId);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicator.name = $"MinimapIndicator_{playerId}";
            indicator.layer = indicatorLayer;
            indicator.transform.SetParent(transform);
            indicator.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            indicator.transform.localScale = new Vector3(size, size, 1f);

            var collider = indicator.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = indicator.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (indicatorMaterial != null)
            {
                renderer.material = new Material(indicatorMaterial);
                renderer.material.color = color;
            }
            else
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(mpb);
            }

            return indicator;
        }
        
        private void Cleanup()
        {
            foreach (var kvp in _indicators)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _indicators.Clear();

            if (_viewBoundsObj != null)
            {
                Destroy(_viewBoundsObj);
            }

            _isInitialized = false;
        }
    }
}