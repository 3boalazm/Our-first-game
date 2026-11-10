using System;

namespace Network
{
    /// <summary>
    /// Abstraction over the raw transport layer.
    /// Think of it like a power outlet standard — your lamp (NetworkManager)
    /// doesn't care if the electricity comes from solar or coal, it just
    /// needs the same shaped plug. UdpClient and WebSocketClient both
    /// implement this same plug shape.
    /// </summary>
    public interface INetworkTransport : IDisposable
    {
        bool IsConnected { get; }
        uint SendSequence { get; }
        uint LastReceivedSequence { get; }

        event Action<byte[]> OnDataReceived;
        event Action<string> OnError;
        event Action OnDisconnected;
        event Action OnConnected;

        bool Connect(string host, int port);
        void Disconnect();
        bool Send(byte[] data);
        int UpdateReceivedSequence(uint sequence);
    }
}