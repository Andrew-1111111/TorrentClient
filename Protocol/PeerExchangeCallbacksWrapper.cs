using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Обёртка колбэков для PeerExchange, которая перенаправляет вызовы в PeerConnection
    /// </summary>
    internal class PeerExchangeCallbacksWrapper : IPeerExchangeCallbacks
    {
        private readonly PeerConnection _connection;

        public PeerExchangeCallbacksWrapper(PeerConnection connection)
        {
            _connection = connection;
        }

        public Task OnPeersReceivedAsync(List<IPEndPoint> peers)
        {
            _connection.OnPexPeersReceived(null, peers);
            return Task.CompletedTask;
        }
    }
}

