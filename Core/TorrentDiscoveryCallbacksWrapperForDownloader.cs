using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Core
{
    /// <summary>
    /// Обёртка колбэков для TorrentDiscovery в TorrentDownloader
    /// </summary>
    internal class TorrentDiscoveryCallbacksWrapperForDownloader : ITorrentDiscoveryCallbacks
    {
        private readonly TorrentDownloader _downloader;

        public TorrentDiscoveryCallbacksWrapperForDownloader(TorrentDownloader downloader)
        {
            _downloader = downloader;
        }

        public Task OnPeerDiscoveredAsync(IPEndPoint peer)
        {
            if (!_downloader.IsSelfPeer(peer))
                _downloader.AddPeer(peer);
            return Task.CompletedTask;
        }
    }
}

