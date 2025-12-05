namespace TorrentClient.UI.Interfaces
{
    /// <summary>
    /// Интерфейс для управления иконкой в системном трее
    /// </summary>
    public interface ITrayIconManager : IDisposable
    {
        /// <summary>Сворачивает окно в трей</summary>
        void MinimizeToTray();
        
        /// <summary>Восстанавливает окно из трея</summary>
        void RestoreFromTray();
        
        /// <summary>Показывает уведомление в трее</summary>
        void ShowBalloonTip(int timeout, string title, string text, ToolTipIcon icon);
        
        /// <summary>Обновляет текст иконки в трее с информацией о скорости</summary>
        void UpdateSpeedInfo(long downloadSpeed, long uploadSpeed);
        
        /// <summary>Видимость иконки</summary>
        bool Visible { get; set; }
    }
}

