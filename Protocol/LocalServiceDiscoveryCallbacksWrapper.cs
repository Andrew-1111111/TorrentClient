using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Обёртка колбэков для LocalServiceDiscovery, которая перенаправляет вызовы в TorrentDiscovery
    /// </summary>
    internal class LocalServiceDiscoveryCallbacksWrapper : ILocalServiceDiscoveryCallbacks
    {
        private readonly TorrentDiscovery _discovery;

        public LocalServiceDiscoveryCallbacksWrapper(TorrentDiscovery discovery)
        {
            _discovery = discovery;
        }

        public Task OnPeerDiscoveredAsync(IPEndPoint peer)
        {
            _discovery.OnPeerDiscovered(peer);
            return Task.CompletedTask;
        }
    }
}

