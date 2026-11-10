using System;
using UnityEngine;

namespace Network
{
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly System.Collections.Generic.Queue<Action> ExecutionQueue = new();

        public static void Enqueue(Action action)
        {
            lock (ExecutionQueue)
            {
                ExecutionQueue.Enqueue(action);
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            lock (ExecutionQueue)
            {
                while (ExecutionQueue.Count > 0)
                {
                    try
                    {
                        ExecutionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] Error executing action: {ex}");
                    }
                }
            }
        }

        public static void Initialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("[MainThreadDispatcher]");
                _instance = go.AddComponent<MainThreadDispatcher>();
            }
        }
    }
}