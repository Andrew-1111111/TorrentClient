using TorrentClient.Models;

namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для хранения состояния торрентов (низкоуровневые операции)
    /// Разделение интерфейсов по принципу ISP
    /// </summary>
    public interface ITorrentStateStorage
    {
        /// <summary>Сохраняет состояние торрента</summary>
        void SaveTorrentState(Torrent torrent);
        
        /// <summary>Асинхронно сохраняет состояние торрента</summary>
        Task SaveTorrentStateAsync(Torrent torrent);
        
        /// <summary>Загружает состояние торрента</summary>
        TorrentStateData? LoadTorrentState(string infoHash);
        
        /// <summary>Асинхронно загружает состояние торрента</summary>
        Task<TorrentStateData?> LoadTorrentStateAsync(string infoHash);
        
        /// <summary>Сохраняет список торрентов</summary>
        void SaveTorrentList(List<Torrent> torrents);
        
        /// <summary>Асинхронно сохраняет список торрентов</summary>
        Task SaveTorrentListAsync(List<Torrent> torrents);
        
        /// <summary>Загружает список торрентов</summary>
        List<TorrentStateData> LoadTorrentList();
        
        /// <summary>Асинхронно загружает список торрентов</summary>
        Task<List<TorrentStateData>> LoadTorrentListAsync();
        
        /// <summary>Удаляет состояние торрента</summary>
        void DeleteTorrentState(string infoHash);
        
        /// <summary>Восстанавливает состояние торрента из сохранённых данных</summary>
        void RestoreTorrentState(Torrent torrent, TorrentStateData? state);
    }
}

