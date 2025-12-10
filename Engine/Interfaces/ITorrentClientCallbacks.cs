namespace TorrentClient.Engine.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий TorrentClient (замена событий)
    /// </summary>
    public interface ITorrentClientCallbacks
    {
        /// <summary>Вызывается при добавлении торрента</summary>
        Task OnTorrentAddedAsync(ActiveTorrent torrent);
        
        /// <summary>Вызывается при удалении торрента</summary>
        Task OnTorrentRemovedAsync(ActiveTorrent torrent);
        
        /// <summary>Вызывается при изменении прогресса торрента</summary>
        Task OnTorrentProgressAsync(ActiveTorrent torrent);
        
        /// <summary>Вызывается при завершении торрента</summary>
        Task OnTorrentCompleteAsync(ActiveTorrent torrent);
    }
}



























