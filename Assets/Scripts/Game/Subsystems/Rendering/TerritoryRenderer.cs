using Core.Services;
using Game.Data;
using UnityEngine;

namespace Game.Subsystems.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerritoryRenderer : MonoBehaviour, IService
    {
        [Header("Configuration")]
        [SerializeField] private Material territoryMaterial;
        [SerializeField] private bool debugLogUpdates = false;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private int _totalCellsUpdated;

        public MeshFilter MeshFilter => _meshFilter;
        public Mesh SharedMesh => _mesh;
        public int TotalCellsUpdated => _totalCellsUpdated;
        public bool IsInitialized => _mesh != null;

        private ITerritoryVisualDataProvider _territoryVisualData;
        public void Initialize(ServiceContainer services)
        {
            _territoryVisualData = services.Get<TerritoryVisualData>();

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }
        public void Dispose()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        public void CreateTerritory()
        {
            if (territoryMaterial != null)
            {
                _meshRenderer.material = territoryMaterial;
            }

            CreateMeshFromVisualData();
        }

        public void FlushToMesh(int changeCount = 0)
        {
            if (!IsInitialized)
            {
                return;
            }

            _mesh.colors32 = _territoryVisualData.Colors;

            _totalCellsUpdated += changeCount;

            if (debugLogUpdates && changeCount > 0)
            {
                Debug.Log($"[TerritoryRenderer] Flushed {changeCount} cell changes to mesh");
            }
        }

        private void CreateMeshFromVisualData()
        {
            _mesh = new Mesh
            {
                name = "TerritoryMesh"
            };

            if (_territoryVisualData.VertexCount > 65535)
            {
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            _mesh.vertices = _territoryVisualData.Vertices;
            _mesh.triangles = _territoryVisualData.Triangles;
            _mesh.colors32 = _territoryVisualData.Colors;
            _mesh.uv = _territoryVisualData.UVs;

            _mesh.RecalculateNormals();
            _mesh.MarkDynamic();
            _mesh.RecalculateBounds();

            _meshFilter.mesh = _mesh;
        }
    }
}