using System;
using Core.Services;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameReconnect : GameState
    {
        public override Action TriggerStateSwitch { get; set; }

        private NetworkManager _networkManager;
        private bool _isWaitingForResponse;
        private float _timeoutTimer;

        private const float ReconnectTimeoutSeconds = 5f;
        private const string ReconnectTokenKey = "ReconnectToken";
        private const string ReconnectRoomKey = "ReconnectRoomCode";

        public override void Initialize(ServiceContainer container)
        {
            _networkManager = container.Get<NetworkManager>();
            _isWaitingForResponse = false;
            _timeoutTimer = 0f;
            Succeeded = true;

            string savedToken = PlayerPrefs.GetString(ReconnectTokenKey, "");
            string savedRoom = PlayerPrefs.GetString(ReconnectRoomKey, "");

            if (string.IsNullOrEmpty(savedToken))
            {
                Debug.Log("[GameReconnect] No saved reconnect token, falling back to join");
                Succeeded = false;
                TriggerStateSwitch?.Invoke();
                return;
            }

            if (!string.IsNullOrEmpty(_networkManager.RoomCode) &&
                !string.IsNullOrEmpty(savedRoom) &&
                _networkManager.RoomCode != savedRoom)
            {
                Debug.Log($"[GameReconnect] Room mismatch (saved: {savedRoom}, target: {_networkManager.RoomCode}), falling back to join");
                ClearSavedToken();
                Succeeded = false;
                TriggerStateSwitch?.Invoke();
                return;
            }

            Debug.Log($"[GameReconnect] Attempting reconnect with saved token for room {savedRoom}");

            _networkManager.OnRoomJoined += OnRoomJoined;
            _networkManager.OnError += OnError;

            _networkManager.SendReconnect(savedToken);
            _isWaitingForResponse = true;
            _timeoutTimer = 0f;
        }

        public override void Tick()
        {
            if (!_isWaitingForResponse)
            {
                return;
            }

            _timeoutTimer += Time.deltaTime;
            if (_timeoutTimer >= ReconnectTimeoutSeconds)
            {
                Debug.LogWarning("[GameReconnect] Reconnect timed out");
                ClearSavedToken();
                Cleanup();
                Succeeded = false;
                TriggerStateSwitch?.Invoke();
            }
        }

        public override void Stop()
        {
            Cleanup();
        }

        private void OnRoomJoined(Game.Server.RoomJoined roomJoined)
        {
            Debug.Log($"[GameReconnect] Reconnected successfully as player {roomJoined.PlayerId} in room {roomJoined.RoomCode}");
            SaveToken(roomJoined.ReconnectToken, roomJoined.RoomCode);
            _isWaitingForResponse = false;
            Cleanup();
            Succeeded = true;
            TriggerStateSwitch?.Invoke();
        }

        private void OnError(Game.Server.Error error)
        {
            Debug.LogWarning($"[GameReconnect] Reconnect failed: {error.Message}");
            ClearSavedToken();
            _isWaitingForResponse = false;
            Cleanup();
            Succeeded = false;
            TriggerStateSwitch?.Invoke();
        }

        private void Cleanup()
        {
            if (_networkManager != null)
            {
                _networkManager.OnRoomJoined -= OnRoomJoined;
                _networkManager.OnError -= OnError;
            }
        }

        private static void SaveToken(string token, string roomCode)
        {
            PlayerPrefs.SetString(ReconnectTokenKey, token);
            PlayerPrefs.SetString(ReconnectRoomKey, roomCode);
            PlayerPrefs.Save();
        }

        private static void ClearSavedToken()
        {
            PlayerPrefs.DeleteKey(ReconnectTokenKey);
            PlayerPrefs.DeleteKey(ReconnectRoomKey);
            PlayerPrefs.Save();
        }

        public static bool HasSavedToken()
        {
            return !string.IsNullOrEmpty(PlayerPrefs.GetString(ReconnectTokenKey, ""));
        }
    }
}