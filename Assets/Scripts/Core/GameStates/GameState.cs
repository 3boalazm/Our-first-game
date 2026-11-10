using System;
using Core.Services;
using UnityEngine;

namespace Core.GameStates
{
    public abstract class GameState : MonoBehaviour
    {
        public abstract Action TriggerStateSwitch { get; set; }
        public bool Succeeded { get; protected set; } = true;
        public abstract void Initialize(ServiceContainer container);
        public abstract void Tick();
        public abstract void Stop();
    }
}