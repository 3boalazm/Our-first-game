using Game.Data;
using UnityEngine;

namespace Game.Effects.Implementations
{
    public class PlayerRespawnEffect : MonoBehaviour, IEffect
    {
        [SerializeField] private Effect type;
        [SerializeField] private GameObject ringObject;
        
        [Header("Glow Settings")]
        [SerializeField] private float glowIntensity = 3f;
        
        [Header("Ring Effect")]
        [SerializeField] private float ringExpandDuration = 0.5f;
        [SerializeField] private float ringMaxRadius = 3f;

        private ParticleSystem _glowParticles;
        private MeshRenderer _ringRenderer;
        private Material _ringMaterialInstance;
        private Coroutine _ringCoroutine;

        public Effect Type => type;
        public GameObject GameObject => this.gameObject;

        public bool IsPlaying
        {
            get
            {
                if (_glowParticles != null && _glowParticles.isPlaying)
                {
                    return true;
                }
                if (_ringCoroutine != null)
                {
                    return true;
                }
                return false;
            }
        }

        public void Prepare(IGameSessionDataProvider gameSessionData)
        { }

        public void Play(EffectData data)
        {
            var position = data.Position;
            var playerColor = data.Color;
            
            transform.position = position;

            if (_glowParticles != null)
            {
                var main = _glowParticles.main;
                Color glowColor = playerColor * glowIntensity;
                glowColor.a = 1f;
                main.startColor = glowColor;
                
                _glowParticles.Clear();
                _glowParticles.Play();
            }

            if (ringObject != null)
            {
                if (_ringCoroutine != null)
                {
                    StopCoroutine(_ringCoroutine);
                }
                _ringCoroutine = StartCoroutine(AnimateRing(playerColor));
            }
        }

        public void Stop()
        {
            if (_ringCoroutine != null)
            {
                StopCoroutine(_ringCoroutine);
                _ringCoroutine = null;
            }
            
            if (_glowParticles != null && _glowParticles.isPlaying)
            {
                _glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (ringObject != null)
            {
                ringObject.SetActive(false);
            }
        }

        public void Reset()
        {
            if (_ringCoroutine != null)
            {
                StopCoroutine(_ringCoroutine);
                _ringCoroutine = null;
            }
            
            if (_glowParticles != null)
            {
                _glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _glowParticles.Clear();
            }

            if (ringObject != null)
            {
                ringObject.SetActive(false);
                ringObject.transform.localScale = Vector3.one;
            }

            transform.position = Vector3.zero;
        }

        private System.Collections.IEnumerator AnimateRing(Color color)
        {
            ringObject.SetActive(true);
            
            if (_ringMaterialInstance == null)
            {
                _ringRenderer = ringObject.GetComponent<MeshRenderer>();
                if (_ringRenderer != null)
                {
                    _ringMaterialInstance = _ringRenderer.material;
                }
            }
            
            if (_ringMaterialInstance != null)
            {
                _ringMaterialInstance.color = new Color(color.r, color.g, color.b, 0.5f);
            }

            float elapsed = 0f;
            while (elapsed < ringExpandDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / ringExpandDuration;
                float easedProgress = Easing.ExpoOut(progress);

                float radius = ringMaxRadius * easedProgress;
                float alpha = 1f - progress;

                ringObject.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
                
                if (_ringMaterialInstance != null)
                {
                    Color ringColor = _ringMaterialInstance.color;
                    ringColor.a = alpha * 0.5f;
                    _ringMaterialInstance.color = ringColor;
                }

                yield return null;
            }

            ringObject.SetActive(false);
            _ringCoroutine = null;
        }
    }
}