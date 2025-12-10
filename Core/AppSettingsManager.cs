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
        
        /// <summary>Код языка интерфейса (например, "en", "ru", "es"). Если пусто, определяется автоматически из Windows</summary>
        public string? LanguageCode { get; set; } = null;
    }

    /// <summary>
    /// Менеджер настроек приложения.
    /// Управляет загрузкой и сохранением настроек приложения в файл appsettings.json.
    /// </summary>
    public class AppSettingsManager : IAppSettingsManager
    {
        private readonly string _settingsFilePath;

        /// <summary>
        /// Получает путь к папке приложения.
        /// </summary>
        /// <returns>Путь к папке, где находится исполняемый файл приложения.</returns>
        /// <remarks>
        /// При отладке это будет папка сборки, при запуске .exe - папка с .exe файлом.
        /// </remarks>
        private static string GetApplicationDirectory()
        {
            // Используем AppDomain.CurrentDomain.BaseDirectory - это путь к папке, где находится исполняемый файл
            // При отладке это будет папка сборки, при запуске .exe - папка с .exe файлом
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Инициализирует новый экземпляр AppSettingsManager.
        /// Создает папку Settings, если она не существует.
        /// </summary>
        /// <remarks>
        /// Путь к файлу настроек: {папка_приложения}/Settings/appsettings.json
        /// </remarks>
        public AppSettingsManager()
        {
            // Используем папку приложения для Settings
            var appDirectory = GetApplicationDirectory();
            var settingsPath = Path.Combine(appDirectory, "Settings");
            
            System.Diagnostics.Debug.WriteLine($"AppSettingsManager: Папка приложения: {appDirectory}");
            System.Diagnostics.Debug.WriteLine($"AppSettingsManager: Путь к Settings: {settingsPath}");
            
            try
            {
                // Создаем папку, если её нет
                if (!Directory.Exists(settingsPath))
                {
                    System.Diagnostics.Debug.WriteLine($"AppSettingsManager: Создание папки Settings: {settingsPath}");
                    Directory.CreateDirectory(settingsPath);
                }
                
                // Проверяем, что папка действительно создана
                if (!Directory.Exists(settingsPath))
                {
                    var error = $"Не удалось создать директорию настроек: {settingsPath}";
                    System.Diagnostics.Debug.WriteLine($"AppSettingsManager: {error}");
                    throw new InvalidOperationException(error);
                }
                
                System.Diagnostics.Debug.WriteLine($"AppSettingsManager: Папка Settings создана/существует: {settingsPath}");
            }
            catch (Exception ex)
            {
                // Логируем ошибку с полной информацией
                System.Diagnostics.Debug.WriteLine($"AppSettingsManager: ОШИБКА создания директории настроек: {settingsPath}");
                System.Diagnostics.Debug.WriteLine($"AppSettingsManager: Ошибка: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AppSettingsManager: StackTrace: {ex.StackTrace}");
                throw;
            }
            
            _settingsFilePath = Path.Combine(settingsPath, "appsettings.json");
            System.Diagnostics.Debug.WriteLine($"AppSettingsManager: Путь к файлу настроек: {_settingsFilePath}");
        }

        /// <summary>
        /// Загружает настройки из файла appsettings.json.
        /// </summary>
        /// <returns>Объект AppSettings с загруженными настройками. Если файл не существует, возвращаются настройки по умолчанию.</returns>
        /// <remarks>
        /// Если файл настроек не существует, возвращаются настройки по умолчанию.
        /// При ошибке чтения файла также возвращаются настройки по умолчанию, ошибка логируется.
        /// </remarks>
        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return GetDefaultSettings();

                var json = File.ReadAllText(_settingsFilePath);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true
                };
                var settings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? GetDefaultSettings();
                
                // Нормализуем LanguageCode: если пустая строка, устанавливаем null для использования языка Windows
                if (string.IsNullOrWhiteSpace(settings.LanguageCode))
                {
                    settings.LanguageCode = null;
                }
                
                // Логируем загруженные глобальные лимиты для отладки
                Logger.LogInfo($"[AppSettingsManager] Загружены глобальные лимиты: загрузка={FormatSpeed(settings.GlobalMaxDownloadSpeed)}, отдача={FormatSpeed(settings.GlobalMaxUploadSpeed)}");
                Logger.LogInfo($"[AppSettingsManager] Загружен LanguageCode: {(settings.LanguageCode ?? "null (будет использован язык Windows)")}");
                
                return settings;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[AppSettingsManager] Ошибка загрузки настроек: {ex.Message}");
                return GetDefaultSettings();
            }
        }
        
        private static string FormatSpeed(long? bytesPerSecond)
        {
            if (bytesPerSecond == null) return "без ограничений";
            // Конвертация согласно стандарту: https://en.wikipedia.org/wiki/Data-rate_units
            // 1 Mbps = 1,000,000 bits/s = 125,000 bytes/s
            var mbps = bytesPerSecond.Value * 8.0 / 1_000_000.0;
            return $"{mbps:F1} Mbps";
        }

        /// <summary>
        /// Сохраняет настройки в файл appsettings.json.
        /// </summary>
        /// <param name="settings">Объект AppSettings с настройками для сохранения.</param>
        /// <remarks>
        /// Если папка Settings не существует, она будет создана автоматически.
        /// Настройки сохраняются в формате JSON с отступами для читаемости.
        /// </remarks>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                // Убеждаемся, что директория существует перед сохранением
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Нормализуем LanguageCode перед сохранением: если пустая строка, устанавливаем null
                // Это гарантирует, что при следующей загрузке будет использован язык Windows
                if (string.IsNullOrWhiteSpace(settings.LanguageCode))
                {
                    settings.LanguageCode = null;
                }
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                    IncludeFields = false
                };
                var json = JsonSerializer.Serialize(settings, options);
                
                // Логируем JSON для отладки (первые 500 символов)
                var jsonPreview = json.Length > 500 ? json.Substring(0, 500) + "..." : json;
                Logger.LogInfo($"[AppSettingsManager] JSON для сохранения (первые 500 символов): {jsonPreview}");
                
                File.WriteAllText(_settingsFilePath, json);
                
                // Логируем сохраненные глобальные лимиты для отладки
                Logger.LogInfo($"[AppSettingsManager] Сохранены глобальные лимиты: загрузка={FormatSpeed(settings.GlobalMaxDownloadSpeed)}, отдача={FormatSpeed(settings.GlobalMaxUploadSpeed)}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AppSettingsManager] Ошибка сохранения настроек: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Возвращает настройки по умолчанию
        /// </summary>
        /// <returns>Настройки приложения со значениями по умолчанию</returns>
        /// <summary>
        /// Получает настройки приложения по умолчанию.
        /// </summary>
        /// <returns>Объект AppSettings с настройками по умолчанию.</returns>
        private static AppSettings GetDefaultSettings()
        {
            // Используем папку приложения для всех папок
            var appDirectory = GetApplicationDirectory();
            var statesPath = Path.Combine(appDirectory, "States");
            var downloadsPath = Path.Combine(appDirectory, "Downloads");
            
            return new AppSettings
            {
                DefaultDownloadPath = downloadsPath,
                StatePath = statesPath,
                TrackerCookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                TrackerHeaders = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase),
                EnableLogging = true
            };
        }
    }
}
