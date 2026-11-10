using Core.Services;
using Game.Data;
using UnityEngine;
using Utils;

namespace Game.Subsystems
{
    public class CameraController : MonoBehaviour
    {
        [Header("Follow")] [SerializeField] private float height = 30f;
        [SerializeField] private float distance = 10f;
        [SerializeField] private float followSmoothTime = 0.15f;

        [Header("Zoom")] [SerializeField] private float minHeight = 20f;
        [SerializeField] private float maxHeight = 50f;
        [SerializeField] private float zoomSmoothTime = 0.5f;

        [Header("Look")] [SerializeField] private bool followDirection;
        [SerializeField] private float lookAheadDistance = 5f;
        [SerializeField] private float rotationSmoothTime = 0.3f;

        [Header("Bounds")] [SerializeField] private bool useBounds = true;
        [SerializeField] private float boundsPadding = 5f;

        private Camera _camera;
        private Transform _transform;

        private Transform _localTarget;

        private Vector3 _currentVelocity;
        private float _currentZoomVelocity;
        private float _currentRotationVelocity;
        private float _targetHeight;
        private float _currentYaw;

        private Bounds _gameBounds;
        private bool _hasBounds;

        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;

        private bool _isInitialized;

        public bool IsFollowing => _localTarget != null && _isInitialized;

        public void Initialize(uint gridWidth, uint gridHeight, float cellSize)
        {
            _transform = transform;
            _camera = GetComponent<Camera>();

            _targetHeight = height;
            _currentYaw = _transform.eulerAngles.y;

            _gameBounds = GridHelper.GetGridBounds(
                gridWidth,
                gridWidth,
                cellSize);

            _hasBounds = _gameBounds != default;
            _isInitialized = true;
            
            Debug.Log($"[CameraController] Initialized — height:{height} distance:{distance}");
        }

        public void SetLocalTarget(Transform localPlayerTransform)
        {
            if (localPlayerTransform == null)
            {
                Debug.LogWarning("[CameraController] SetLocalTarget called with null — ignoring.");
                return;
            }

            _localTarget = localPlayerTransform;
            SnapToTarget();

            Debug.Log($"[CameraController] Local target locked → {localPlayerTransform.name}");
        }

        public void TickLate()
        {
            if (!_isInitialized || _localTarget == null) return;

            height = Mathf.SmoothDamp(height, _targetHeight,
                ref _currentZoomVelocity, zoomSmoothTime);

            UpdateFollowPosition();

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                var t = _shakeTimer / Mathf.Max(_shakeDuration, 0.001f);
                var offset = Mathf.Sin(Time.unscaledTime * 40f) * _shakeIntensity * t;
                _transform.position += _transform.right * offset;
            }
        }

        public void Clear()
        {
            _localTarget = null;
            _isInitialized = false;
        }
        
        private void SnapToTarget()
        {
            if (_localTarget == null) return;

            _transform.position = CalculateDesiredPosition(_localTarget.position);
            _currentVelocity = Vector3.zero;
            LookAtTarget();
        }

        private void UpdateFollowPosition()
        {
            var desired = CalculateDesiredPosition(_localTarget.position);

            if (useBounds && _hasBounds)
                desired = ClampToBounds(desired);

            _transform.position = Vector3.SmoothDamp(
                _transform.position, desired,
                ref _currentVelocity, followSmoothTime);

            LookAtTarget();
        }

        private Vector3 CalculateDesiredPosition(Vector3 targetPos)
        {
            var yawRad = _currentYaw * Mathf.Deg2Rad;

            var offset = followDirection
                ? new Vector3(-Mathf.Sin(yawRad) * distance, height, -Mathf.Cos(yawRad) * distance)
                : new Vector3(0f, height, -distance);

            return targetPos + offset;
        }

        private void LookAtTarget()
        {
            if (_localTarget == null) return;

            var dir = _localTarget.position - _transform.position;
            if (dir.sqrMagnitude > 0.001f)
                _transform.rotation = Quaternion.LookRotation(dir);
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            if (_camera == null) return position;

            var frustumH = 2f * height * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var frustumW = frustumH * _camera.aspect;

            var halfW = frustumW * 0.5f + boundsPadding;
            var halfH = frustumH * 0.5f + boundsPadding;

            var minX = _gameBounds.min.x + halfW;
            var maxX = _gameBounds.max.x - halfW;
            var minZ = _gameBounds.min.z + halfH;
            var maxZ = _gameBounds.max.z - halfH;

            return new Vector3(
                Mathf.Clamp(position.x, minX, maxX),
                position.y,
                Mathf.Clamp(position.z, minZ, maxZ));
        }
    }
}