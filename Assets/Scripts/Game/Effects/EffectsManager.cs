using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Effects
{
    [Serializable]
    public struct EffectPrefab
    {
        [SerializeField] public UnityEngine.Object Prefab;
        public IEffect Effect => Prefab as IEffect;
    }
    
    public class EffectsManager : MonoBehaviour, ITickableService
    {
        [SerializeField] private List<EffectPrefab> effectPrefabs = new();
        [SerializeField] private int defaultPoolSize = 4;
        [SerializeField] private int maxPoolSize = 16;

        private readonly Dictionary<Effect, ObjectPool<IEffect>> _pools = new();
        private readonly Dictionary<Effect, UnityEngine.Object> _prefabMap = new();
        private readonly List<IEffect> _activeEffects = new();

        private Transform _effectsContainer;
        private IGameSessionDataProvider _gameSessionData;
        public void Initialize(ServiceContainer services)
        {
            _gameSessionData = services.Get<GameSessionData>();
            
            _effectsContainer = new GameObject("EffectsContainer").transform;
            _effectsContainer.SetParent(transform, false);
        }

        public void Tick()
        {
            ReturnFinishedEffects();
        }

        public void Dispose()
        {
            ClearAllEffects();
            
            foreach (var pool in _pools.Values)
            {
                pool.Dispose();
            }
            _pools.Clear();
            _prefabMap.Clear();
        }
        
        public void PreparePools()
        {
            foreach (var entry in effectPrefabs)
            {
                // Resolve the IEffect from the prefab to read its Type
                IEffect sample = (entry.Prefab as GameObject)?.GetComponent<IEffect>()
                                 ?? (entry.Prefab as Component)?.GetComponent<IEffect>();

                if (sample == null)
                {
                    Debug.LogError($"[EffectsManager] Prefab {entry.Prefab.name} does not implement IEffect!");
                    continue;
                }

                Effect effectType = sample.Type;

                if (_pools.ContainsKey(effectType))
                {
                    Debug.LogWarning($"[EffectsManager] Duplicate pool for {effectType}, skipping.");
                    continue;
                }

                _prefabMap[effectType] = entry.Prefab;

                Effect capturedType = effectType;

                var pool = new ObjectPool<IEffect>(
                    createFunc:       () => CreateEffect(capturedType),
                    actionOnGet:      OnGetEffect,
                    actionOnRelease:  OnReleaseEffect,
                    actionOnDestroy:  OnDestroyEffect,
                    collectionCheck:  false,
                    defaultCapacity:  defaultPoolSize,
                    maxSize:          maxPoolSize
                );

                _pools[effectType] = pool;

                var prewarmed = new List<IEffect>(defaultPoolSize);
                for (int i = 0; i < defaultPoolSize; i++)
                {
                    prewarmed.Add(pool.Get());
                }
                foreach (var effect in prewarmed)
                {
                    pool.Release(effect);
                }
            }
        }

        public void PlayEffect(Effect effectType, EffectData data)
        {
            if (!_pools.TryGetValue(effectType, out var pool))
            {
                Debug.LogWarning($"[EffectsManager] No pool for effect type {effectType}");
                return;
            }

            IEffect effect = pool.Get();
            effect.Play(data);
            _activeEffects.Add(effect);
        }

        private IEffect CreateEffect(Effect effectType)
        {
            if (!_prefabMap.TryGetValue(effectType, out var prefab))
            {
                Debug.LogError($"[EffectsManager] No prefab registered for {effectType}");
                return null;
            }

            var instance = Instantiate(prefab, _effectsContainer);
            IEffect effect = (instance as GameObject)?.GetComponent<IEffect>()
                             ?? (instance as Component)?.GetComponent<IEffect>();

            if (effect == null)
            {
                Debug.LogError($"[EffectsManager] Instantiated {instance.name} but no IEffect found!");
                return null;
            }

            effect.Prepare(_gameSessionData);
            effect.GameObject.SetActive(false);
            return effect;
        }

        private void OnGetEffect(IEffect effect)
        {
            if (effect?.GameObject != null)
            {
                effect.GameObject.SetActive(true);
            }
        }

        private void OnReleaseEffect(IEffect effect)
        {
            if (effect == null)
            {
                return;
            }
            
            effect.Stop();
            effect.Reset();
            
            if (effect.GameObject != null)
            {
                effect.GameObject.SetActive(false);
            }
        }

        private void OnDestroyEffect(IEffect effect)
        {
            if (effect?.GameObject != null)
            {
                Destroy(effect.GameObject);
            }
        }

        private void ReturnFinishedEffects()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                if (!effect.IsPlaying)
                {
                    _activeEffects.RemoveAt(i);
                    
                    if (_pools.TryGetValue(effect.Type, out var pool))
                    {
                        pool.Release(effect);
                    }
                }
            }
        }

        private void ClearAllEffects()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                _activeEffects.RemoveAt(i);
                
                if (_pools.TryGetValue(effect.Type, out var pool))
                {
                    pool.Release(effect);
                }
            }
        }
    }
}