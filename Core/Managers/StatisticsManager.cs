using TorrentClient.Core.Interfaces;
using TorrentClient.Models;

namespace TorrentClient.Core.Managers
{
    /// <summary>
    /// Менеджер статистики загрузки
    /// Принцип SRP: единственная ответственность - управление статистикой
    /// </summary>
    public class StatisticsManager : IStatisticsManager
    {
        private readonly Torrent _torrent;
        private readonly Func<IReadOnlyList<PeerConnection>> _getConnections;
        private readonly Func<int> _getTotalPeers;
        private long _lastDownloadedBytes;
        private DateTime _lastSpeedUpdate = DateTime.UtcNow;

        public StatisticsManager(
            Torrent torrent,
            Func<IReadOnlyList<PeerConnection>> getConnections,
            Func<int> getTotalPeers)
        {
            _torrent = torrent ?? throw new ArgumentNullException(nameof(torrent));
            _getConnections = getConnections ?? throw new ArgumentNullException(nameof(getConnections));
            _getTotalPeers = getTotalPeers ?? throw new ArgumentNullException(nameof(getTotalPeers));
        }

        /// <summary>
        /// Обновляет статистику скорости загрузки
        /// </summary>
        public void UpdateStatistics()
        {
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
                if (elapsed > 0)
                {
                    var downloaded = _torrent.DownloadedBytes - _lastDownloadedBytes;
                    _torrent.DownloadSpeed = (long)(downloaded / elapsed);
                    _lastDownloadedBytes = _torrent.DownloadedBytes;
                    _lastSpeedUpdate = now;
                }

                var connections = _getConnections();
                _torrent.ConnectedPeers = connections.Count(c => c.IsConnected);
                _torrent.TotalPeers = _getTotalPeers();
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка обновления статистики", ex);
            }
        }

        /// <summary>
        /// Получает текущую скорость загрузки (байт/сек)
        /// </summary>
        public long GetDownloadSpeed() => _torrent.DownloadSpeed;

        /// <summary>
        /// Получает текущую скорость отдачи (байт/сек)
        /// </summary>
        public long GetUploadSpeed() => _torrent.UploadSpeed;
    }
}

