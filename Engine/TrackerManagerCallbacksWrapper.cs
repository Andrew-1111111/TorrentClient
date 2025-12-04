using System.Reflection;
using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Обёртка колбэков для TrackerManager, которая перенаправляет вызовы в ActiveTorrent
    /// </summary>
    internal class TrackerManagerCallbacksWrapper : ITrackerCallbacks
    {
        private readonly ActiveTorrent _torrent;
        private static readonly FieldInfo? _swarmField = typeof(ActiveTorrent)
            .GetField("_swarm", BindingFlags.NonPublic | BindingFlags.Instance);

        public TrackerManagerCallbacksWrapper(ActiveTorrent torrent)
        {
            _torrent = torrent;
        }

        public async Task OnPeerDiscoveredAsync(IPEndPoint peer)
        {
            // Получаем Swarm через рефлексию
            var swarm = _swarmField?.GetValue(_torrent) as Swarm;
            
            // Добавляем пиров как при загрузке, так и при раздаче
            if (swarm != null && 
                (_torrent.Status == TorrentStatus.Downloading || _torrent.Status == TorrentStatus.Seeding))
            {
                await swarm.AddPeerAsync(peer).ConfigureAwait(false);
            }
        }

        public async Task OnPeersReceivedAsync(List<IPEndPoint> peers)
        {
            // Получаем Swarm через рефлексию
            var swarm = _swarmField?.GetValue(_torrent) as Swarm;
            
            // Добавляем всех полученных пиров
            if (swarm != null && 
                (_torrent.Status == TorrentStatus.Downloading || _torrent.Status == TorrentStatus.Seeding))
            {
                foreach (var peer in peers)
                {
                    await swarm.AddPeerAsync(peer).ConfigureAwait(false);
                }
            }
        }

        public Task OnErrorAsync(string error)
        {
            // Ошибки трекера логируются в Tracker, здесь просто возвращаем Task
            return Task.CompletedTask;
        }
    }
}

