using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Главный клиент торрентов - координирует все компоненты
    /// Реализует принцип SRP: управление жизненным циклом торрентов
    /// </summary>
    public class Client : ITorrentClient
    {
        #region Поля

        private readonly byte[] _peerId;
        private readonly int _port;
        private readonly string _downloadPath;
        private readonly Dictionary<string, ActiveTorrent> _torrents = new();
        private readonly Dictionary<string, IActiveTorrentCallbacks> _torrentCallbacks = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ITorrentClientCallbacks? _callbacks;

        #endregion

        #region Свойства

        public byte[] PeerId => _peerId;
        public int Port => _port;
        public string DownloadPath => _downloadPath;

        #endregion

        #region Конструктор

        public Client(string downloadPath, int port = 0, ITorrentClientCallbacks? callbacks = null)
        {
            _downloadPath = downloadPath;
            _peerId = GeneratePeerId();
            _port = port > 0 ? port : FindFreePort();
            _callbacks = callbacks;

            Directory.CreateDirectory(downloadPath);

            Logger.LogInfo($"[Client] Инициализирован");
            Logger.LogInfo($"[Client]   PeerId: {Encoding.ASCII.GetString(_peerId)}");
            Logger.LogInfo($"[Client]   Порт: {_port}");
            Logger.LogInfo($"[Client]   Путь загрузки: {_downloadPath}");
        }

        #endregion

        #region Управление торрентами

        /// <summary>Добавляет торрент из .torrent файла</summary>
        public async Task<ActiveTorrent> AddTorrentAsync(string torrentFilePath, string? customDownloadPath = null)
        {
            var metadata = TorrentParser.Parse(torrentFilePath);
            return await AddTorrentAsync(metadata, customDownloadPath).ConfigureAwait(false);
        }

        /// <summary>Добавляет торрент из метаданных</summary>
        public async Task<ActiveTorrent> AddTorrentAsync(TorrentMetadata metadata, string? customDownloadPath = null)
        {
            var infoHashHex = metadata.InfoHashHex;
            ActiveTorrent torrent;

            // Быстро проверяем и добавляем в словарь, не держим lock долго
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_torrents.ContainsKey(infoHashHex))
                {
                    Logger.LogWarning($"[Client] Торрент уже добавлен: {metadata.Name}");
                    return _torrents[infoHashHex];
                }

                var downloadPath = customDownloadPath ?? _downloadPath;
                torrent = new ActiveTorrent(metadata, _peerId, _port, downloadPath);

                _torrents[infoHashHex] = torrent;
                
                // Создаём колбэки для торрента после создания
                var torrentRef = torrent;
                var torrentCallbacks = new ActiveTorrentCallbacksWrapper(this, () => torrentRef);
                _torrentCallbacks[infoHashHex] = torrentCallbacks;
                
                // Устанавливаем колбэки в торрент
                torrent.SetCallbacks(torrentCallbacks);

                Logger.LogInfo($"[Client] Добавлен торрент: {metadata.Name}");
                
                // КРИТИЧНО: Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentAddedAsync(torrent).ConfigureAwait(false));
                }
            }
            finally
            {
                _lock.Release();
            }

            // Инициализация вне lock - может занять много времени
            await torrent.InitializeAsync().ConfigureAwait(false);

            return torrent;
        }

        /// <summary>Удаляет торрент</summary>
        public async Task RemoveTorrentAsync(string infoHashHex, bool deleteFiles = false)
        {
            ActiveTorrent? torrent;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_torrents.TryGetValue(infoHashHex, out torrent))
                    return;

                _torrents.Remove(infoHashHex);
                
                // Очищаем колбэки перед удалением
                torrent.SetCallbacks(null!);
                _torrentCallbacks.Remove(infoHashHex);
            }
            finally
            {
                _lock.Release();
            }

            torrent.Stop();
            torrent.Dispose();

            if (deleteFiles)
            {
                // Удаляем загруженные файлы
                var downloadPath = Path.Combine(torrent.DownloadPath, torrent.Metadata.Name);
                if (Directory.Exists(downloadPath))
                {
                    try
                    {
                        Directory.Delete(downloadPath, true);
                        Logger.LogInfo($"[Client] Удалены файлы: {downloadPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[Client] Не удалось удалить файлы: {ex.Message}");
                    }
                }
                else if (File.Exists(downloadPath))
                {
                    try
                    {
                        File.Delete(downloadPath);
                        Logger.LogInfo($"[Client] Удалён файл: {downloadPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[Client] Не удалось удалить файл: {ex.Message}");
                    }
                }
            }

            Logger.LogInfo($"[Client] Удалён торрент: {torrent.Metadata.Name}");
            
            // КРИТИЧНО: Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentRemovedAsync(torrent).ConfigureAwait(false));
            }
        }

        /// <summary>Синхронное удаление торрента (для обратной совместимости)</summary>
        public void RemoveTorrent(string infoHashHex, bool deleteFiles = false)
        {
            RemoveTorrentAsync(infoHashHex, deleteFiles).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>Получает торрент по InfoHashHex</summary>
        public async Task<ActiveTorrent?> GetTorrentAsync(string infoHashHex)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _torrents.GetValueOrDefault(infoHashHex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Синхронное получение торрента</summary>
        public ActiveTorrent? GetTorrent(string infoHashHex)
        {
            _lock.Wait();
            try
            {
                return _torrents.GetValueOrDefault(infoHashHex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Получает все торренты</summary>
        public async Task<IReadOnlyList<ActiveTorrent>> GetAllTorrentsAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _torrents.Values.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Синхронное получение всех торрентов</summary>
        public IReadOnlyList<ActiveTorrent> GetAllTorrents()
        {
            _lock.Wait();
            try
            {
                return _torrents.Values.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region Вспомогательные методы

        private static byte[] GeneratePeerId()
        {
            // Формат: -TC2000-XXXXXXXXXXXX
            // TC = TorrentClient, 2000 = версия 2.0.0.0
            var prefix = "-TC2000-";
            var random = new byte[12];
            Random.Shared.NextBytes(random);

            var peerId = new byte[20];
            Encoding.ASCII.GetBytes(prefix, 0, 8, peerId, 0);

            for (int i = 0; i < 12; i++)
            {
                // Используем буквенно-цифровые символы
                peerId[8 + i] = (byte)((random[i] % 62) switch
                {
                    < 10 => '0' + (random[i] % 62),
                    < 36 => 'A' + (random[i] % 62) - 10,
                    _ => 'a' + (random[i] % 62) - 36
                });
            }

            return peerId;
        }

        private static int FindFreePort()
        {
            // Пробуем случайные порты в динамическом диапазоне
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                var port = random.Next(49152, 65535);
                try
                {
                    using var listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (SocketException)
                {
                    continue;
                }
            }

            // Запасной вариант: пусть система выберет
            using var fallbackListener = new TcpListener(IPAddress.Any, 0);
            fallbackListener.Start();
            var chosenPort = ((IPEndPoint)fallbackListener.LocalEndpoint).Port;
            fallbackListener.Stop();
            return chosenPort;
        }

        #endregion

        #region IDisposable / IAsyncDisposable
        
        private void ClearEvents()
        {
            // События заменены на колбэки, очистка не требуется
        }

        public void Dispose()
        {
            ClearEvents();
            
            _lock.Wait();
            try
            {
                foreach (var kvp in _torrents)
                {
                    var torrent = kvp.Value;
                    var infoHashHex = kvp.Key;
                    
                    // Очищаем колбэки перед удалением
                    torrent.SetCallbacks(null!);
                    
                    torrent.Stop();
                    torrent.Dispose();
                }
                _torrents.Clear();
                _torrentCallbacks.Clear();
            }
            finally
            {
                _lock.Release();
            }
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            ClearEvents();
            
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                List<Task> disposeTasks = [];
                
                foreach (var kvp in _torrents)
                {
                    var torrent = kvp.Value;
                    var infoHashHex = kvp.Key;
                    
                    // Очищаем колбэки перед удалением
                    torrent.SetCallbacks(null!);
                    
                    torrent.Stop();
                    disposeTasks.Add(torrent.DisposeAsync().AsTask());
                }
                
                // КРИТИЧНО: Агрессивное освобождение с таймаутом для быстрого закрытия
                try
                {
                    await Task.WhenAll(disposeTasks).WaitAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // Если не успело - используем синхронный Dispose для оставшихся
                    Logger.LogWarning("[TorrentClient] DisposeAsync не завершился за 200мс, используем синхронный Dispose");
                    foreach (var kvp in _torrents)
                    {
                        try { kvp.Value.Dispose(); } catch { }
                    }
                }
                
                _torrents.Clear();
                _torrentCallbacks.Clear();
            }
            finally
            {
                _lock.Release();
            }
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// Представляет активный торрент (загружаемый/раздаваемый)
    /// </summary>
    public class ActiveTorrent : IDisposable, IAsyncDisposable
    {
        #region Поля

        private readonly byte[] _peerId;
        private readonly int _port;
        private Storage? _storage;
        private Swarm? _swarm;
        private TrackerManager? _trackerManager;
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        
        private SwarmCallbacksWrapper? _swarmCallbacksWrapper;
        private TrackerManagerCallbacksWrapper? _trackerCallbacksWrapper;

        // Для расчёта скорости (EMA - экспоненциальное скользящее среднее)
        private long _lastDownloaded;
        private long _lastUploaded;
        private DateTime _lastSpeedUpdate = DateTime.UtcNow;
        private double _emaDownloadSpeed;
        private double _emaUploadSpeed;
        private const double EmaAlpha = 0.5;
        
        // Колбэки заменяют события
        
        // Настройки Swarm (применяются при инициализации)
        // Значения по умолчанию синхронизированы с AppSettings
        private int _maxConnections = 200;
        private int _maxHalfOpen = 100;
        private int _maxPiecesToRequest = 100; // Увеличено для максимальной скорости
        private int _maxRequestsPerWire = 128; // Увеличено для максимальной скорости

        #endregion

        #region Свойства

        /// <summary>Метаданные торрента</summary>
        public TorrentMetadata Metadata { get; }

        /// <summary>Путь загрузки</summary>
        public string DownloadPath { get; }

        /// <summary>Статус торрента</summary>
        public TorrentStatus Status { get; private set; } = TorrentStatus.Stopped;

        #endregion

        #region Прогресс

        /// <summary>Битовое поле загруженных кусков</summary>
        public BitArray? Bitfield => _swarm?.Bitfield;

        /// <summary>Загружено байт (завершённые куски)</summary>
        public long Downloaded => _swarm?.Downloaded ?? 0;

        /// <summary>Все полученные байты (для расчёта скорости)</summary>
        public long DownloadedBytes => _swarm?.DownloadedBytes ?? 0;

        /// <summary>Отдано байт</summary>
        public long Uploaded => _swarm?.Uploaded ?? 0;

        /// <summary>Осталось байт</summary>
        public long Left => Metadata.TotalLength - Downloaded;

        /// <summary>Процент прогресса</summary>
        public double ProgressPercent => Metadata.TotalLength > 0 ? (double)Downloaded / Metadata.TotalLength : 0;

        /// <summary>Загрузка завершена</summary>
        public bool IsComplete => Bitfield?.IsComplete ?? false;

        #endregion

        #region Пиры

        /// <summary>Подключённые пиры</summary>
        public int ConnectedPeers => _swarm?.ConnectedPeers ?? 0;

        /// <summary>Активные пиры (unchoked)</summary>
        public int ActivePeers => _swarm?.ActivePeers ?? 0;

        /// <summary>Все известные пиры</summary>
        public int TotalPeers => _swarm?.TotalPeers ?? 0;

        #endregion

        #region Скорость

        /// <summary>Скорость загрузки (байт/сек)</summary>
        public long DownloadSpeed { get; private set; }

        /// <summary>Скорость отдачи (байт/сек)</summary>
        public long UploadSpeed { get; private set; }

        private long? _maxDownloadSpeed;
        private long? _maxUploadSpeed;

        /// <summary>Ограничение скорости загрузки (байт/сек)</summary>
        public long? MaxDownloadSpeed
        {
            get => _maxDownloadSpeed;
            set
            {
                _maxDownloadSpeed = value;
                if (_swarm != null)
                    _swarm.MaxDownloadSpeed = value;
            }
        }

        /// <summary>Ограничение скорости отдачи (байт/сек)</summary>
        public long? MaxUploadSpeed
        {
            get => _maxUploadSpeed;
            set
            {
                _maxUploadSpeed = value;
                if (_swarm != null)
                    _swarm.MaxUploadSpeed = value;
            }
        }

        #endregion

        #region Асинхронные колбэки (замена событий)
        
        // События заменены на IActiveTorrentCallbacks через SetCallbacks
        // Оставлены для обратной совместимости, но не используются

        #endregion

        #region Конструктор

        private IActiveTorrentCallbacks? _callbacks;

        public ActiveTorrent(TorrentMetadata metadata, byte[] peerId, int port, string downloadPath)
        {
            Metadata = metadata;
            _peerId = peerId;
            _port = port;
            DownloadPath = downloadPath;
        }
        
        /// <summary>Устанавливает колбэки для замены событий</summary>
        internal void SetCallbacks(IActiveTorrentCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        #region Инициализация

        /// <summary>Ожидает завершения инициализации</summary>
        public async Task WaitForInitializationAsync()
        {
            // Ждём освобождения семафора (инициализация завершена или ещё не начата)
            await _initLock.WaitAsync().ConfigureAwait(false);
            _initLock.Release();
            
            // Если торрент не инициализирован, запускаем инициализацию
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Инициализирует хранилище и проверяет существующие куски</summary>
        public async Task InitializeAsync()
        {
            await _initLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                Logger.LogInfo($"[ActiveTorrent] Инициализация: {Metadata.Name}");

                _storage = new Storage(Metadata, DownloadPath);
                await _storage.InitializeAsync().ConfigureAwait(false);

                // Проверяем существующие куски
                var existingBitfield = await _storage.VerifyExistingPiecesAsync().ConfigureAwait(false);

                // Создаём рой
                _swarm = new Swarm(Metadata, _peerId, _port, _storage, existingBitfield);

                // Применяем настройки Swarm
                _swarm.MaxConnections = _maxConnections;
                _swarm.MaxHalfOpen = _maxHalfOpen;
                _swarm.MaxPiecesToRequest = _maxPiecesToRequest;
                _swarm.MaxRequestsPerWire = _maxRequestsPerWire;

                // Применяем ограничения скорости
                if (_maxDownloadSpeed.HasValue)
                    _swarm.MaxDownloadSpeed = _maxDownloadSpeed;
                if (_maxUploadSpeed.HasValue)
                    _swarm.MaxUploadSpeed = _maxUploadSpeed;

                // Инициализируем счётчики для расчёта скорости
                _lastDownloaded = _swarm.DownloadedBytes;
                _lastUploaded = _swarm.Uploaded;
                _lastSpeedUpdate = DateTime.UtcNow;

                // Создаём колбэки для Swarm
                _swarmCallbacksWrapper = new SwarmCallbacksWrapper(this);
                _swarm.SetCallbacks(_swarmCallbacksWrapper);

                // Создаём менеджер трекеров
                _trackerManager = new TrackerManager(Metadata.InfoHash, _peerId, _port, Metadata.Trackers);
                _trackerCallbacksWrapper = new TrackerManagerCallbacksWrapper(this);
                _trackerManager.SetCallbacks(_trackerCallbacksWrapper);

                _initialized = true;

                if (existingBitfield.IsComplete)
                {
                    Status = TorrentStatus.Seeding;
                    Logger.LogInfo($"[ActiveTorrent] Уже завершён, раздача: {Metadata.Name}");
                }
                else
                {
                    Logger.LogInfo($"[ActiveTorrent] Инициализировано: {existingBitfield.SetCount}/{existingBitfield.Length} кусков");
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        internal Task OnStatsUpdatedAsync(long downloaded, long downloadedBytes, long uploaded, int peers)
        {
            // Вычисляем скорость (EMA)
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSpeedUpdate).TotalSeconds;

            if (elapsed >= 1.0)
            {
                var downloadedDelta = DownloadedBytes - _lastDownloaded;
                var uploadedDelta = Uploaded - _lastUploaded;

                if (elapsed > 0)
                {
                    var instantUploadSpeed = uploadedDelta / elapsed;
                    _emaUploadSpeed = EmaAlpha * instantUploadSpeed + (1 - EmaAlpha) * _emaUploadSpeed;
                    
                    // Скорость загрузки обновляем только если не в режиме раздачи
                    if (Status != TorrentStatus.Seeding)
                    {
                        var instantDownloadSpeed = downloadedDelta / elapsed;
                        _emaDownloadSpeed = EmaAlpha * instantDownloadSpeed + (1 - EmaAlpha) * _emaDownloadSpeed;
                    }
                }

                // Плавное затухание при отсутствии активности
                if (Status != TorrentStatus.Seeding && downloadedDelta == 0)
                    _emaDownloadSpeed *= 0.7;
                if (uploadedDelta == 0)
                    _emaUploadSpeed *= 0.7;

                // Округляем малые значения до 0
                // Скорость загрузки всегда 0 в режиме раздачи
                DownloadSpeed = (Status == TorrentStatus.Seeding) ? 0 : (_emaDownloadSpeed > 50 ? (long)_emaDownloadSpeed : 0);
                UploadSpeed = _emaUploadSpeed > 50 ? (long)_emaUploadSpeed : 0;

                _lastDownloaded = DownloadedBytes;
                _lastUploaded = Uploaded;
                _lastSpeedUpdate = now;
            }

            // КРИТИЧНО: Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnProgressChangedAsync().ConfigureAwait(false));
            }
            
            return Task.CompletedTask;
        }

        #endregion

        #region Управление

        /// <summary>Запускает загрузку/раздачу</summary>
        public void Start()
        {
            if (!_initialized)
            {
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync("Торрент не инициализирован").ConfigureAwait(false));
                }
                return;
            }

            if (Status == TorrentStatus.Downloading || Status == TorrentStatus.Seeding)
                return;

            Status = IsComplete ? TorrentStatus.Seeding : TorrentStatus.Downloading;

            // Сбрасываем счётчики скорости для корректного расчёта после паузы
            _lastDownloaded = DownloadedBytes;
            _lastUploaded = Uploaded;
            _lastSpeedUpdate = DateTime.UtcNow;
            _emaDownloadSpeed = 0;
            _emaUploadSpeed = 0;
            DownloadSpeed = 0;
            UploadSpeed = 0;

            // Очищаем список известных пиров для повторного подключения
            _trackerManager?.ClearSeenPeers();

            _swarm?.Start();
            _trackerManager?.Start(Left);

            Logger.LogInfo($"[ActiveTorrent] Запущен: {Metadata.Name} ({Status})");
        }

        /// <summary>Останавливает загрузку/раздачу</summary>
        public void Stop()
        {
            if (Status == TorrentStatus.Stopped)
                return;

            Status = TorrentStatus.Stopped;

            _swarm?.Stop();
            _trackerManager?.Stop();
            
            // Закрываем файлы для освобождения памяти
            _storage?.CloseAllFilesAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            // Сбрасываем скорость
            DownloadSpeed = 0;
            UploadSpeed = 0;
            _emaDownloadSpeed = 0;
            _emaUploadSpeed = 0;

            Logger.LogInfo($"[ActiveTorrent] Остановлен: {Metadata.Name}");
        }

        /// <summary>Вручную добавляет пира</summary>
        public async Task AddPeerAsync(IPEndPoint endpoint)
        {
            if (_swarm != null)
            {
                await _swarm.AddPeerAsync(endpoint).ConfigureAwait(false);
            }
        }
        
        /// <summary>Применяет настройки Swarm</summary>
        public void ApplySwarmSettings(int maxConnections, int maxHalfOpen, int maxPieces, int maxRequestsPerWire)
        {
            // Сохраняем для использования при инициализации
            _maxConnections = maxConnections;
            _maxHalfOpen = maxHalfOpen;
            _maxPiecesToRequest = maxPieces;
            _maxRequestsPerWire = maxRequestsPerWire;
            
            // Применяем к уже созданному Swarm
            if (_swarm != null)
            {
                _swarm.MaxConnections = maxConnections;
                _swarm.MaxHalfOpen = maxHalfOpen;
                _swarm.MaxPiecesToRequest = maxPieces;
                _swarm.MaxRequestsPerWire = maxRequestsPerWire;
                Logger.LogInfo($"[ActiveTorrent] Настройки Swarm применены: {Metadata.Name}");
            }
        }

        #endregion

        #region IDisposable / IAsyncDisposable
        
        private void ClearEventsAndUnsubscribe()
        {
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
            
            // КРИТИЧНО: Очищаем колбэки перед освобождением ресурсов
            if (_swarm != null)
            {
                _swarm.SetCallbacks(null!);
            }
            
            if (_trackerManager != null)
            {
                _trackerManager.SetCallbacks(null!);
            }
            
            _swarmCallbacksWrapper = null;
            _trackerCallbacksWrapper = null;
        }

        public void Dispose()
        {
            Stop();
            ClearEventsAndUnsubscribe();
            
            _swarm?.Dispose();
            _trackerManager?.Dispose();
            _storage?.Dispose();
            _initLock.Dispose();
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            Stop();
            ClearEventsAndUnsubscribe();
            
            // КРИТИЧНО: Агрессивное освобождение с таймаутом для быстрого закрытия
            if (_swarm != null)
            {
                try
                {
                    await _swarm.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    Logger.LogWarning("[ActiveTorrent] Swarm.DisposeAsync не завершился за 200мс, используем синхронный Dispose");
                    try { _swarm.Dispose(); } catch { }
                }
            }
            
            _trackerManager?.Dispose();
            
            if (_storage != null)
            {
                try
                {
                    await _storage.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    Logger.LogWarning("[ActiveTorrent] Storage.DisposeAsync не завершился за 200мс, используем синхронный Dispose");
                    try { _storage.Dispose(); } catch { }
                }
            }
            
            _initLock.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>Статус торрента</summary>
    public enum TorrentStatus
    {
        /// <summary>Остановлен</summary>
        Stopped,
        /// <summary>Загружается</summary>
        Downloading,
        /// <summary>Раздаётся</summary>
        Seeding,
        /// <summary>Ошибка</summary>
        Error
    }
}
