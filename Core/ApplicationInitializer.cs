using TorrentClient.Core.Interfaces;
using TorrentClient.UI.Interfaces;
using TorrentClient.UI;
using TorrentClient.UI.Services;
using TorrentClient.UI.Services.Interfaces;
using TorrentClient.Protocol;

namespace TorrentClient.Core
{
    /// <summary>
    /// Инициализатор приложения - отвечает за создание и настройку всех зависимостей
    /// Принцип SRP: единственная ответственность - инициализация зависимостей
    /// </summary>
    public class ApplicationInitializer
    {
        /// <summary>
        /// Инициализирует все зависимости приложения
        /// </summary>
        public ApplicationDependencies Initialize()
        {
            // Инициализация настроек
            var settingsManager = new AppSettingsManager();
            var appSettings = settingsManager.LoadSettings();
            
            // Инициализация локализации
            LocalizationManager.Initialize(appSettings.LanguageCode);
            
            // Настройка логирования
            Logger.SetEnabled(appSettings.EnableLogging);
            
            // Настройка глобального лимитера скорости
            GlobalSpeedLimiter.Instance.UpdateLimits(
                appSettings.GlobalMaxDownloadSpeed,
                appSettings.GlobalMaxUploadSpeed);
            
            // Определение путей - все папки внутри папки приложения
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            System.Diagnostics.Debug.WriteLine($"Папка приложения: {appDirectory}");
            
            var downloadPath = !string.IsNullOrWhiteSpace(appSettings.DefaultDownloadPath)
                ? appSettings.DefaultDownloadPath
                : Path.Combine(appDirectory, "Downloads");
            
            // Используем путь из настроек или папку приложения
            var statePath = !string.IsNullOrWhiteSpace(appSettings.StatePath)
                ? appSettings.StatePath
                : Path.Combine(appDirectory, "States");
            
            // Нормализуем пути (GetFullPath работает даже для несуществующих путей)
            try
            {
                downloadPath = Path.GetFullPath(downloadPath);
                statePath = Path.GetFullPath(statePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка нормализации путей. DownloadPath: {downloadPath}, StatePath: {statePath}, Ошибка: {ex.Message}");
                // Продолжаем с исходными путями
            }
            
            System.Diagnostics.Debug.WriteLine($"Создание папок. DownloadPath: {downloadPath}, StatePath: {statePath}");
            
            // Создание директорий (CreateDirectory создает все необходимые поддиректории)
            // Создаем папку загрузок
            try
            {
                if (!Directory.Exists(downloadPath))
                {
                    Directory.CreateDirectory(downloadPath);
                    System.Diagnostics.Debug.WriteLine($"Попытка создания папки загрузок: {downloadPath}");
                }
                
                if (!Directory.Exists(downloadPath))
                {
                    var error = $"Не удалось создать директорию загрузок: {downloadPath}";
                    System.Diagnostics.Debug.WriteLine(error);
                    throw new InvalidOperationException(error);
                }
                System.Diagnostics.Debug.WriteLine($"Папка загрузок создана/существует: {downloadPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ОШИБКА создания папки загрузок: {downloadPath}, Ошибка: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
            
            // Создаем папку состояний
            try
            {
                if (!Directory.Exists(statePath))
                {
                    Directory.CreateDirectory(statePath);
                    System.Diagnostics.Debug.WriteLine($"Попытка создания папки состояний: {statePath}");
                }
                
                if (!Directory.Exists(statePath))
                {
                    var error = $"Не удалось создать директорию состояний: {statePath}";
                    System.Diagnostics.Debug.WriteLine(error);
                    throw new InvalidOperationException(error);
                }
                System.Diagnostics.Debug.WriteLine($"Папка состояний создана/существует: {statePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ОШИБКА создания папки состояний: {statePath}, Ошибка: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
            
            // Логируем успешное создание (если Logger инициализирован)
            try
            {
                Logger.LogInfo($"Директории созданы. DownloadPath: {downloadPath}, StatePath: {statePath}");
            }
            catch
            {
                // Logger может быть не инициализирован, игнорируем
            }
            
            // Создание опций трекера
            var trackerOptions = new TrackerClientOptions(
                appSettings.TrackerCookies,
                appSettings.TrackerHeaders);
            
            // Создание TorrentStateManager (DIP - через интерфейс)
            var stateStorage = new TorrentStateManager(statePath);
            
            // Создание TorrentManager с внедрением зависимостей (DIP)
            // Передаем null для ITorrentClient - он будет создан внутри TorrentManager
            var torrentManager = new TorrentManager(downloadPath, statePath, stateStorage, null, trackerOptions, appSettings);
            
            // Создание UI сервисов
            var updateThrottler = new UpdateThrottler();
            var listViewUpdater = new TorrentListViewUpdater(updateThrottler);
            var operationsService = new TorrentOperationsService(torrentManager, settingsManager);
            
            // Инициализация HttpClientService (синглтон) - проверяем, что он доступен
            _ = HttpClientService.Instance;
            
            return new ApplicationDependencies
            {
                SettingsManager = settingsManager,
                TorrentManager = torrentManager,
                OperationsService = operationsService,
                UpdateThrottler = updateThrottler,
                ListViewUpdater = listViewUpdater,
                AppSettings = appSettings,
                DownloadPath = downloadPath,
                StatePath = statePath
            };
        }
    }
    
    /// <summary>
    /// Контейнер для всех зависимостей приложения
    /// </summary>
    public class ApplicationDependencies
    {
        public required IAppSettingsManager SettingsManager { get; init; }
        public required ITorrentManager TorrentManager { get; init; }
        public required ITorrentOperationsService OperationsService { get; init; }
        public required IUpdateThrottler UpdateThrottler { get; init; }
        public required ITorrentListViewUpdater ListViewUpdater { get; init; }
        public required AppSettings AppSettings { get; init; }
        public required string DownloadPath { get; init; }
        public required string StatePath { get; init; }
    }
}

