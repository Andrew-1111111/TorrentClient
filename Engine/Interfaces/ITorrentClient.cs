namespace TorrentClient.Engine.Interfaces
{
    /// <summary>
    /// Интерфейс для клиента торрентов
    /// </summary>
    public interface ITorrentClient : IDisposable, IAsyncDisposable
    {
        /// <summary>Добавляет торрент</summary>
        Task<ActiveTorrent> AddTorrentAsync(TorrentMetadata metadata, string downloadPath);
        
        /// <summary>Удаляет торрент</summary>
        Task RemoveTorrentAsync(string infoHashHex, bool deleteFiles = false);
        
        /// <summary>Получает торрент по InfoHash</summary>
        ActiveTorrent? GetTorrent(string infoHashHex);
        
        /// <summary>Получает все торренты</summary>
        IReadOnlyList<ActiveTorrent> GetAllTorrents();
    }
}

