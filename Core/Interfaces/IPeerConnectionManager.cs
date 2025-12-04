using System.Net;
using TorrentClient.Protocol;

namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления соединениями с пирами
    /// Принцип ISP: специфичный интерфейс только для управления соединениями
    /// </summary>
    public interface IPeerConnectionManager
    {
        /// <summary>
        /// Получает список активных соединений
        /// </summary>
        IReadOnlyList<PeerConnection> GetActiveConnections();

        /// <summary>
        /// Получает количество активных соединений
        /// </summary>
        int GetActiveConnectionCount();

        /// <summary>
        /// Подключается к пирам асинхронно
        /// </summary>
        Task ConnectToPeersAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Подключается к конкретному пиру
        /// </summary>
        Task ConnectToPeerAsync(IPEndPoint peer, CancellationToken cancellationToken);

        /// <summary>
        /// Очищает неработающие соединения
        /// </summary>
        Task CleanupDeadConnectionsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Выбирает лучшего пира для загрузки куска
        /// </summary>
        PeerConnection? SelectBestPeer(int pieceIndex, CancellationToken cancellationToken);

        /// <summary>
        /// Добавляет пира в список известных
        /// </summary>
        void AddPeer(IPEndPoint peer);

        /// <summary>
        /// Обновляет настройки соединений
        /// </summary>
        void UpdateSettings(int maxConnections, int maxHalfOpenConnections);
    }
}

