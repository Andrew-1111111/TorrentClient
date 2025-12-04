namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления хранением кусков
    /// Принцип ISP: специфичный интерфейс только для работы с файлами кусков
    /// </summary>
    public interface IPieceStorageManager
    {
        /// <summary>
        /// Сохраняет кусок на диск
        /// </summary>
        Task SavePieceAsync(int pieceIndex, byte[] pieceData, CancellationToken cancellationToken);

        /// <summary>
        /// Читает кусок с диска
        /// </summary>
        Task<byte[]?> ReadPieceAsync(int pieceIndex, CancellationToken cancellationToken);

        /// <summary>
        /// Проверяет хеш куска
        /// </summary>
        bool VerifyPieceHash(int pieceIndex, byte[] pieceData);
    }
}

