// WebGL-compatible transport using browser-native WebSocket via JS plugin.
// Implements INetworkTransport so NetworkManager can use it identically
// to UdpClient — same events, same methods, same behavior contract.

#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Network
{
    public class WebSocketClient : INetworkTransport
    {
        // ── JS plugin imports ──
        [DllImport("__Internal")] private static extern void WebSocket_Initialize(
            Action<int> onOpen, Action<int, int> onClose,
            Action<int, IntPtr> onError, Action<int, IntPtr, int> onMessage);
        [DllImport("__Internal")] private static extern int WebSocket_Connect(string url);
        [DllImport("__Internal")] private static extern int WebSocket_Send(int id, byte[] buffer, int length);
        [DllImport("__Internal")] private static extern void WebSocket_Close(int id);
        [DllImport("__Internal")] private static extern int WebSocket_GetState(int id);

        // ── Singleton bridge for static JS callbacks → instance ──
        // JS callbacks must be static (MonoPInvokeCallback), so we route
        // through a static dictionary to find the right instance.
        private static readonly Dictionary<int, WebSocketClient> _instances = new();
        private static bool _pluginInitialized;

        private int _wsId = -1;
        private bool _isConnected;
        private bool _isDisposed;

        private uint _sendSequence;
        private uint _lastReceivedSequence;

        // Queue messages received in JS callbacks to process on main thread
        private readonly Queue<byte[]> _receivedQueue = new();
        private readonly Queue<string> _errorQueue = new();
        private bool _pendingDisconnect;
        private bool _pendingConnect;

        public bool IsConnected => _isConnected && !_isDisposed;
        public uint SendSequence => _sendSequence;
        public uint LastReceivedSequence => _lastReceivedSequence;

        public event Action<byte[]> OnDataReceived;
        public event Action<string> OnError;
        public event Action OnDisconnected;
        public event Action OnConnected;

        public WebSocketClient()
        {
            if (!_pluginInitialized)
            {
                WebSocket_Initialize(OnOpenStatic, OnCloseStatic, OnErrorStatic, OnMessageStatic);
                _pluginInitialized = true;
            }
        }

        /// <summary>
        /// Must be called every frame (e.g., from NetworkManager.Tick)
        /// to dispatch queued events on the main thread.
        /// </summary>
        public void ProcessCallbacks()
        {
            // Process connect
            if (_pendingConnect)
            {
                _pendingConnect = false;
                _isConnected = true;
                OnConnected?.Invoke();
            }

            // Process received data
            lock (_receivedQueue)
            {
                while (_receivedQueue.Count > 0)
                {
                    var data = _receivedQueue.Dequeue();
                    OnDataReceived?.Invoke(data);
                }
            }

            // Process errors
            lock (_errorQueue)
            {
                while (_errorQueue.Count > 0)
                {
                    var err = _errorQueue.Dequeue();
                    OnError?.Invoke(err);
                }
            }

            // Process disconnect
            if (_pendingDisconnect)
            {
                _pendingDisconnect = false;
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        public bool Connect(string host, int port)
        {
            if (_isDisposed)
            {
                OnError?.Invoke("Cannot connect: client has been disposed");
                return false;
            }

            Disconnect();

            // WebSocket URL — use ws:// for dev, wss:// for production
            string protocol = (host == "127.0.0.1" || host == "localhost") ? "ws" : "wss";
            string url = $"{protocol}://{host}:{port}";

            Debug.Log($"[WebSocketClient] Connecting to {url}");

            _wsId = WebSocket_Connect(url);
            if (_wsId < 0)
            {
                OnError?.Invoke("WebSocket connection failed");
                return false;
            }

            _instances[_wsId] = this;
            _sendSequence = 0;
            _lastReceivedSequence = 0;

            // WebSocket connect is async — we return true to indicate the
            // attempt started successfully. _isConnected will be set to true
            // when the OnOpen callback fires (via ProcessCallbacks).
            // Callers should listen for OnConnected before sending data.
            return true;
        }

        public void Disconnect()
        {
            if (_wsId >= 0)
            {
                _instances.Remove(_wsId);
                WebSocket_Close(_wsId);
                _wsId = -1;
            }

            if (_isConnected)
            {
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        public bool Send(byte[] data)
        {
            if (!IsConnected || _wsId < 0)
            {
                OnError?.Invoke("Cannot send: not connected");
                return false;
            }

            int result = WebSocket_Send(_wsId, data, data.Length);
            if (result == 0)
            {
                OnError?.Invoke("Send failed: WebSocket not ready");
                return false;
            }

            return true;
        }

        public int UpdateReceivedSequence(uint sequence)
        {
            if (sequence == 0)
            {
                _lastReceivedSequence = sequence;
                return 0;
            }

            uint expected = _lastReceivedSequence + 1;
            int gap = 0;

            if (sequence > expected)
            {
                gap = (int)(sequence - expected);
                Debug.LogWarning($"[WebSocketClient] Sequence gap: {gap} messages missing");
            }
            else if (sequence < expected && sequence != 1)
            {
                Debug.Log($"[WebSocketClient] Out-of-order: seq={sequence}, expected={expected}");
                return -1;
            }

            _lastReceivedSequence = sequence;
            return gap;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Disconnect();
        }

        // ── Static callbacks from JS plugin ──────────────────────────────

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnOpenStatic(int id)
        {
            if (_instances.TryGetValue(id, out var client))
            {
                client._pendingConnect = true;
                Debug.Log($"[WebSocketClient] Connected (id={id})");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnCloseStatic(int id, int code)
        {
            if (_instances.TryGetValue(id, out var client))
            {
                client._pendingDisconnect = true;
                Debug.Log($"[WebSocketClient] Closed (id={id}, code={code})");
            }
            _instances.Remove(id);
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr>))]
        private static void OnErrorStatic(int id, IntPtr msgPtr)
        {
            string msg = Marshal.PtrToStringUTF8(msgPtr) ?? "Unknown error";
            if (_instances.TryGetValue(id, out var client))
            {
                lock (client._errorQueue)
                {
                    client._errorQueue.Enqueue(msg);
                }
            }
            Debug.LogError($"[WebSocketClient] Error (id={id}): {msg}");
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        private static void OnMessageStatic(int id, IntPtr bufferPtr, int length)
        {
            if (_instances.TryGetValue(id, out var client))
            {
                byte[] data = new byte[length];
                Marshal.Copy(bufferPtr, data, 0, length);

                lock (client._receivedQueue)
                {
                    client._receivedQueue.Enqueue(data);
                }
            }
        }
    }
}
#endif