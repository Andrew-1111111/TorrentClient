namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления торрентами
    /// Объединяет специализированные интерфейсы (ISP - Interface Segregation Principle)
    /// </summary>
    public interface ITorrentManager : 
        IDisposable, 
        IAsyncDisposable,
        ITorrentRepository,
        ITorrentStateManager,
        ITorrentSettingsManager
    {
        /// <summary>Устанавливает колбэки для замены событий</summary>
        void SetCallbacks(ITorrentManagerCallbacks callbacks);
    }
}

