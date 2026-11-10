using System;
using Core.Services;
using Game.Client;
using Game.Paperio;
using Game.Server;
using Google.Protobuf;
using UnityEngine;

namespace Network
{
    public class NetworkManager : MonoBehaviour, ITickableService
    {
        [Header("Connection Settings")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 9000;
        [SerializeField] private string webSocketHost = "ws.paperio-demo.info";
        [SerializeField] private int webSocketPort = 9001;
        [SerializeField] private string playerName = "";
        [SerializeField] private string roomCode = "";

        [Header("Ping Settings")]
        [SerializeField] private float pingInterval = 1.0f;
        [SerializeField] private float connectionTimeout = 10.0f;

        private INetworkTransport _transport;
        private uint _sendSequence;
        private uint _pingSequence;
        private float _lastPingTime;
        private float _lastPongTime;
        private float _roundTripTime;

        private bool _isJoined;
        private uint _localPlayerId;
        private string _roomCode;
        private string _reconnectToken;

        public bool IsConnected => _transport?.IsConnected ?? false;
        public string RoomCode => _roomCode;

        public event Action<RoomJoined> OnRoomJoined;
        public event Action<RoomUpdate> OnRoomUpdate;
        public event Action<GameStarting> OnGameStarting;
        public event Action<Game.Server.GameMessage> OnGameMessage;
        public event Action<GameEnded> OnGameEnded;
        public event Action<PlayerLeft> OnPlayerLeft;
        public event Action<Game.Server.Error> OnError;
        public event Action<Pong> OnPong;
        public event Action<PlayerDisconnected> OnPlayerDisconnected;
        public event Action<PlayerReconnected> OnPlayerReconnected;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public event Action<PaperioState> OnPaperioStateReceived;
        public event Action<PaperioJoinResponse> OnPaperioJoinResponse;
        
        public void Initialize(ServiceContainer services)
        {
            MainThreadDispatcher.Initialize();

            #if UNITY_WEBGL && !UNITY_EDITOR
                _transport = new WebSocketClient();
            #else
                _transport = new UdpClient();
            #endif

            _transport.OnDataReceived += HandleDataReceived;
            _transport.OnError += HandleNetworkError;
            _transport.OnDisconnected += HandleDisconnected;
            _transport.OnConnected += HandleTransportConnected;
        }

        public void Tick()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
                // WebSocket callbacks arrive from JS — must be dispatched on main thread
                if (_transport is WebSocketClient wsClient)
                {
                    wsClient.ProcessCallbacks();
                }
            #endif
            if (IsConnected && _isJoined)
            {
                if (Time.time - _lastPingTime >= pingInterval)
                {
                    SendPing();
                }

                if (Time.time - _lastPongTime > connectionTimeout && _lastPongTime > 0)
                {
                    Debug.LogWarning("[NetworkManager] Connection timeout - no pong received");
                    Disconnect();
                }
            }
        }

        public void Dispose()
        {
            SendLeaveRoom();
            
            if (_transport != null)
            {
                _transport.OnDataReceived -= HandleDataReceived;
                _transport.OnError -= HandleNetworkError;
                _transport.OnDisconnected -= HandleDisconnected;
                _transport.OnConnected -= HandleTransportConnected; 
                _transport.Dispose();
            }
        }
        
        public void SetPlayerName(string name)
        {
            playerName = name;
        }

        public bool Connect()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
                string host = webSocketHost;
                int port = webSocketPort;
            #else
                string host = serverHost;
                int port = serverPort;
            #endif

            if (_transport.Connect(host, port))
            {
                _sendSequence = 0;
                _lastPongTime = Time.time;
                return true;
            }
            return false;
        }

        public void Disconnect()
        {
            _isJoined = false;
            _localPlayerId = 0;
            _roomCode = null;
            _reconnectToken = null;
            _transport.Disconnect();
        }

        public void SendJoinRoom()
        {
            if (!IsConnected)
            {
                Debug.LogError("[NetworkManager] Cannot join room: not connected");
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                JoinRoom = new JoinRoom
                {
                    RoomCode = roomCode,
                    PlayerName = playerName
                }
            };

            SendMessage(msg);
            Debug.Log($"[NetworkManager] Joining room: {(string.IsNullOrEmpty(roomCode) ? "new" : roomCode)}");
        }

        public void SendLeaveRoom()
        {
            if (!IsConnected || !_isJoined)
            {
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                LeaveRoom = new LeaveRoom()
            };

            SendMessage(msg);
            _isJoined = false;
            Debug.Log("[NetworkManager] Leaving room");
        }

        public void SendReady()
        {
            if (!IsConnected || !_isJoined)
            {
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                Ready = new Ready()
            };

            SendMessage(msg);
            Debug.Log("[NetworkManager] Sent ready signal");
        }

        public void SendReconnect(string token)
        {
            if (!IsConnected)
            {
                Debug.LogError("[NetworkManager] Cannot reconnect: not connected");
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                Reconnect = new Reconnect
                {
                    Token = token,
                    PlayerName = playerName
                }
            };

            SendMessage(msg);
            Debug.Log("[NetworkManager] Attempting reconnect");
        }

        public void SendGameInput(byte[] payload)
        {
            if (!IsConnected || !_isJoined) return;

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                GameMessage = new Game.Client.GameMessage
                {
                    Payload = ByteString.CopyFrom(payload)
                }
            };

            SendMessage(msg);
        }

        public void SendDirection(Direction direction)
        {
            var input = new PaperioInput { Direction = direction };
            
            using (var stream = new System.IO.MemoryStream())
            {
                input.WriteTo(stream);
                SendGameInput(stream.ToArray());
            }
        }

        private void HandleTransportConnected()
        {
            Debug.Log("[NetworkManager] Transport connected");
            OnConnected?.Invoke();
        }
        
        private void HandleDataReceived(byte[] data)
        {
            try
            {
                var serverMsg = new ServerMessage();
                serverMsg.MergeFrom(new CodedInputStream(data));

                _transport.UpdateReceivedSequence(serverMsg.Sequence);

                switch (serverMsg.PayloadCase)
                {
                    case ServerMessage.PayloadOneofCase.RoomJoined:
                        Debug.Log($"[NetworkManager] RoomJoined received! PlayerId={serverMsg.RoomJoined.PlayerId}, Room={serverMsg.RoomJoined.RoomCode}");
                        HandleRoomJoined(serverMsg.RoomJoined);
                        break;
                    case ServerMessage.PayloadOneofCase.RoomUpdate:
                        Debug.Log("[NetworkManager] RoomUpdate received");
                        OnRoomUpdate?.Invoke(serverMsg.RoomUpdate);
                        break;
                    case ServerMessage.PayloadOneofCase.GameStarting:
                        Debug.Log($"[NetworkManager] GameStarting received! Countdown={serverMsg.GameStarting.CountdownSeconds}");
                        OnGameStarting?.Invoke(serverMsg.GameStarting);
                        break;
                    case ServerMessage.PayloadOneofCase.GameMessage:
                        HandleGameMessage(serverMsg.GameMessage);
                        break;
                    case ServerMessage.PayloadOneofCase.GameEnded:
                        OnGameEnded?.Invoke(serverMsg.GameEnded);
                        break;
                    case ServerMessage.PayloadOneofCase.PlayerLeft:
                        Debug.Log($"[NetworkManager] PlayerLeft received! PlayerId={serverMsg.PlayerLeft.PlayerId}");
                        OnPlayerLeft?.Invoke(serverMsg.PlayerLeft);
                        break;
                    case ServerMessage.PayloadOneofCase.Error:
                        Debug.Log($"[NetworkManager] Error received: {serverMsg.Error.Message}");
                        HandleServerError(serverMsg.Error);
                        break;
                    case ServerMessage.PayloadOneofCase.Pong:
                        HandlePong(serverMsg.Pong);
                        break;
                    case ServerMessage.PayloadOneofCase.PlayerDisconnected:
                        Debug.Log($"[NetworkManager] PlayerDisconnected received! PlayerId={serverMsg.PlayerDisconnected.PlayerId}");
                        OnPlayerDisconnected?.Invoke(serverMsg.PlayerDisconnected);
                        break;
                    case ServerMessage.PayloadOneofCase.PlayerReconnected:
                        Debug.Log($"[NetworkManager] PlayerReconnected received! PlayerId={serverMsg.PlayerReconnected.PlayerId}");
                        OnPlayerReconnected?.Invoke(serverMsg.PlayerReconnected);
                        break;
                    case ServerMessage.PayloadOneofCase.None:
                        Debug.LogWarning("[NetworkManager] Received message with PayloadCase=None (empty payload!)");
                        Debug.LogWarning($"[NetworkManager] Raw bytes ({data.Length}): {BitConverter.ToString(data, 0, Math.Min(data.Length, 64))}");
                        break;
                    default:
                        Debug.LogWarning($"[NetworkManager] Unknown message type: {serverMsg.PayloadCase}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Error parsing message ({data.Length} bytes): {ex}");
                Debug.LogError($"[NetworkManager] Raw bytes: {BitConverter.ToString(data, 0, Math.Min(data.Length, 64))}");
            }
        }

        private void HandleRoomJoined(RoomJoined roomJoined)
        {
            _isJoined = true;
            _localPlayerId = roomJoined.PlayerId;
            _roomCode = roomJoined.RoomCode;
            _reconnectToken = roomJoined.ReconnectToken;

            Debug.Log($"[NetworkManager] Joined room {_roomCode} as player {_localPlayerId}");
            
            PlayerPrefs.SetString("ReconnectToken", _reconnectToken);
            PlayerPrefs.Save();

            OnRoomJoined?.Invoke(roomJoined);
        }

        private void HandleGameMessage(Game.Server.GameMessage gameMessage)
        {
            OnGameMessage?.Invoke(gameMessage);

            TryParsePaperioMessage(gameMessage.Payload.ToByteArray());
        }

        private void TryParsePaperioMessage(byte[] payload)
        {
            try
            {
                var state = new PaperioState();
                state.MergeFrom(new CodedInputStream(payload));
                
                if (state.GridWidth > 0)
                {
                    OnPaperioStateReceived?.Invoke(state);
                    return;
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                var response = new PaperioJoinResponse();
                response.MergeFrom(new CodedInputStream(payload));
                
                if (response.YourPlayerId > 0)
                {
                    OnPaperioJoinResponse?.Invoke(response);
                    return;
                }
            }
            catch
            {
                // ignored
            }
        }

        private void HandleServerError(Error error)
        {
            Debug.LogError($"[NetworkManager] Server error: {error.Message}");
            OnError?.Invoke(error);
        }

        private void HandlePong(Pong pong)
        {
            _lastPongTime = Time.time;
            
            ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _roundTripTime = (float)(now - pong.Timestamp);

            OnPong?.Invoke(pong);
        }

        private void HandleNetworkError(string error)
        {
            Debug.LogError($"[NetworkManager] Network error: {error}");
        }

        private void HandleDisconnected()
        {
            _isJoined = false;
            OnDisconnected?.Invoke();
            Debug.Log("[NetworkManager] Disconnected from server");
        }

        private void SendMessage(ClientMessage msg)
        {
            try
            {
                using (var stream = new System.IO.MemoryStream())
                {
                    msg.WriteTo(stream);
                    _transport.Send(stream.ToArray());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Error sending message: {ex}");
            }
        }

        private void SendPing()
        {
            _pingSequence++;
            _lastPingTime = Time.time;

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                Ping = new Game.Client.Ping()
                {
                    Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Sequence = _pingSequence,
                }
            };

            SendMessage(msg);
        }

        private uint NextSequence()
        {
            return ++_sendSequence;
        }
    }
}
