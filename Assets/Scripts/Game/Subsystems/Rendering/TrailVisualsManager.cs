using System.Collections.Generic;
using Core.Services;
using Game.Data;
using UnityEngine;

namespace Game.Subsystems.Rendering
{
    public class TrailVisualsManager : MonoBehaviour, ITickableService
    {
        [Header("Configuration")]
        [SerializeField] private Material trailMaterial;
        [SerializeField] private int initialPoolSize = 8;
        [SerializeField] private float trailHeightOffset = 0.15f;
        
        [Header("Glow Settings")]
        [SerializeField] private float baseEmission = 1.5f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseIntensity = 0.5f;
        [SerializeField] private bool enablePulse = true;

        private readonly Dictionary<uint, TrailRenderer> _activeTrails = new();
        private readonly Queue<TrailRenderer> _pool = new();
        private Transform _container;

        public int ActiveTrailCount => _activeTrails.Count;

        private GameWorldConfigProvider _configProvider;
        public void Initialize(ServiceContainer services)
        {
            _configProvider = services.Get<GameWorldConfigProvider>();
            
            _container = new GameObject("TrailContainer").transform;
            _container.SetParent(transform, false);

            for (int i = 0; i < initialPoolSize; i++)
            {
                var trail = CreateTrailRenderer();
                trail.gameObject.SetActive(false);
                _pool.Enqueue(trail);
            }
        }

        public void Tick()
        {
            if (!enablePulse)
            {
                return;
            }

            float pulse = baseEmission + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            
            foreach (var kvp in _activeTrails)
            {
                if (kvp.Value.IsVisible)
                {
                    kvp.Value.SetEmissionIntensity(pulse);
                }
            }
        }

        public void Dispose()
        {
            ClearAllTrails();
            
            while (_pool.Count > 0)
            {
                var trail = _pool.Dequeue();
                if (trail != null)
                {
                    Destroy(trail.gameObject);
                }
            }
        }

        public void UpdatePlayerTrail(uint playerId, List<Vector2Int> gridPoints, Color playerColor)
        {
            if (gridPoints == null || gridPoints.Count < 2)
            {
                RemoveTrail(playerId);
                return;
            }

            if (!_activeTrails.TryGetValue(playerId, out var trail))
            {
                trail = GetFromPool();
                trail.gameObject.SetActive(true);
                _activeTrails[playerId] = trail;
            }

            Color trailColor = new Color(
                Mathf.Min(1f, playerColor.r * 1.2f),
                Mathf.Min(1f, playerColor.g * 1.2f),
                Mathf.Min(1f, playerColor.b * 1.2f),
                0.9f
            );

            trail.SetColor(trailColor);
            trail.UpdateTrailFromGrid(gridPoints, trailHeightOffset);
        }

        public void RemoveTrail(uint playerId)
        {
            if (!_activeTrails.TryGetValue(playerId, out var trail)) return;

            trail.ClearTrail();
            trail.gameObject.SetActive(false);
            _pool.Enqueue(trail);
            _activeTrails.Remove(playerId);
        }

        private void ClearAllTrails()
        {
            foreach (var kvp in _activeTrails)
            {
                kvp.Value.ClearTrail();
                kvp.Value.gameObject.SetActive(false);
                _pool.Enqueue(kvp.Value);
            }
            _activeTrails.Clear();
        }

        private TrailRenderer GetFromPool()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
            return CreateTrailRenderer();
        }

        private TrailRenderer CreateTrailRenderer()
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(_container, false);

            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();
            var trail = go.AddComponent<TrailRenderer>();

            if (trailMaterial != null)
            {
                meshRenderer.material = new Material(trailMaterial);
            }

            trail.Initialize(_configProvider.Config.CellSize, meshRenderer.material);
            
            return trail;
        }

        public TrailRenderer GetTrailForPlayer(uint playerId)
        {
            _activeTrails.TryGetValue(playerId, out var trail);
            return trail;
        }
    }
}