using Game.Data;
using UnityEngine;

namespace Game.Effects.Implementations
{
    public class PlayerDeathEffect : MonoBehaviour, IEffect
    {
        [SerializeField] private Effect type;
        
        [Header("Scale Animation")]
        [SerializeField] private float scaleDuration = 0.3f;
        [SerializeField] private float maxScale = 1.5f;

        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleRenderer;
        private Material _particleMaterial;

        public Effect Type => type;
        public GameObject GameObject => this.gameObject;
        public bool IsPlaying => _particleSystem != null && _particleSystem.isPlaying;
        
        public void Prepare(IGameSessionDataProvider gameSessionData)
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public void Play(EffectData data)
        {
            var position = data.Position;
            var playerColor = data.Color;
            
            transform.position = position;
            
            if (_particleMaterial != null)
            {
                _particleMaterial.SetColor("_Color", playerColor);
                _particleMaterial.SetColor("_EmissionColor", playerColor * 2f);
            }

            var main = _particleSystem.main;
            main.startColor = playerColor;

            _particleSystem.Clear();
            _particleSystem.Play();
        }

        public void Stop()
        {
            if (_particleSystem != null && _particleSystem.isPlaying)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Reset()
        {
            if (_particleSystem != null)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _particleSystem.Clear();
            }
            
            transform.position = Vector3.zero;
            transform.localScale = Vector3.one;
        }
    }
}