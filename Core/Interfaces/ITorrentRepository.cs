namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для операций с торрентами (CRUD)
    /// Разделение интерфейсов по принципу ISP
    /// </summary>
    public interface ITorrentRepository
    {
        /// <summary>Добавляет торрент</summary>
        Models.Torrent? AddTorrent(string torrentFilePath, string? downloadPath = null);
        
        /// <summary>Удаляет торрент</summary>
        Task RemoveTorrentsAsync(IEnumerable<string> torrentIds, bool deleteFiles = false);
        
        /// <summary>Запускает торрент</summary>
        Task StartTorrentAsync(string torrentId);
        
        /// <summary>Ставит торрент на паузу</summary>
        Task PauseTorrentAsync(string torrentId);
        
        /// <summary>Останавливает торрент</summary>
        Task StopTorrentAsync(string torrentId);
        
        /// <summary>Получает все торренты</summary>
        List<Models.Torrent> GetAllTorrents();
        
        /// <summary>Получает торрент по ID</summary>
        Models.Torrent? GetTorrent(string torrentId);
    }
}

