using System.Text.Json;

using TorrentClient.Core.Interfaces;

namespace TorrentClient.Core
{
    /// <summary>
    /// Настройки приложения
    /// </summary>
    public class AppSettings
    {
        /// <summary>Путь для загрузки торрентов по умолчанию</summary>
        public string DefaultDownloadPath { get; set; } = string.Empty;
        
        /// <summary>Путь для сохранения состояния торрентов</summary>
        public string StatePath { get; set; } = string.Empty;
        
        /// <summary>Cookies для трекеров (ключ - URL трекера, значение - строка cookies)</summary>
        public Dictionary<string, string> TrackerCookies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>HTTP заголовки для трекеров (ключ - URL трекера, значение - словарь заголовков)</summary>
        public Dictionary<string, Dictionary<string, string>> TrackerHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>Максимальное количество одновременных соединений с пирами</summary>
        public int MaxConnections { get; set; } = 200;
        
        /// <summary>Максимальное количество полуоткрытых соединений (в процессе установки)</summary>
        public int MaxHalfOpenConnections { get; set; } = 100;
        
        /// <summary>Максимальное количество кусков для запроса одновременно</summary>
        public int MaxPiecesToRequest { get; set; } = 100;
        
        /// <summary>Максимальное количество запросов на одного пира</summary>
        public int MaxRequestsPerPeer { get; set; } = 128;
        
        /// <summary>Включено ли логирование</summary>
        public bool EnableLogging { get; set; } = true;
        
        /// <summary>Сворачивать ли окно в трей при закрытии (true) или закрывать приложение (false)</summary>
        public bool MinimizeToTrayOnClose { get; set; } = true;
        
        /// <summary>Автоматически запускать все торренты при запуске приложения</summary>
        public bool AutoStartOnLaunch { get; set; } = false;
        
        /// <summary>Автоматически запускать торрент при добавлении</summary>
        public bool AutoStartOnAdd { get; set; } = false;
        
        /// <summary>Копировать .torrent файл в папку загрузки</summary>
        public bool CopyTorrentFileToDownloadFolder { get; set; } = false;
        
        /// <summary>Глобальный лимит скорости загрузки в байтах в секунду (null = без ограничений)</summary>
        public long? GlobalMaxDownloadSpeed { get; set; } = null;
        
        /// <summary>Глобальный лимит скорости отдачи в байтах в секунду (null = без ограничений)</summary>
        public long? GlobalMaxUploadSpeed { get; set; } = null;
    }

    /// <summary>
    /// Менеджер настроек приложения
    /// </summary>
    public class AppSettingsManager : IAppSettingsManager
    {
        private readonly string _settingsFilePath;

        /// <summary>
        /// Инициализирует новый экземпляр AppSettingsManager
        /// </summary>
        public AppSettingsManager()
        {
            var appDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "appsettings.json");
        }

        /// <summary>
        /// Загружает настройки из файла
        /// </summary>
        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return GetDefaultSettings();

                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? GetDefaultSettings();
            }
            catch
            {
                return GetDefaultSettings();
            }
        }

        /// <summary>
        /// Сохраняет настройки в файл
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Игнорируем ошибки сохранения
            }
        }

        /// <summary>
        /// Возвращает настройки по умолчанию
        /// </summary>
        /// <returns>Настройки приложения со значениями по умолчанию</returns>
        private static AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                DefaultDownloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "Torrents"),
                StatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "States"),
                TrackerCookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                TrackerHeaders = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase),
                EnableLogging = true
            };
        }
    }
}
