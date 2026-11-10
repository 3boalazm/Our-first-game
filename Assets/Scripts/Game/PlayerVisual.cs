using Game.Data;
using Game.Paperio;
using Game.UI;
using Network;
using UnityEngine;

namespace Game
{
    public class PlayerVisual : MonoBehaviour
    {
        [Header("Visual Components")] 
        [SerializeField] private MeshRenderer bodyRenderer;

        [SerializeField] private NameLabel nameLabel;

        [Header("Configuration")] 
        [SerializeField] private float bodyScale = 0.8f;

        [SerializeField] private float deathFadeDuration = 0.5f;
        [SerializeField] private float respawnScaleDuration = 0.3f;
        [SerializeField] private float localPlayerScaleMultiplier = 1.1f;

        [Header("Interpolation")] 
        [SerializeField] private float renderDelayTicks = 1f;

        [SerializeField] private float maxExtrapolationTicks = 3f;
        [SerializeField] private int snapshotBufferSize = 10;

        [SerializeField] private Camera playerCamera;

        private MaterialPropertyBlock _propertyBlock;
        private Transform _transform;
        private uint _playerId;
        private bool _isLocalPlayer;
        private bool _isAlive = true;
        private Color _playerColor;
        private float _baseScale;

        private Vector3 _previousPosition;
        private Vector3 _targetPosition;

        private float _moveDuration;
        private float _moveProgress = 1f;
        private Vector3 _lerpStart;
        private Vector3 _lerpEnd;

        private InterpolationBuffer _interpolationBuffer;

        private Direction _currentDirection = Direction.None;

        private float _deathAnimationProgress;
        private float _respawnAnimationProgress;
        private bool _isPlayingDeathAnimation;
        private bool _isPlayingRespawnAnimation;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        public void SetMoveDuration(float moveDurationSeconds)
        {
            _moveDuration = Mathf.Max(0.05f, moveDurationSeconds);
        }

        public void Initialize(uint playerId, string playerName, Color color, Vector3 worldPosition, bool isLocalPlayer, float tickDurationSeconds = 0.05f)
        {
            _transform = transform;
            _propertyBlock = new MaterialPropertyBlock();
            _baseScale = bodyScale;

            _playerId = playerId;
            _isLocalPlayer = isLocalPlayer;
            _playerColor = color;
            _isAlive = true;

            SetColor(color);

            if (nameLabel != null)
            {
                nameLabel.Setup(playerName, isLocalPlayer);
            }

            var scale = _baseScale * (isLocalPlayer ? localPlayerScaleMultiplier : 1f);
            _transform.localScale = Vector3.one * scale;

            _lerpStart = worldPosition;
            _lerpEnd = worldPosition;
            _moveProgress = 1f;
            _transform.position = worldPosition;

            if (!isLocalPlayer)
            {
                _interpolationBuffer = new InterpolationBuffer(
                    snapshotBufferSize,
                    renderDelayTicks,
                    maxExtrapolationTicks,
                    tickDurationSeconds
                );
            }

            gameObject.SetActive(true);
            SetBodyVisible(true);

            playerCamera.gameObject.SetActive(_isLocalPlayer);

            Debug.Log($"[PlayerVisual] Initialized: {playerName} (ID: {playerId})" +
                      (isLocalPlayer ? " [LOCAL]" : ""));
        }

        public void UpdateFromData(PlayerData playerData, Vector3 worldPosition, uint tick = 0)
        {
            if (_isLocalPlayer)
            {
                _previousPosition = _targetPosition;
                _targetPosition = worldPosition;
            }
            else
            {
                _interpolationBuffer?.AddSnapshot(
                    tick,
                    worldPosition,
                    playerData.Direction,
                    playerData.Alive
                );
            }

            if (playerData.Direction != _currentDirection)
            {
                _currentDirection = playerData.Direction;
            }

            if (playerData.Color != _playerColor && playerData.Color != default)
            {
                _playerColor = playerData.Color;
                SetColor(_playerColor);
            }

            if (playerData.Alive != _isAlive)
            {
                if (playerData.Alive)
                {
                    OnRespawn(worldPosition);
                }
                else
                {
                    OnDeath();
                }
            }
        }

        public void UpdateInterpolation(float tickProgress)
        {
            if (_isPlayingDeathAnimation || _isPlayingRespawnAnimation)
            {
                UpdateAnimations();
                return;
            }

            if (_isLocalPlayer)
            {
                if (_moveProgress < 1f)
                {
                    _moveProgress += Time.deltaTime / _moveDuration;
                    _moveProgress = Mathf.Clamp01(_moveProgress);
                    _transform.position = Vector3.Lerp(_lerpStart, _lerpEnd, _moveProgress);
                }
                else
                {
                    _transform.position = _lerpEnd;
                }
            }
            else
            {
                if (_interpolationBuffer != null)
                {
                    var result = _interpolationBuffer.Sample(tickProgress);
                    if (result.IsValid)
                    {
                        _transform.position = result.Position;

                        if (result.Direction != _currentDirection)
                        {
                            _currentDirection = result.Direction;
                        }
                    }
                }
            }
        }

        public void SetPredictedTarget(Vector3 predictedWorldPosition)
        {
            if (!_isLocalPlayer) return;

            var dist = Vector3.Distance(_lerpEnd, predictedWorldPosition);
            
            // Same cell - ignore
            if (dist < 0.01f)
            {
                return;
            }

            _lerpStart = _transform.position;
            _lerpEnd = predictedWorldPosition;
            _moveProgress = 0f;
        }

        public void SnapToPosition(Vector3 worldPosition)
        {
            _lerpStart = worldPosition;
            _lerpEnd = worldPosition;
            _moveProgress = 1f;
            _transform.position = worldPosition;

            _interpolationBuffer?.Clear();
        }

        private void SetColor(Color color)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            _propertyBlock.SetColor(ColorProperty, color);
            _propertyBlock.SetColor(BaseColorProperty, color);
            bodyRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetBodyVisible(bool visible)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = visible;
            }
        }

        private void OnDeath()
        {
            _isAlive = false;
            _isPlayingDeathAnimation = true;
            _deathAnimationProgress = 0f;

            nameLabel.SetVisible(false);

            Debug.Log($"[PlayerVisual] Player {_playerId} death animation started");
        }

        private void OnRespawn(Vector3 newPosition)
        {
            _isAlive = true;
            _isPlayingDeathAnimation = false;
            _isPlayingRespawnAnimation = true;
            _respawnAnimationProgress = 0f;

            SnapToPosition(newPosition);
            SetBodyVisible(true);
            nameLabel.SetVisible(true);

            Debug.Log($"[PlayerVisual] Player {_playerId} respawn animation started");
        }

        private void UpdateAnimations()
        {
            if (_isPlayingDeathAnimation)
            {
                _deathAnimationProgress += Time.deltaTime / deathFadeDuration;

                if (_deathAnimationProgress >= 1f)
                {
                    _isPlayingDeathAnimation = false;
                    SetBodyVisible(false);
                }
                else
                {
                    var scale = _baseScale * (1f - _deathAnimationProgress);
                    _transform.localScale = Vector3.one * scale;

                    var fadedColor = _playerColor;
                    fadedColor.a = 1f - _deathAnimationProgress;
                    SetColor(fadedColor);
                }
            }
            else if (_isPlayingRespawnAnimation)
            {
                _respawnAnimationProgress += Time.deltaTime / respawnScaleDuration;

                if (_respawnAnimationProgress >= 1f)
                {
                    _isPlayingRespawnAnimation = false;
                    var finalScale = _baseScale * (_isLocalPlayer ? localPlayerScaleMultiplier : 1f);
                    _transform.localScale = Vector3.one * finalScale;
                    SetColor(_playerColor);
                }
                else
                {
                    var scale = _baseScale * _respawnAnimationProgress * (_isLocalPlayer ? localPlayerScaleMultiplier : 1f);
                    _transform.localScale = Vector3.one * scale;
                }
            }
        }

        public void ResetForPool()
        {
            _playerId = 0;
            _isLocalPlayer = false;
            _isAlive = true;
            _isPlayingDeathAnimation = false;
            _isPlayingRespawnAnimation = false;
            _currentDirection = Direction.None;
            _moveProgress = 1f;

            _interpolationBuffer?.Clear();
            _interpolationBuffer = null;

            _transform.localScale = Vector3.one * _baseScale;
            SetBodyVisible(true);

            nameLabel.ResetForPool();

            gameObject.SetActive(false);
        }
    }
}