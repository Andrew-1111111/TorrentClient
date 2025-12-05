using TorrentClient.Core.Interfaces;
using TorrentClient.Engine.Interfaces;
using TorrentClient.Utilities;

namespace TorrentClient.Core
{
    /// <summary>
    /// Менеджер торрентов - использует рабочую реализацию из Engine
    /// </summary>
    public class TorrentManager : ITorrentManager
    {
        #region Константы
        
        /// <summary>Интервал автосохранения состояния (секунды)</summary>
        private const int AutoSaveIntervalSeconds = 30;
        
        #endregion
        
        #region Поля

        private readonly ITorrentClient _client;
        private readonly Dictionary<string, Torrent> _uiTorrents = []; // Ключ: torrent.Id (GUID)
        private readonly Dictionary<string, string> _infoHashToId = []; // Маппинг: InfoHashHex -> Id
        private readonly Lock _torrentsLock = new(); // Блокировка для потокобезопасности (.NET 9+)
        private readonly ITorrentStateStorage _stateStorage;
        private readonly string _statePath;
        private readonly string _downloadPath;
        private AppSettings? _appSettings;
        
        // Периодическое сохранение состояния
        private System.Threading.Timer? _saveTimer;
        private DateTime _lastSaveTime = DateTime.MinValue;
        private bool _disposed;
        
        // Реализация колбэков для Client
        private readonly TorrentClientCallbacks _clientCallbacks;
        
        /// <summary>Словарь для отслеживания инициализируемых торрентов</summary>
        private readonly HashSet<string> _initializingTorrents = [];

        #endregion

        #region Асинхронные колбэки (замена событий)

        private ITorrentManagerCallbacks? _callbacks;

        /// <summary>
        /// Устанавливает колбэки для замены событий
        /// </summary>
        /// <param name="callbacks">Колбэки для обработки событий менеджера торрентов</param>
        public void SetCallbacks(ITorrentManagerCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        #region Конструктор

        public TorrentManager(string downloadPath, string statePath, 
            TrackerClientOptions? trackerOptions = null, AppSettings? appSettings = null)
            : this(downloadPath, statePath, new TorrentStateManager(statePath), null, trackerOptions, appSettings)
        {
        }
        
        /// <summary>
        /// Конструктор с внедрением зависимостей (DIP)
        /// </summary>
        public TorrentManager(string downloadPath, string statePath, 
            ITorrentStateStorage stateStorage,
            ITorrentClient? torrentClient = null,
            TrackerClientOptions? _ = null, AppSettings? appSettings = null)
        {
            Logger.Initialize();
            Logger.LogInfo($"Инициализация TorrentManager. Путь загрузки: {downloadPath}, Путь состояний: {statePath}");
            
            _statePath = statePath;
            _downloadPath = downloadPath;
            _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
            _appSettings = appSettings;

            Directory.CreateDirectory(downloadPath);
            Directory.CreateDirectory(statePath);

            // Создаём колбэки для Client
            _clientCallbacks = new TorrentClientCallbacks(this);
            
            // Используем переданный клиент или создаём новый (DIP - через интерфейс)
            _client = torrentClient ?? new Engine.Client(downloadPath, 0, _clientCallbacks);
            
            // Запускаем таймер периодического сохранения
            _saveTimer = new System.Threading.Timer(SaveAllTorrentsCallback, null, 
                TimeSpan.FromSeconds(AutoSaveIntervalSeconds), 
                TimeSpan.FromSeconds(AutoSaveIntervalSeconds));
        }
        
        /// <summary>
        /// Обработчик прогресса торрента (вызывается из колбэков)
        /// </summary>
        internal Task OnClientTorrentProgressAsync(ActiveTorrent activeTorrent)
        {
            var uiTorrent = GetTorrentByInfoHash(activeTorrent.Metadata.InfoHashHex);
            if (uiTorrent != null)
            {
                SyncTorrentState(activeTorrent, uiTorrent);
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                }
                
                // Периодическое сохранение прогресса (раз в 30 секунд)
                TrySaveProgressPeriodically(uiTorrent);
            }
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Обработчик завершения торрента (вызывается из колбэков)
        /// </summary>
        internal Task OnClientTorrentCompleteAsync(ActiveTorrent activeTorrent)
        {
            var uiTorrent = GetTorrentByInfoHash(activeTorrent.Metadata.InfoHashHex);
            if (uiTorrent != null)
            {
                uiTorrent.State = TorrentState.Seeding;
                uiTorrent.CompletedDate = DateTime.Now;
                _stateStorage.SaveTorrentState(uiTorrent);
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                }
                Logger.LogInfo($"[TorrentManager] Торрент завершён, состояние сохранено: {uiTorrent.Info.Name}");
            }
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Периодическое сохранение прогресса (не чаще чем раз в 30 секунд)
        /// </summary>
        private void TrySaveProgressPeriodically(Torrent uiTorrent)
        {
            var now = DateTime.Now;
            if ((now - _lastSaveTime).TotalSeconds >= AutoSaveIntervalSeconds)
            {
                _lastSaveTime = now;
                _stateStorage.SaveTorrentState(uiTorrent);
                Logger.LogInfo($"[TorrentManager] Прогресс сохранён: {uiTorrent.Info.Name} ({uiTorrent.Progress:F1}%)");
            }
        }
        
        /// <summary>
        /// Callback для таймера сохранения
        /// </summary>
        private void SaveAllTorrentsCallback(object? state)
        {
            if (_disposed) return;
            
            try
            {
                List<Torrent> torrents;
                using (_torrentsLock.EnterScope())
                {
                    torrents = [.. _uiTorrents.Values];
                }
                
                foreach (var uiTorrent in torrents)
                {
                    if (uiTorrent.State == TorrentState.Downloading)
                    {
                        var activeTorrent = _client.GetTorrent(uiTorrent.Info.InfoHash);
                        if (activeTorrent != null)
                        {
                            SyncTorrentState(activeTorrent, uiTorrent);
                            _stateStorage.SaveTorrentState(uiTorrent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[TorrentManager] Ошибка периодического сохранения", ex);
            }
        }
        
        /// <summary>
        /// Находит торрент по InfoHash (потокобезопасно)
        /// </summary>
        private Torrent? GetTorrentByInfoHash(string infoHashHex)
        {
            using (_torrentsLock.EnterScope())
            {
                if (_infoHashToId.TryGetValue(infoHashHex, out var id))
                {
                    return _uiTorrents.TryGetValue(id, out var torrent) ? torrent : null;
                }
                return null;
            }
        }

        #endregion

        #region Управление торрентами

        /// <summary>
        /// Добавляет торрент из файла
        /// </summary>
        /// <param name="torrentFilePath">Путь к файлу .torrent</param>
        /// <param name="downloadPath">Путь для загрузки (опционально, если не указан - используется путь по умолчанию)</param>
        /// <returns>Добавленный торрент или null, если произошла ошибка</returns>
        /// <exception cref="Exception">Выбрасывается при ошибке парсинга или добавления торрента</exception>
        public Torrent? AddTorrent(string torrentFilePath, string? downloadPath = null)
        {
            try
            {
                // Парсим через Engine
                var metadata = TorrentParser.Parse(torrentFilePath);
                
                Torrent uiTorrent;
                
                using (_torrentsLock.EnterScope())
                {
                    // Проверяем, не добавлен ли уже этот торрент
                    if (_infoHashToId.TryGetValue(metadata.InfoHashHex, out var existingId))
                    {
                        Logger.LogWarning($"[TorrentManager] Торрент уже добавлен: {metadata.Name}");
                        return _uiTorrents.TryGetValue(existingId, out var existing) ? existing : null;
                    }
                    
                    // Создаём UI торрент
                    uiTorrent = CreateUITorrent(metadata, torrentFilePath, downloadPath);
                    
                    // Загружаем сохраненное состояние
                    var savedState = _stateStorage.LoadTorrentState(metadata.InfoHashHex);
                    _stateStorage.RestoreTorrentState(uiTorrent, savedState);

                    // Используем torrent.Id как ключ (соответствует Tag в ListView)
                    _uiTorrents[uiTorrent.Id] = uiTorrent;
                    // Добавляем маппинг InfoHash -> Id для событий Engine
                    _infoHashToId[metadata.InfoHashHex] = uiTorrent.Id;
                }
                
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentAddedAsync(uiTorrent).ConfigureAwait(false));
                }
                
                // Сохраняем состояние торрента И список торрентов
                _stateStorage.SaveTorrentState(uiTorrent);
                
                List<Torrent> allTorrents;
                using (_torrentsLock.EnterScope())
                {
                    allTorrents = [.. _uiTorrents.Values];
                }
                _stateStorage.SaveTorrentList(allTorrents);
                
                Logger.LogInfo($"[TorrentManager] Торрент добавлен и сохранён: {uiTorrent.Info.Name}");
                
                // КРИТИЧНО: Сразу добавляем в Engine и инициализируем в фоне (для быстрого старта)
                // Защита от накопления задач - если уже инициализируется, пропускаем
                if (!_initializingTorrents.Contains(uiTorrent.Id))
                {
                    _initializingTorrents.Add(uiTorrent.Id);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await InitializeTorrentInEngineAsync(uiTorrent).ConfigureAwait(false);
                        }
                        finally
                        {
                            _initializingTorrents.Remove(uiTorrent.Id);
                        }
                    });
                }

                return uiTorrent;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка добавления торрента: {ex.Message}", ex);
                throw new Exception($"Ошибка добавления торрента: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Инициализирует торрент в Engine в фоновом режиме
        /// </summary>
        private async Task InitializeTorrentInEngineAsync(Torrent uiTorrent)
        {
            try
            {
                Logger.LogInfo($"[TorrentManager] Инициализация торрента в Engine: {uiTorrent.Info.Name}");
                var metadata = TorrentParser.Parse(uiTorrent.TorrentFilePath);
                var activeTorrent = await _client.AddTorrentAsync(metadata, uiTorrent.DownloadPath);
                
                // Применяем глобальные настройки Swarm
                if (_appSettings != null)
                {
                    activeTorrent.ApplySwarmSettings(
                        _appSettings.MaxConnections,
                        _appSettings.MaxHalfOpenConnections,
                        _appSettings.MaxPiecesToRequest,
                        _appSettings.MaxRequestsPerPeer);
                }
                
                // Применяем сохранённые ограничения скорости (включая null для сброса ограничений)
                activeTorrent.MaxDownloadSpeed = uiTorrent.MaxDownloadSpeed;
                activeTorrent.MaxUploadSpeed = uiTorrent.MaxUploadSpeed;
                if (uiTorrent.MaxDownloadSpeed.HasValue || uiTorrent.MaxUploadSpeed.HasValue)
                {
                    Logger.LogInfo($"[TorrentManager] Применены ограничения скорости для {uiTorrent.Info.Name}: загрузка={FormatSpeed(uiTorrent.MaxDownloadSpeed)}, отдача={FormatSpeed(uiTorrent.MaxUploadSpeed)}");
                }

                // Синхронизируем прогресс из Engine
                SyncTorrentState(activeTorrent, uiTorrent);
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                }
                
                Logger.LogInfo($"[TorrentManager] Торрент инициализирован в Engine: {uiTorrent.Info.Name} ({uiTorrent.Progress:F1}%)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TorrentManager] Ошибка инициализации торрента в Engine: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Удаляет торрент
        /// </summary>
        /// <param name="torrentId">ID торрента</param>
        /// <param name="deleteFiles">Удалить загруженные файлы</param>
        public async Task RemoveTorrentAsync(string torrentId, bool deleteFiles = false)
        {
            Torrent? uiTorrent;
            string infoHash;
            string torrentName;
            string downloadPath;
            
            using (_torrentsLock.EnterScope())
            {
                if (!_uiTorrents.TryGetValue(torrentId, out uiTorrent))
                    return;
                    
                infoHash = uiTorrent.Info.InfoHash;
                torrentName = uiTorrent.Info.Name;
                downloadPath = uiTorrent.DownloadPath;
                
                // Удаляем из словарей
                _uiTorrents.Remove(torrentId);
                _infoHashToId.Remove(infoHash);
            }
            
            // Останавливаем и удаляем в Engine (передаём deleteFiles)
            await _client.RemoveTorrentAsync(infoHash, deleteFiles).ConfigureAwait(false);
            
            // Если Engine не удалил файлы (торрент не был в Engine), удаляем вручную
            if (deleteFiles)
            {
                try
                {
                    var fullPath = Path.Combine(downloadPath, torrentName);
                    if (Directory.Exists(fullPath))
                    {
                        Directory.Delete(fullPath, true);
                        Logger.LogInfo($"[TorrentManager] Удалены файлы торрента: {fullPath}");
                    }
                    else if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        Logger.LogInfo($"[TorrentManager] Удалён файл торрента: {fullPath}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[TorrentManager] Не удалось удалить файлы: {ex.Message}");
                }
            }
            
            // Удаляем состояние
            _stateStorage.DeleteTorrentState(infoHash);
            
            // Сохраняем список торрентов
            List<Torrent> allTorrents;
            using (_torrentsLock.EnterScope())
            {
                allTorrents = [.. _uiTorrents.Values];
            }
            _stateStorage.SaveTorrentList(allTorrents);
            
            Logger.LogInfo($"[TorrentManager] Торрент удалён: {torrentName}, файлы удалены: {deleteFiles}");
            // Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentRemovedAsync(uiTorrent).ConfigureAwait(false));
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Удаляет несколько торрентов
        /// </summary>
        /// <param name="torrentIds">Список ID торрентов для удаления</param>
        /// <param name="deleteFiles">Удалить загруженные файлы</param>
        /// <summary>
        /// Удаляет несколько торрентов
        /// </summary>
        /// <param name="torrentIds">Список идентификаторов торрентов</param>
        /// <param name="deleteFiles">Удалять ли загруженные файлы</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task RemoveTorrentsAsync(IEnumerable<string> torrentIds, bool deleteFiles = false)
        {
            var idsList = torrentIds.ToList();
            if (idsList.Count == 0)
                return;

            List<Torrent> removedTorrents = [];
            List<string> errors = [];

            foreach (var torrentId in idsList)
            {
                try
                {
                    Torrent? uiTorrent;
                    string infoHash;
                    string torrentName;
                    string downloadPath;
                    
                    using (_torrentsLock.EnterScope())
                    {
                        if (!_uiTorrents.TryGetValue(torrentId, out uiTorrent))
                            continue;
                            
                        infoHash = uiTorrent.Info.InfoHash;
                        torrentName = uiTorrent.Info.Name;
                        downloadPath = uiTorrent.DownloadPath;
                        
                        // Удаляем из словарей
                        _uiTorrents.Remove(torrentId);
                        _infoHashToId.Remove(infoHash);
                    }
                    
                    // Останавливаем и удаляем в Engine (передаём deleteFiles)
                    await _client.RemoveTorrentAsync(infoHash, deleteFiles).ConfigureAwait(false);
                    
                    // Если Engine не удалил файлы (торрент не был в Engine), удаляем вручную
                    if (deleteFiles)
                    {
                        try
                        {
                            var fullPath = Path.Combine(downloadPath, torrentName);
                            if (Directory.Exists(fullPath))
                            {
                                Directory.Delete(fullPath, true);
                                Logger.LogInfo($"[TorrentManager] Удалены файлы торрента: {fullPath}");
                            }
                            else if (File.Exists(fullPath))
                            {
                                File.Delete(fullPath);
                                Logger.LogInfo($"[TorrentManager] Удалён файл торрента: {fullPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[TorrentManager] Не удалось удалить файлы торрента {torrentName}: {ex.Message}");
                            errors.Add($"{torrentName}: {ex.Message}");
                        }
                    }
                    
                    // Удаляем состояние
                    _stateStorage.DeleteTorrentState(infoHash);
                    
                    removedTorrents.Add(uiTorrent);
                    Logger.LogInfo($"[TorrentManager] Торрент удалён: {torrentName}, файлы удалены: {deleteFiles}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[TorrentManager] Ошибка удаления торрента {torrentId}: {ex.Message}", ex);
                    errors.Add($"Ошибка удаления: {ex.Message}");
                }
            }
            
            // Сохраняем список торрентов после всех удалений
            List<Torrent> allTorrents;
            using (_torrentsLock.EnterScope())
            {
                allTorrents = [.. _uiTorrents.Values];
            }
            _stateStorage.SaveTorrentList(allTorrents);
            
            // Вызываем колбэки для каждого удалённого торрента
            if (_callbacks != null)
            {
                foreach (var torrent in removedTorrents)
                {
                    var torrentCopy = torrent; // Захватываем копию для лямбды
                    SafeTaskRunner.RunSafe(async () => await _callbacks.OnTorrentRemovedAsync(torrentCopy).ConfigureAwait(false));
                }
            }
            
            if (errors.Count > 0)
            {
                Logger.LogWarning($"[TorrentManager] При удалении {idsList.Count} торрентов произошло {errors.Count} ошибок");
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Запускает загрузку торрента
        /// </summary>
        /// <param name="torrentId">Идентификатор торрента</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task StartTorrentAsync(string torrentId)
        {
            Torrent? uiTorrent;
            using (_torrentsLock.EnterScope())
            {
                _uiTorrents.TryGetValue(torrentId, out uiTorrent);
            }
            
            if (uiTorrent != null)
            {
                var infoHash = uiTorrent.Info.InfoHash;
                
                // Устанавливаем статус проверки
                uiTorrent.State = TorrentState.Checking;
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                }
                
                // Получаем или создаём торрент в Engine
                var activeTorrent = _client.GetTorrent(infoHash);
                if (activeTorrent == null)
                {
                    Logger.LogInfo($"[TorrentManager] Добавление торрента в Engine: {uiTorrent.Info.Name}");
                    var metadata = TorrentParser.Parse(uiTorrent.TorrentFilePath);
                    activeTorrent = await _client.AddTorrentAsync(metadata, uiTorrent.DownloadPath);
                }
                else
                {
                    // Торрент уже в Engine, но возможно ещё инициализируется - ждём завершения
                    Logger.LogInfo($"[TorrentManager] Ожидание инициализации торрента: {uiTorrent.Info.Name}");
                    await activeTorrent.WaitForInitializationAsync();
                }
                
                // Применяем глобальные настройки Swarm (критично для применения изменённых настроек)
                if (_appSettings != null)
                {
                    activeTorrent.ApplySwarmSettings(
                        _appSettings.MaxConnections,
                        _appSettings.MaxHalfOpenConnections,
                        _appSettings.MaxPiecesToRequest,
                        _appSettings.MaxRequestsPerPeer);
                }
                
                // Применяем сохранённые ограничения скорости (включая null для сброса ограничений)
                activeTorrent.MaxDownloadSpeed = uiTorrent.MaxDownloadSpeed;
                activeTorrent.MaxUploadSpeed = uiTorrent.MaxUploadSpeed;
                if (uiTorrent.MaxDownloadSpeed.HasValue || uiTorrent.MaxUploadSpeed.HasValue)
                {
                    Logger.LogInfo($"[TorrentManager] Восстановлены ограничения скорости для {uiTorrent.Info.Name}: загрузка={FormatSpeed(uiTorrent.MaxDownloadSpeed)}, отдача={FormatSpeed(uiTorrent.MaxUploadSpeed)}");
                }
                
                // Запускаем
                activeTorrent.Start();
                uiTorrent.State = activeTorrent.IsComplete ? TorrentState.Seeding : TorrentState.Downloading;
                
                Logger.LogInfo($"[TorrentManager] Торрент запущен: {uiTorrent.Info.Name}");
                // Используем уже захваченную ссылку callbacks из выше
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                }
            }
        }
        
        /// <summary>
        /// Приостанавливает загрузку торрента
        /// </summary>
        /// <param name="torrentId">Идентификатор торрента</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task PauseTorrentAsync(string torrentId)
        {
            Torrent? uiTorrent;
            using (_torrentsLock.EnterScope())
            {
                _uiTorrents.TryGetValue(torrentId, out uiTorrent);
            }
            
            if (uiTorrent != null)
            {
                var activeTorrent = _client.GetTorrent(uiTorrent.Info.InfoHash);
                if (activeTorrent != null)
                {
                    // Синхронизируем состояние перед паузой
                    SyncTorrentState(activeTorrent, uiTorrent);
                    activeTorrent.Stop();
                }
                
                uiTorrent.State = TorrentState.Paused;
                // Сбрасываем скорость при паузе
                uiTorrent.DownloadSpeed = 0;
                
                // Сохраняем состояние при паузе (асинхронно на ThreadPool)
                await Task.Run(() => _stateStorage.SaveTorrentState(uiTorrent)).ConfigureAwait(false);
                Logger.LogInfo($"[TorrentManager] Торрент приостановлен, состояние сохранено: {uiTorrent.Info.Name} ({uiTorrent.Progress:F1}%)");
                
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                }
            }
        }

        /// <summary>
        /// Останавливает загрузку торрента
        /// </summary>
        /// <param name="torrentId">Идентификатор торрента</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task StopTorrentAsync(string torrentId)
        {
            await Task.Run(() =>
            {
                Torrent? uiTorrent;
                using (_torrentsLock.EnterScope())
                {
                    _uiTorrents.TryGetValue(torrentId, out uiTorrent);
                }
                
                if (uiTorrent != null)
                {
                    var activeTorrent = _client.GetTorrent(uiTorrent.Info.InfoHash);
                    if (activeTorrent != null)
                    {
                        // Синхронизируем состояние перед остановкой
                        SyncTorrentState(activeTorrent, uiTorrent);
                        activeTorrent.Stop();
                    }
                    
                    uiTorrent.State = TorrentState.Stopped;
                    // Сбрасываем скорость при остановке
                    uiTorrent.DownloadSpeed = 0;
                    
                    // Сохраняем состояние при остановке
                    _stateStorage.SaveTorrentState(uiTorrent);
                    Logger.LogInfo($"[TorrentManager] Торрент остановлен, состояние сохранено: {uiTorrent.Info.Name} ({uiTorrent.Progress:F1}%)");
                    
                    if (_callbacks != null)
                    {
                        _ = Task.Run(async () => await _callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
                    }
                }
            }).ConfigureAwait(false);
        }

        #endregion

        #region Настройки

        /// <summary>
        /// Устанавливает лимит скорости для торрента
        /// </summary>
        /// <param name="torrentId">Идентификатор торрента</param>
        /// <param name="maxDownload">Максимальная скорость загрузки в байтах/сек (null - без ограничений)</param>
        /// <param name="maxUpload">Максимальная скорость отдачи в байтах/сек (null - без ограничений)</param>
        public void SetTorrentSpeedLimit(string torrentId, long? maxDownload, long? maxUpload)
        {
            Torrent? uiTorrent;
            using (_torrentsLock.EnterScope())
            {
                _uiTorrents.TryGetValue(torrentId, out uiTorrent);
            }

            if (uiTorrent != null)
            {
                uiTorrent.MaxDownloadSpeed = maxDownload;
                uiTorrent.MaxUploadSpeed = maxUpload;

                // Применяем ограничение к Engine
                var activeTorrent = _client.GetTorrent(uiTorrent.Info.InfoHash);
                if (activeTorrent != null)
                {
                    activeTorrent.MaxDownloadSpeed = maxDownload;
                    activeTorrent.MaxUploadSpeed = maxUpload;
                    Logger.LogInfo($"[TorrentManager] Ограничение скорости применено: загрузка={FormatSpeed(maxDownload)}, отдача={FormatSpeed(maxUpload)}");
                }
                else
                {
                    Logger.LogWarning($"[TorrentManager] Не удалось применить ограничение скорости - торрент не найден в Engine: {uiTorrent.Info.Name}");
                }

                // Сохраняем состояние торрента с новыми лимитами
                _stateStorage.SaveTorrentState(uiTorrent);
            }
        }

        /// <summary>
        /// Устанавливает приоритет файла в торренте
        /// </summary>
        /// <param name="torrentId">Идентификатор торрента</param>
        /// <param name="filePath">Путь к файлу относительно корня торрента</param>
        /// <param name="priority">Приоритет (0=низкий, 1=нормальный, 2=высокий)</param>
        public void SetFilePriority(string torrentId, string filePath, int priority)
        {
            Torrent? uiTorrent;
            using (_torrentsLock.EnterScope())
            {
                _uiTorrents.TryGetValue(torrentId, out uiTorrent);
            }

            if (uiTorrent == null)
                return;

            // Находим файл и устанавливаем приоритет
            var fileInfo = uiTorrent.FileInfos.FirstOrDefault(f => f.Path == filePath);
            if (fileInfo != null)
            {
                fileInfo.Priority = Math.Clamp(priority, 0, 2);
                Logger.LogInfo($"[TorrentManager] Приоритет файла установлен: {filePath} = {priority}");

                // Сохраняем состояние торрента
                _stateStorage.SaveTorrentState(uiTorrent);

                // TODO: Обновить PiecePicker в TorrentDownloader при следующем запуске/перезапуске
                // Это требует доступа к TorrentDownloader, который находится в Engine
            }
            
            // Захватываем ссылку в локальную переменную для предотвращения race condition
            // uiTorrent гарантированно не null здесь, так как мы вернулись раньше если он null
            var callbacks = _callbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentUpdatedAsync(uiTorrent).ConfigureAwait(false));
            }
        }

        private static string FormatSpeed(long? bytesPerSecond)
        {
            if (bytesPerSecond == null) return "без ограничений";
            // Конвертация согласно стандарту: https://en.wikipedia.org/wiki/Data-rate_units
            // 1 Mbps = 1,000,000 bits/s = 125,000 bytes/s
            var mbps = bytesPerSecond.Value * 8.0 / 1_000_000.0;
            if (mbps >= 1.0)
                return $"{mbps:F1} Mbps";
            var kbps = mbps * 1000.0;
            return $"{kbps:F1} Kbps";
        }

        /// <summary>
        /// Обновляет глобальные настройки приложения
        /// </summary>
        /// <param name="settings">Настройки приложения</param>
        public void UpdateGlobalSettings(AppSettings settings)
        {
            _appSettings = settings;
            Logger.LogInfo($"Глобальные настройки обновлены");
        }
        
        /// <summary>
        /// Применяет глобальные настройки ко всем активным торрентам
        /// </summary>
        /// <param name="maxConnections">Максимальное количество соединений</param>
        /// <param name="maxHalfOpen">Максимальное количество полуоткрытых соединений</param>
        /// <param name="maxPieces">Максимальное количество кусков для запроса</param>
        /// <param name="maxRequestsPerWire">Максимальное количество запросов на соединение</param>
        public void ApplyGlobalSettings(int maxConnections, int maxHalfOpen, int maxPieces, int maxRequestsPerWire)
        {
            // Обновляем локальные настройки
            if (_appSettings != null)
            {
                _appSettings.MaxConnections = maxConnections;
                _appSettings.MaxHalfOpenConnections = maxHalfOpen;
                _appSettings.MaxPiecesToRequest = maxPieces;
                _appSettings.MaxRequestsPerPeer = maxRequestsPerWire;
            }
            
            // Применяем ко всем активным торрентам
            var torrents = _client.GetAllTorrents();
            foreach (var activeTorrent in torrents)
            {
                activeTorrent.ApplySwarmSettings(maxConnections, maxHalfOpen, maxPieces, maxRequestsPerWire);
            }
            
            Logger.LogInfo($"[TorrentManager] Настройки применены: соед={maxConnections}, полуоткр={maxHalfOpen}, кусков={maxPieces}, запр/соед={maxRequestsPerWire}");
        }

        #endregion

        #region Получение данных

        /// <summary>
        /// Получает список всех торрентов
        /// </summary>
        /// <returns>Список всех торрентов</returns>
        public List<Torrent> GetAllTorrents()
        {
            using (_torrentsLock.EnterScope())
            {
                return [.. _uiTorrents.Values];
            }
        }

        /// <summary>
        /// Получает торрент по идентификатору
        /// </summary>
        /// <param name="torrentId">Идентификатор торрента</param>
        /// <returns>Торрент или null, если не найден</returns>
        public Torrent? GetTorrent(string torrentId)
        {
            using (_torrentsLock.EnterScope())
            {
                return _uiTorrents.TryGetValue(torrentId, out var torrent) ? torrent : null;
            }
        }
        
        /// <summary>
        /// Синхронизирует состояние всех торрентов с Engine
        /// </summary>
        public void SyncAllTorrentsState()
        {
            List<Torrent> torrents;
            using (_torrentsLock.EnterScope())
            {
                torrents = [.. _uiTorrents.Values];
            }
            
            foreach (var uiTorrent in torrents)
            {
                var activeTorrent = _client.GetTorrent(uiTorrent.Info.InfoHash);
                if (activeTorrent != null)
                {
                    SyncTorrentState(activeTorrent, uiTorrent);
                }
            }
        }

        #endregion

        #region Сохранение/загрузка

        /// <summary>
        /// Загружает сохранённые торренты из хранилища
        /// </summary>
        public void LoadSavedTorrents()
        {
            try
            {
                var savedTorrents = _stateStorage.LoadTorrentList();
                foreach (var savedState in savedTorrents)
                {
                    if (!File.Exists(savedState.TorrentFilePath))
                    {
                        Logger.LogWarning($"Файл торрента не найден: {savedState.TorrentFilePath}");
                        continue;
                    }

                    try
                    {
                        var torrent = AddTorrent(savedState.TorrentFilePath, savedState.DownloadPath);
                        if (torrent != null)
                            Logger.LogInfo($"Загружен торрент: {torrent.Info.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Ошибка загрузки торрента: {savedState.TorrentFilePath}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка загрузки сохраненных торрентов", ex);
            }
        }

        /// <summary>
        /// Сохраняет состояние всех торрентов в хранилище
        /// </summary>
        public void SaveAllTorrents()
        {
            try
            {
                List<Torrent> allTorrents;
                using (_torrentsLock.EnterScope())
                {
                    allTorrents = [.. _uiTorrents.Values];
                }
                
                // Синхронизируем состояние с Engine перед сохранением
                foreach (var uiTorrent in allTorrents)
                {
                    var activeTorrent = _client.GetTorrent(uiTorrent.Info.InfoHash);
                    if (activeTorrent != null)
                    {
                        SyncTorrentState(activeTorrent, uiTorrent);
                    }
                    _stateStorage.SaveTorrentState(uiTorrent);
                }
                
                _stateStorage.SaveTorrentList(allTorrents);
                Logger.LogInfo($"[TorrentManager] Сохранено {allTorrents.Count} торрентов");
            }
            catch (Exception ex)
            {
                Logger.LogError("[TorrentManager] Ошибка сохранения торрентов", ex);
            }
        }

        #endregion

        #region Освобождение ресурсов
        
        private void ClearEventsAndUnsubscribe()
        {
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            ClearEventsAndUnsubscribe();
            
            // Останавливаем таймер сохранения
            _saveTimer?.Dispose();
            _saveTimer = null;
            
            // Сохраняем состояние всех торрентов перед закрытием
            SaveAllTorrents();
            
            _initializingTorrents.Clear();
            
            _client.Dispose();
            if (_stateStorage is IDisposable disposable)
            {
                disposable.Dispose();
            }
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            
            ClearEventsAndUnsubscribe();
            
            // Останавливаем таймер сохранения
            _saveTimer?.Dispose();
            _saveTimer = null;
            
            // Сохраняем состояние всех торрентов перед закрытием
            SaveAllTorrents();
            
            _initializingTorrents.Clear();
            
            await _client.DisposeAsync().ConfigureAwait(false);
            if (_stateStorage is IDisposable disposable)
            {
                disposable.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Вспомогательные методы

        private Torrent CreateUITorrent(TorrentMetadata metadata, string torrentFilePath, string? downloadPath)
        {
            var torrentInfo = new TorrentInfo
            {
                Name = metadata.Name,
                InfoHash = metadata.InfoHashHex,
                TotalSize = metadata.TotalLength,
                PieceLength = metadata.PieceLength,
                PieceCount = metadata.PieceCount, // Только количество, без хранения хэшей (экономия памяти)
                Files = [.. metadata.Files.Select(f => new TorrentFile
                {
                    Path = f.Path,
                    Length = f.Length,
                    Offset = f.Offset
                })],
                AnnounceUrls = metadata.Trackers,
                Comment = metadata.Comment,
                CreatedBy = metadata.CreatedBy,
                CreationDate = metadata.CreationDate
            };

            var torrent = new Torrent
            {
                Info = torrentInfo,
                DownloadPath = downloadPath ?? _downloadPath,
                TorrentFilePath = torrentFilePath,
                BitField = new BitField(metadata.PieceCount),
                FileInfos = [.. metadata.Files.Select(f => new FileDownloadInfo
                {
                    Path = f.Path,
                    Length = f.Length,
                    Downloaded = 0,
                    IsSelected = true
                })]
            };

            return torrent;
        }

        private static void SyncTorrentState(ActiveTorrent activeTorrent, Torrent uiTorrent)
        {
            // Синхронизируем загруженные байты (используем Downloaded из Engine)
            uiTorrent.DownloadedBytes = activeTorrent.Downloaded;
            uiTorrent.UploadedBytes = activeTorrent.Uploaded;
            uiTorrent.DownloadSpeed = activeTorrent.DownloadSpeed;
            uiTorrent.UploadSpeed = activeTorrent.UploadSpeed;
            uiTorrent.ConnectedPeers = activeTorrent.ConnectedPeers;
            uiTorrent.ActivePeers = activeTorrent.ActivePeers; // Активные (разблокированные) пиры
            uiTorrent.TotalPeers = activeTorrent.TotalPeers; // Все известные пиры
            
            // Обновляем BitField (критично для докачки!)
            if (uiTorrent.BitField != null && activeTorrent.Bitfield != null)
            {
                int completedPieces = 0;
                for (int i = 0; i < Math.Min(uiTorrent.BitField.Length, activeTorrent.Bitfield.Length); i++)
                {
                    uiTorrent.BitField[i] = activeTorrent.Bitfield[i];
                    if (activeTorrent.Bitfield[i])
                        completedPieces++;
                }
                
                // Логируем прогресс для диагностики
                if (completedPieces > 0 && completedPieces % 10 == 0)
                {
                    Logger.LogInfo($"[TorrentManager] {uiTorrent.Info.Name}: {completedPieces}/{uiTorrent.BitField.Length} кусков завершено");
                }
            }
            
            // Обновляем состояние
            if (activeTorrent.IsComplete)
                uiTorrent.State = TorrentState.Seeding;
            else if (activeTorrent.Status == TorrentStatus.Downloading)
                uiTorrent.State = TorrentState.Downloading;
        }

        #endregion
    }
}
