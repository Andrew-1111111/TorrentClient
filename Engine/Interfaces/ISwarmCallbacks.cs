namespace TorrentClient.Engine.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий Swarm (замена событий)
    /// </summary>
    public interface ISwarmCallbacks
    {
        /// <summary>Вызывается при завершении загрузки куска</summary>
        Task OnPieceCompletedAsync(int pieceIndex);
        
        /// <summary>Вызывается при завершении загрузки</summary>
        Task OnDownloadCompleteAsync();
        
        /// <summary>Вызывается при обновлении статистики</summary>
        Task OnStatsUpdatedAsync(long downloaded, long downloadedBytes, long uploaded, int peers);
    }
}



























