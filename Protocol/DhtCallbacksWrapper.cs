using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Обёртка колбэков для DhtClient, которая перенаправляет вызовы в TorrentDiscovery
    /// </summary>
    internal class DhtCallbacksWrapper : IDhtCallbacks
    {
        private readonly TorrentDiscovery _discovery;

        public DhtCallbacksWrapper(TorrentDiscovery discovery)
        {
            _discovery = discovery;
        }

        public Task OnPeersFoundAsync(List<IPEndPoint> peers)
        {
            foreach (var peer in peers)
            {
                _discovery.OnPeerDiscovered(peer);
            }
            return Task.CompletedTask;
        }
    }
}

