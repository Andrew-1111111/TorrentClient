using TorrentClient.Protocol;
using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Core
{
    /// <summary>
    /// Обёртка колбэков для PeerConnection в TorrentDownloader
    /// </summary>
    internal class PeerConnectionCallbacksWrapperForDownloader : IPeerConnectionCallbacks
    {
        private readonly TorrentDownloader _downloader;
        private readonly PeerConnection _connection;

        public PeerConnectionCallbacksWrapperForDownloader(TorrentDownloader downloader, PeerConnection connection)
        {
            _downloader = downloader;
            _connection = connection;
        }

        public Task OnPieceReceivedAsync(PeerConnection.PieceDataEventArgs e)
        {
            _downloader.HandlePieceData(_connection, e);
            return Task.CompletedTask;
        }

        public async Task OnConnectionClosedAsync()
        {
            await _downloader.RemoveConnectionAsync(_connection).ConfigureAwait(false);
        }

        public Task OnHaveReceivedAsync(PeerConnection.HaveEventArgs e)
        {
            _downloader.HandleHaveMessage(_connection, e.PieceIndex);
            return Task.CompletedTask;
        }

        public async Task OnRequestReceivedAsync(PeerConnection.RequestEventArgs e)
        {
            await _downloader.HandlePeerRequestAsync(_connection, e).ConfigureAwait(false);
        }

        public Task OnPexPeersReceivedAsync(List<IPEndPoint> peers)
        {
            _downloader.HandlePexPeers(peers);
            return Task.CompletedTask;
        }

        public Task OnPeerBitfieldUpdatedAsync()
        {
            _downloader.HandlePeerBitfieldUpdated(_connection);
            return Task.CompletedTask;
        }
    }
}

