using Game.Data;
using UnityEngine;

namespace Game.Effects
{
    public interface IEffect
    {
        Effect Type { get; }
        public GameObject GameObject { get; }
        public bool IsPlaying { get; }
        void Prepare(IGameSessionDataProvider gameSessionData);
        void Play(EffectData data);
        void Reset();
        void Stop();
    }
}