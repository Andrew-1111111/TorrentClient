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
            
            // Настройка логирования
            Logger.SetEnabled(appSettings.EnableLogging);
            
            // Настройка глобального лимитера скорости
            GlobalSpeedLimiter.Instance.UpdateLimits(
                appSettings.GlobalMaxDownloadSpeed,
                appSettings.GlobalMaxUploadSpeed);
            
            // Определение путей
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var downloadPath = appSettings.DefaultDownloadPath ?? 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Torrents");
            var statePath = appSettings.StatePath ?? 
                Path.Combine(baseDirectory, "States");
            
            // Создание директорий
            Directory.CreateDirectory(downloadPath);
            Directory.CreateDirectory(statePath);
            
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

