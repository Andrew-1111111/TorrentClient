namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления торрентами.
    /// Объединяет специализированные интерфейсы (ISP - Interface Segregation Principle).
    /// Предоставляет полный функционал для работы с торрентами: добавление, удаление, управление состоянием и настройками.
    /// </summary>
    /// <remarks>
    /// Реализует паттерн композиции интерфейсов:
    /// - ITorrentRepository - работа с коллекцией торрентов
    /// - ITorrentStateManager - управление состоянием торрентов
    /// - ITorrentSettingsManager - управление настройками торрентов
    /// </remarks>
    public interface ITorrentManager : 
        IDisposable, 
        IAsyncDisposable,
        ITorrentRepository,
        ITorrentStateManager,
        ITorrentSettingsManager
    {
        /// <summary>
        /// Устанавливает колбэки для асинхронных уведомлений о изменениях.
        /// </summary>
        /// <param name="callbacks">Объект с колбэками для обработки событий торрентов.</param>
        /// <remarks>
        /// Колбэки используются вместо событий для лучшей поддержки асинхронного программирования.
        /// </remarks>
        void SetCallbacks(ITorrentManagerCallbacks callbacks);
    }
}

