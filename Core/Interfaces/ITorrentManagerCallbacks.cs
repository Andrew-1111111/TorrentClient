namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий TorrentManager (замена событий)
    /// </summary>
    public interface ITorrentManagerCallbacks
    {
        /// <summary>Вызывается при добавлении торрента</summary>
        Task OnTorrentAddedAsync(Models.Torrent torrent);
        
        /// <summary>Вызывается при удалении торрента</summary>
        Task OnTorrentRemovedAsync(Models.Torrent torrent);
        
        /// <summary>Вызывается при обновлении торрента</summary>
        Task OnTorrentUpdatedAsync(Models.Torrent torrent);
    }
}

