namespace TorrentClient.Engine.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий ActiveTorrent (замена событий)
    /// </summary>
    public interface IActiveTorrentCallbacks
    {
        /// <summary>Вызывается при изменении прогресса</summary>
        Task OnProgressChangedAsync();
        
        /// <summary>Вызывается при завершении загрузки</summary>
        Task OnCompleteAsync();
        
        /// <summary>Вызывается при ошибке</summary>
        Task OnErrorAsync(string error);
    }
}



























