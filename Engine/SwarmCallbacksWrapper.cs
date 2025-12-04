using System.Reflection;
using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Обёртка колбэков для Swarm, которая перенаправляет вызовы в ActiveTorrent
    /// </summary>
    internal class SwarmCallbacksWrapper : ISwarmCallbacks
    {
        private readonly ActiveTorrent _torrent;
        private static readonly FieldInfo? _callbacksField = typeof(ActiveTorrent)
            .GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? _emaDownloadSpeedField = typeof(ActiveTorrent)
            .GetField("_emaDownloadSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo? _statusProperty = typeof(ActiveTorrent)
            .GetProperty("Status", BindingFlags.Public | BindingFlags.Instance);
        private static readonly PropertyInfo? _downloadSpeedProperty = typeof(ActiveTorrent)
            .GetProperty("DownloadSpeed", BindingFlags.Public | BindingFlags.Instance);

        public SwarmCallbacksWrapper(ActiveTorrent torrent)
        {
            _torrent = torrent;
        }

        public async Task OnPieceCompletedAsync(int pieceIndex)
        {
            var callbacks = _callbacksField?.GetValue(_torrent) as IActiveTorrentCallbacks;
            if (callbacks != null)
            {
                await callbacks.OnProgressChangedAsync().ConfigureAwait(false);
            }
        }

        public async Task OnDownloadCompleteAsync()
        {
            _statusProperty?.SetValue(_torrent, TorrentStatus.Seeding);
            // Сбрасываем скорость загрузки (загрузка завершена)
            _downloadSpeedProperty?.SetValue(_torrent, 0);
            _emaDownloadSpeedField?.SetValue(_torrent, 0);
            
            var callbacks = _callbacksField?.GetValue(_torrent) as IActiveTorrentCallbacks;
            if (callbacks != null)
            {
                await callbacks.OnCompleteAsync().ConfigureAwait(false);
            }
        }

        public async Task OnStatsUpdatedAsync(long downloaded, long downloadedBytes, long uploaded, int peers)
        {
            await _torrent.OnStatsUpdatedAsync(downloaded, downloadedBytes, uploaded, peers).ConfigureAwait(false);
        }
    }
}

