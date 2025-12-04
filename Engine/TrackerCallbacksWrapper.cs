using TorrentClient.Engine.Interfaces;
using System.Reflection;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Обёртка колбэков для Tracker, которая перенаправляет вызовы в TrackerManager или ActiveTorrent
    /// </summary>
    internal class TrackerCallbacksWrapper : ITrackerCallbacks
    {
        private readonly TrackerManager? _trackerManager;
        private readonly ActiveTorrent? _torrent;
        private static readonly FieldInfo? _swarmField = typeof(ActiveTorrent)
            .GetField("_swarm", BindingFlags.NonPublic | BindingFlags.Instance);

        public TrackerCallbacksWrapper(TrackerManager trackerManager)
        {
            _trackerManager = trackerManager;
        }

        public TrackerCallbacksWrapper(ActiveTorrent torrent)
        {
            _torrent = torrent;
        }

        public async Task OnPeersReceivedAsync(List<IPEndPoint> peers)
        {
            if (_trackerManager != null)
            {
                // Перенаправляем в TrackerManager
                await _trackerManager.OnPeersReceivedAsync(peers).ConfigureAwait(false);
            }
            else if (_torrent != null)
            {
                // Получаем Swarm через рефлексию
                var swarm = _swarmField?.GetValue(_torrent) as Swarm;
                
                // Добавляем пиров как при загрузке, так и при раздаче
                if (swarm != null && 
                    (_torrent.Status == TorrentStatus.Downloading || _torrent.Status == TorrentStatus.Seeding))
                {
                    foreach (var peer in peers)
                    {
                        await swarm.AddPeerAsync(peer).ConfigureAwait(false);
                    }
                }
            }
        }

        public Task OnErrorAsync(string error)
        {
            // Ошибки логируются в Tracker, колбэк не требуется
            return Task.CompletedTask;
        }
    }
}

