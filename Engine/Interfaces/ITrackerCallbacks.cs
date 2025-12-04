namespace TorrentClient.Engine.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий Tracker (замена событий)
    /// </summary>
    public interface ITrackerCallbacks
    {
        /// <summary>Вызывается при получении пиров</summary>
        Task OnPeersReceivedAsync(List<IPEndPoint> peers);
        
        /// <summary>Вызывается при ошибке</summary>
        Task OnErrorAsync(string error);
    }
}

