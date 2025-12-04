namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления настройками приложения
    /// </summary>
    public interface IAppSettingsManager
    {
        /// <summary>Загружает настройки</summary>
        AppSettings LoadSettings();
        
        /// <summary>Сохраняет настройки</summary>
        void SaveSettings(AppSettings settings);
    }
}

