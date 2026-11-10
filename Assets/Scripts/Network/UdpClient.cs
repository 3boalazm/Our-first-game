using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class UdpClient : INetworkTransport
    {
        private System.Net.Sockets.UdpClient _client;
        private IPEndPoint _serverEndpoint;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
        private bool _isConnected;
        private bool _isDisposed;

        private uint _sendSequence = 0;
        private uint _lastReceivedSequence = 0;

        public bool IsConnected => _isConnected && !_isDisposed;
        public uint SendSequence => _sendSequence;
        public uint LastReceivedSequence => _lastReceivedSequence;

        public event Action<byte[]> OnDataReceived;
        public event Action<string> OnError;
        public event Action OnDisconnected;
        public event Action OnConnected;

        private const int ReceiveBufferSize = 4096;
        private const int SendBufferSize = 4096;

        public bool Connect(string host, int port)
        {
            if (_isDisposed)
            {
                OnError?.Invoke("Cannot connect: client has been disposed");
                return false;
            }

            try
            {
                Disconnect();

                _client = new System.Net.Sockets.UdpClient();
                _client.Client.ReceiveBufferSize = ReceiveBufferSize;
                _client.Client.SendBufferSize = SendBufferSize;

                _client.Client.ReceiveTimeout = 5000;
                _client.Client.SendTimeout = 1000;

                IPAddress ipAddress;
                if (!IPAddress.TryParse(host, out ipAddress))
                {
                    var addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length == 0)
                        throw new Exception($"Could not resolve hostname: {host}");
                    ipAddress = addresses[0];
                    Debug.Log($"[UdpClient] Resolved {host} -> {ipAddress}");
                }
                _serverEndpoint = new IPEndPoint(ipAddress, port);
                
                _client.Connect(_serverEndpoint);

                _isConnected = true;
                _sendSequence = 0;
                _lastReceivedSequence = 0;

                StartReceiving();

                Debug.Log($"[UdpClient] Connected to {host}:{port}");
                OnConnected?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                Debug.LogError($"[UdpClient] Connection error: {ex}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            try
            {
                _receiveCts?.Cancel();
                _receiveTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }

            _client = null;
            _isConnected = false;
            _receiveCts?.Dispose();
            _receiveCts = null;

            OnDisconnected?.Invoke();
            Debug.Log("[UdpClient] Disconnected");
        }

        public bool Send(byte[] data)
        {
            if (!IsConnected)
            {
                OnError?.Invoke("Cannot send: not connected");
                return false;
            }

            try
            {
                _client.Send(data, data.Length);
                return true;
            }
            catch (SocketException ex)
            {
                OnError?.Invoke($"Send failed: {ex.Message}");
                Debug.LogWarning($"[UdpClient] Send error: {ex.Message}");
                return false;
            }
        }

        private void StartReceiving()
        {
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token));
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            Debug.Log("[UdpClient] ReceiveLoop STARTED on thread " + 
                      Thread.CurrentThread.ManagedThreadId);

            Task<UdpReceiveResult> receiveTask = null;

            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    if (receiveTask == null)
                    {
                        receiveTask = _client.ReceiveAsync();
                    }

                    var completedTask = await Task.WhenAny(
                        receiveTask,
                        Task.Delay(1000, cancellationToken)
                    );

                    if (completedTask == receiveTask && !cancellationToken.IsCancellationRequested)
                    {
                        var result = await receiveTask;
                        receiveTask = null;

                        byte[] receivedData = new byte[result.Buffer.Length];
                        Array.Copy(result.Buffer, receivedData, result.Buffer.Length);

                        MainThreadDispatcher.Enqueue(() =>
                        {
                            OnDataReceived?.Invoke(receivedData);
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("[UdpClient] ReceiveLoop: OperationCanceledException (normal shutdown)");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("[UdpClient] ReceiveLoop: ObjectDisposedException (socket closed)");
                    break;
                }
                catch (SocketException ex)
                {
                    receiveTask = null;
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Receive error: {ex.Message}"));
                    }
                }
                catch (Exception ex)
                {
                    receiveTask = null;
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Unexpected error: {ex.Message}"));
                    }
                }
            }

            Debug.Log("[UdpClient] Receive loop ended");
        }

        public int UpdateReceivedSequence(uint sequence)
        {
            if (sequence == 0)
            {
                _lastReceivedSequence = sequence;
                return 0;
            }

            int gap = 0;
            uint expected = _lastReceivedSequence + 1;

            if (sequence > expected)
            {
                gap = (int)(sequence - expected);
                Debug.LogWarning($"[UdpClient] Packet loss detected: {gap} packets missing");
            }
            else if (sequence < expected && sequence != 1)
            {
                Debug.Log($"[UdpClient] Out-of-order or duplicate packet: seq={sequence}, expected={expected}");
                return -1;
            }

            _lastReceivedSequence = sequence;
            return gap;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            Disconnect();
            GC.SuppressFinalize(this);
        }

        ~UdpClient()
        {
            Dispose();
        }
    }
}
