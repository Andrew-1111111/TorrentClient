using TorrentClient.Engine;
using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Core
{
    /// <summary>
    /// Реализация колбэков для Client, которая перенаправляет вызовы в TorrentManager
    /// </summary>
    internal class TorrentClientCallbacks : ITorrentClientCallbacks
    {
        private readonly TorrentManager _torrentManager;

        public TorrentClientCallbacks(TorrentManager torrentManager)
        {
            _torrentManager = torrentManager;
        }

        public Task OnTorrentAddedAsync(ActiveTorrent torrent)
        {
            // Обрабатывается в TorrentManager через AddTorrentAsync
            return Task.CompletedTask;
        }

        public Task OnTorrentRemovedAsync(ActiveTorrent torrent)
        {
            // Обрабатывается в TorrentManager через RemoveTorrentAsync
            return Task.CompletedTask;
        }

        public async Task OnTorrentProgressAsync(ActiveTorrent torrent)
        {
            await _torrentManager.OnClientTorrentProgressAsync(torrent).ConfigureAwait(false);
        }

        public async Task OnTorrentCompleteAsync(ActiveTorrent torrent)
        {
            await _torrentManager.OnClientTorrentCompleteAsync(torrent).ConfigureAwait(false);
        }
    }
}

