namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления загрузкой кусков
    /// Принцип ISP: специфичный интерфейс только для загрузки кусков
    /// </summary>
    public interface IPieceDownloadManager
    {
        /// <summary>
        /// Загружает доступные куски
        /// </summary>
        Task DownloadPiecesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Загружает конкретный кусок
        /// </summary>
        Task DownloadPieceAsync(int pieceIndex, CancellationToken cancellationToken);

        /// <summary>
        /// Загружает блок данных
        /// </summary>
        Task<(bool success, int length)> DownloadBlockAsync(
            int pieceIndex, int begin, int length, byte[] pieceData, CancellationToken cancellationToken);

        /// <summary>
        /// Обновляет настройки загрузки
        /// </summary>
        void UpdateSettings(int maxConcurrentBlocks, int maxRequestsPerPeer);
    }
}

