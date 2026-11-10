using System.Collections.Generic;
using UnityEngine;

namespace Game.Subsystems.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TrailRenderer : MonoBehaviour
    {
        [Header("Trail Configuration")]
        [SerializeField] private float trailWidth = 0.4f;
        [SerializeField] private float trailHeight = 0.3f;
        [SerializeField] private float cornerRadius = 0.1f;
        [SerializeField] private int cornerSegments = 4;
        
        [Header("Materials")]
        [SerializeField] private Material trailMaterial;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private readonly List<Vector3> _vertices = new();
        private readonly List<int> _triangles = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Color32> _colors = new();

        private List<Vector3> _currentPoints = new();
        private Color32 _currentColor = Color.white;
        private float _cellSize = 1f;
        private bool _isDirty;

        public bool IsVisible => _meshRenderer != null && _meshRenderer.enabled;
        public int SegmentCount => _currentPoints.Count > 1 ? _currentPoints.Count - 1 : 0;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "TrailMesh" };
            _mesh.MarkDynamic();
            _meshFilter.mesh = _mesh;

            if (trailMaterial != null)
            {
                _meshRenderer.material = trailMaterial;
            }

            SetVisible(false);
        }

        public void Initialize(float cellSize, Material material = null)
        {
            _cellSize = cellSize;
            
            if (material != null)
            {
                trailMaterial = material;
                _meshRenderer.material = trailMaterial;
            }
        }

        public void SetColor(Color color)
        {
            _currentColor = color;
            _isDirty = true;
        }

        public void UpdateTrail(List<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                ClearTrail();
                return;
            }

            _currentPoints.Clear();
            _currentPoints.AddRange(worldPoints);
            _isDirty = true;
            
            SetVisible(true);
        }

        public void UpdateTrailFromGrid(List<Vector2Int> gridPoints, float heightOffset = 0.15f)
        {
            if (gridPoints == null || gridPoints.Count < 2)
            {
                ClearTrail();
                return;
            }

            _currentPoints.Clear();
            foreach (var gridPoint in gridPoints)
            {
                Vector3 worldPos = new Vector3(
                    (gridPoint.x + 0.5f) * _cellSize,
                    heightOffset,
                    (gridPoint.y + 0.5f) * _cellSize
                );
                _currentPoints.Add(worldPos);
            }

            _isDirty = true;
            SetVisible(true);
        }

        public void ClearTrail()
        {
            _currentPoints.Clear();
            _mesh.Clear();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_isDirty)
            {
                RebuildMesh();
                _isDirty = false;
            }
        }

        private void RebuildMesh()
        {
            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();
            _colors.Clear();

            if (_currentPoints.Count < 2)
            {
                _mesh.Clear();
                return;
            }

            float totalLength = 0f;
            for (int i = 1; i < _currentPoints.Count; i++)
            {
                totalLength += Vector3.Distance(_currentPoints[i - 1], _currentPoints[i]);
            }

            float currentLength = 0f;

            for (int i = 0; i < _currentPoints.Count - 1; i++)
            {
                Vector3 start = _currentPoints[i];
                Vector3 end = _currentPoints[i + 1];
                float segmentLength = Vector3.Distance(start, end);

                Vector3 direction = (end - start).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 up = Vector3.up;

                float uvStart = currentLength / totalLength;
                float uvEnd = (currentLength + segmentLength) / totalLength;

                int baseIndex = _vertices.Count;

                float halfWidth = trailWidth * 0.5f;

                _vertices.Add(start - right * halfWidth);
                _vertices.Add(start + right * halfWidth);
                _vertices.Add(start - right * halfWidth + up * trailHeight);
                _vertices.Add(start + right * halfWidth + up * trailHeight);

                _vertices.Add(end - right * halfWidth);
                _vertices.Add(end + right * halfWidth);
                _vertices.Add(end - right * halfWidth + up * trailHeight);
                _vertices.Add(end + right * halfWidth + up * trailHeight);

                _uvs.Add(new Vector2(0f, uvStart));
                _uvs.Add(new Vector2(1f, uvStart));
                _uvs.Add(new Vector2(0f, uvStart));
                _uvs.Add(new Vector2(1f, uvStart));
                _uvs.Add(new Vector2(0f, uvEnd));
                _uvs.Add(new Vector2(1f, uvEnd));
                _uvs.Add(new Vector2(0f, uvEnd));
                _uvs.Add(new Vector2(1f, uvEnd));

                for (int j = 0; j < 8; j++)
                {
                    _colors.Add(_currentColor);
                }

                _triangles.Add(baseIndex + 0);
                _triangles.Add(baseIndex + 4);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 4);
                _triangles.Add(baseIndex + 5);

                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 3);
                _triangles.Add(baseIndex + 6);
                _triangles.Add(baseIndex + 3);
                _triangles.Add(baseIndex + 7);
                _triangles.Add(baseIndex + 6);

                _triangles.Add(baseIndex + 0);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 4);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 6);
                _triangles.Add(baseIndex + 4);

                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 5);
                _triangles.Add(baseIndex + 3);
                _triangles.Add(baseIndex + 3);
                _triangles.Add(baseIndex + 5);
                _triangles.Add(baseIndex + 7);

                if (i == 0)
                {
                    _triangles.Add(baseIndex + 0);
                    _triangles.Add(baseIndex + 1);
                    _triangles.Add(baseIndex + 2);
                    _triangles.Add(baseIndex + 1);
                    _triangles.Add(baseIndex + 3);
                    _triangles.Add(baseIndex + 2);
                }

                if (i == _currentPoints.Count - 2)
                {
                    _triangles.Add(baseIndex + 4);
                    _triangles.Add(baseIndex + 6);
                    _triangles.Add(baseIndex + 5);
                    _triangles.Add(baseIndex + 5);
                    _triangles.Add(baseIndex + 6);
                    _triangles.Add(baseIndex + 7);
                }

                currentLength += segmentLength;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        public void SetVisible(bool visible)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = visible;
            }
        }

        public void SetEmissionIntensity(float intensity)
        {
            if (_meshRenderer == null || _meshRenderer.material == null) return;

            Color emissionColor = (Color)_currentColor * intensity;
            _meshRenderer.material.SetColor("_EmissionColor", emissionColor);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}