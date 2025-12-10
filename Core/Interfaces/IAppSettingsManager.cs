namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления настройками приложения.
    /// Предоставляет методы для загрузки и сохранения настроек в файл.
    /// </summary>
    public interface IAppSettingsManager
    {
        /// <summary>
        /// Загружает настройки приложения из файла.
        /// </summary>
        /// <returns>Объект AppSettings с загруженными настройками. Если файл не существует, возвращаются настройки по умолчанию.</returns>
        /// <remarks>
        /// Настройки загружаются из файла appsettings.json в папке Settings приложения.
        /// </remarks>
        AppSettings LoadSettings();
        
        /// <summary>
        /// Сохраняет настройки приложения в файл.
        /// </summary>
        /// <param name="settings">Объект AppSettings с настройками для сохранения.</param>
        /// <remarks>
        /// Настройки сохраняются в файл appsettings.json в папке Settings приложения.
        /// Если папка не существует, она будет создана автоматически.
        /// </remarks>
        void SaveSettings(AppSettings settings);
    }
}

