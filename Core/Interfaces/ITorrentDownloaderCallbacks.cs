namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий TorrentDownloader (замена событий)
    /// </summary>
    public interface ITorrentDownloaderCallbacks
    {
        /// <summary>Вызывается при обновлении прогресса</summary>
        Task OnProgressUpdatedAsync(long downloadedBytes);
        
        /// <summary>Вызывается при завершении загрузки</summary>
        Task OnDownloadCompletedAsync();
        
        /// <summary>Вызывается при ошибке</summary>
        Task OnErrorOccurredAsync(string error);
        
        /// <summary>Вызывается при изменении состояния</summary>
        Task OnStateChangedAsync();
    }
}

