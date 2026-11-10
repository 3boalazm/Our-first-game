using Game.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Effects.Implementations
{
    public class ScreenShake : MonoBehaviour, IEffect
    {
        [SerializeField] private Effect type;
        
        [Header("Default Settings")]
        [SerializeField] private float defaultIntensity = 0.5f;
        [SerializeField] private float defaultDuration = 0.3f;
        [SerializeField] private float defaultFrequency = 25f;
        
        [Header("Decay")]
        [SerializeField] private AnimationCurve decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        
        [Header("Constraints")]
        [SerializeField] private bool shakeX = true;
        [SerializeField] private bool shakeY = true;
        [SerializeField] private bool shakeZ = true;
        [SerializeField] private float maxOffset = 2f;

        private Transform _targetTransform;
        private Vector3 _originalLocalPosition;
        private float _currentDuration;
        private float _currentIntensity;
        private float _currentFrequency;
        private float _elapsed;
        private bool _isShaking;
        private int _seed;

        public Effect Type => type;
        public GameObject GameObject => this.gameObject;
        public bool IsPlaying => _isShaking;
        
        public void Prepare(IGameSessionDataProvider gameSessionData)
        {
            _targetTransform = gameSessionData.LocalPlayerCamera.transform;
            _originalLocalPosition = _targetTransform.localPosition;
            _seed = Random.Range(0, 1000);
        }

        public void Play(EffectData data)
        {
            Shake(defaultIntensity, defaultDuration, defaultFrequency);
        }
        
        public void Stop()
        {
            _isShaking = false;
            _elapsed = 0f;
            
            if (_targetTransform != null)
            {
                _targetTransform.localPosition = _originalLocalPosition;
            }
        }

        public void Reset()
        {
            _isShaking = false;
            _elapsed = 0f;
            _currentDuration = 0f;
            _currentIntensity = 0f;
            _currentFrequency = 0f;
            
            if (_targetTransform != null)
            {
                _targetTransform.localPosition = _originalLocalPosition;
            }
        }

        private void Shake(float intensity, float duration, float frequency)
        {
            if (intensity > _currentIntensity || !_isShaking)
            {
                _currentIntensity = intensity;
                _currentDuration = duration;
                _currentFrequency = frequency;
                _elapsed = 0f;
                _isShaking = true;
                _seed = Random.Range(0, 1000);
            }
        }

        private void LateUpdate()
        {
            if (!_isShaking || _targetTransform == null)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            
            if (_elapsed >= _currentDuration)
            {
                Stop();
                return;
            }

            float progress = _elapsed / _currentDuration;
            float decayMultiplier = decayCurve.Evaluate(progress);
            float currentIntensity = _currentIntensity * decayMultiplier;

            float time = _elapsed * _currentFrequency;
            
            Vector3 offset = new Vector3(
                shakeX ? PerlinNoise(time, 0f) * currentIntensity : 0f,
                shakeY ? PerlinNoise(time, 100f) * currentIntensity * 0.5f : 0f,
                shakeZ ? PerlinNoise(time, 200f) * currentIntensity : 0f
            );

            offset = ClampOffset(offset);
            _targetTransform.localPosition = _originalLocalPosition + offset;
        }

        private float PerlinNoise(float x, float offset)
        {
            return (Mathf.PerlinNoise(_seed + x + offset, _seed + offset) - 0.5f) * 2f;
        }

        private Vector3 ClampOffset(Vector3 offset)
        {
            return new Vector3(
                Mathf.Clamp(offset.x, -maxOffset, maxOffset),
                Mathf.Clamp(offset.y, -maxOffset, maxOffset),
                Mathf.Clamp(offset.z, -maxOffset, maxOffset)
            );
        }
    }
}