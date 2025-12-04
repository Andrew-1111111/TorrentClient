using TorrentClient.Core.Interfaces;

namespace TorrentClient.Core
{
    /// <summary>
    /// Загрузчик торрентов - основная логика скачивания
    /// </summary>
    public class TorrentDownloader
    {
        #region Константы

        private const int MaxConcurrentPieces = 1;      // Загружаем по одному куску для надежности
        private const int MinConnections = 20;          // Минимум соединений
        private const int BlockSize = 16384;            // Размер блока 16KB
        private const int ConnectionTimeout = 10;       // Таймаут подключения (сек) - уменьшен для более быстрого переключения
        private const int PexSendInterval = 60;        // Интервал отправки PEX пиров (сек)
        private DateTime _lastPexSend = DateTime.MinValue;
        private const int BlockTimeout = 60;            // Таймаут загрузки блока (сек)

        #endregion

        #region Поля

        private readonly Torrent _torrent;
        private readonly string _downloadPath;
        private readonly TrackerClient _trackerClient;
        private readonly SpeedLimiter _downloadLimiter;
        private readonly SpeedLimiter _uploadLimiter;
        
        // Компоненты загрузки
        private readonly PiecePicker _piecePicker;
        private readonly ChokeManager _chokeManager;
        private readonly RequestManager _requestManager;
        
        // Соединения с пирами
        private readonly List<PeerConnection> _connections = [];
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly List<IPEndPoint> _peers = [];

        // Состояние кусков
        private readonly Dictionary<int, PieceState> _pieceStates = [];
        private readonly Dictionary<int, byte[]> _pieceBuffers = [];

        // Сетевые компоненты
        private CancellationTokenSource? _cts;
        private Task? _downloadTask;
        private Task? _listenerTask;
        private TcpListener? _listener;
        private TorrentDiscovery? _discovery;
        
        // Идентификация клиента
        private readonly string _peerId;
        private int _port;
        private readonly HashSet<IPAddress> _localIPs = [];
        private IPAddress? _externalIP;
        
        // Статистика скорости
        private long _lastDownloadedBytes;
        private DateTime _lastSpeedUpdate = DateTime.UtcNow;
        
        // Настраиваемые лимиты (увеличены для максимальной скорости)
        private int _maxConnections = 200;
        private int _maxConcurrentBlocks = 100;
        private int _maxRequestsPerPeer = 128;

        #endregion

        #region Асинхронные колбэки (замена событий)

        private ITorrentDownloaderCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(ITorrentDownloaderCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        #region Конструктор

        public TorrentDownloader(Torrent torrent, string downloadPath, TrackerClient trackerClient)
        {
            _torrent = torrent;
            _downloadPath = downloadPath;
            _trackerClient = trackerClient;
            _downloadLimiter = new SpeedLimiter(torrent.MaxDownloadSpeed);
            _uploadLimiter = new SpeedLimiter(torrent.MaxUploadSpeed);

            _peerId = GeneratePeerId();
            _port = FindFreePort(49152, 65535);
            
            InitializeLocalIPs();
            DetectExternalIPAsync();
            InitializePieceStates();
            
            // Инициализация компонентов
            _piecePicker = new PiecePicker(
                _torrent.BitField ?? new BitField(_torrent.Info.PieceCount), 
                _torrent.Info.PieceCount);
            _chokeManager = new ChokeManager();
            _requestManager = new RequestManager();
            
            // Поиск пиров
            _discovery = new TorrentDiscovery(
                _torrent.Info.InfoHash,
                _peerId,
                _port,
                _trackerClient,
                _torrent.Info.AnnounceUrls ?? []);
            
            _discovery.SetTorrentStats(
                () => _torrent.DownloadedBytes,
                () => _torrent.UploadedBytes,
                () => _torrent.Info.TotalSize - _torrent.DownloadedBytes,
                () => _torrent.State == TorrentState.Downloading ? "started" : 
                      _torrent.State == TorrentState.Seeding ? "completed" : null);
            
            var discoveryCallbacks = new TorrentDiscoveryCallbacksWrapperForDownloader(this);
            _discovery.SetCallbacks(discoveryCallbacks);
            _discovery.Start();
            _chokeManager.Start();
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Обновляет настройки загрузчика
        /// </summary>
        /// <param name="maxConnections">Максимальное количество соединений</param>
        /// <param name="maxConcurrentBlocks">Максимальное количество одновременных блоков</param>
        /// <param name="maxRequestsPerPeer">Максимальное количество запросов на пир</param>
        public void UpdateSettings(int maxConnections, int maxConcurrentBlocks, int maxRequestsPerPeer)
        {
            _maxConnections = Math.Clamp(maxConnections, 1, 10000);
            _maxConcurrentBlocks = Math.Clamp(maxConcurrentBlocks, 1, 5000);
            _maxRequestsPerPeer = Math.Clamp(maxRequestsPerPeer, 1, 2500);
            _requestManager.MaxRequestsPerPeer = _maxRequestsPerPeer;
            
            Logger.LogInfo($"Настройки обновлены: соединений={_maxConnections}, блоков={_maxConcurrentBlocks}, запросов/пир={_maxRequestsPerPeer}");
        }

        /// <summary>
        /// Запускает загрузку торрента
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        /// <exception cref="Exception">Выбрасывается при ошибке запуска загрузки</exception>
        public Task StartAsync()
        {
            if (_torrent.State == TorrentState.Downloading)
                return Task.CompletedTask;

            Logger.LogInfo($"Запуск загрузки: {_torrent.Info.Name}");
            _torrent.State = TorrentState.Downloading;
            _cts = new CancellationTokenSource();

            try
            {
                var torrentDir = Path.Combine(_downloadPath, _torrent.Info.Name);
                Directory.CreateDirectory(torrentDir);
                
                StartListener(_cts.Token);
                _downloadTask = Task.Run(() => DownloadLoopAsync(_cts.Token));
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка запуска загрузки: {_torrent.Info.Name}", ex);
                _torrent.State = TorrentState.Error;
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// Приостанавливает загрузку торрента
        /// </summary>
        public void Pause()
        {
            if (_torrent.State != TorrentState.Downloading)
                return;

            _torrent.State = TorrentState.Paused;
            _cts?.Cancel();
        }

        /// <summary>
        /// Возобновляет загрузку торрента после паузы
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task ResumeAsync()
        {
            if (_torrent.State != TorrentState.Paused)
                return;

            await StartAsync();
        }

        /// <summary>
        /// Останавливает загрузку торрента
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task StopAsync()
        {
            _torrent.State = TorrentState.Stopped;
            _cts?.Cancel();
            
            // КРИТИЧНО: Останавливаем listener и гарантируем его закрытие
            try
            {
                _listener?.Stop();
            }
            catch { }
            _listener = null;

            // КРИТИЧНО: Ожидаем завершения фоновых задач с таймаутом
            if (_listenerTask != null)
            {
                try 
                { 
                    await _listenerTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
                _listenerTask = null;
            }

            if (_downloadTask != null)
            {
                try 
                { 
                    await _downloadTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
                _downloadTask = null;
            }

            // КРИТИЧНО: Освобождаем все соединения
            await _lock.WaitAsync();
            try
            {
                foreach (var conn in _connections.ToList())
                {
                    try
                    {
                        conn.Dispose();
                    }
                    catch { }
                }
                _connections.Clear();
            }
            finally
            {
                _lock.Release();
            }

            // КРИТИЧНО: Освобождаем CancellationTokenSource
            try
            {
                _cts?.Dispose();
            }
            catch { }
            _cts = null;

            // КРИТИЧНО: Освобождаем TorrentDiscovery
            try
            {
                _discovery?.Dispose();
            }
            catch { }
            _discovery = null;
        }

        #endregion

        #region Основной цикл загрузки

        private async Task DownloadLoopAsync(CancellationToken ct)
        {
            try
            {
                await ConnectToPeersAsync(ct);
                var lastConnectTime = DateTime.Now;
                var lastStatsTime = DateTime.Now;

                while (!ct.IsCancellationRequested && 
                       _torrent.State == TorrentState.Downloading && 
                       !_torrent.IsComplete)
                {
                    // Периодическое подключение к новым пирам
                    if (DateTime.Now - lastConnectTime > TimeSpan.FromSeconds(5))
                    {
                        await ConnectToPeersAsync(ct);
                        lastConnectTime = DateTime.Now;
                    }

                    // Обновление статистики
                    if (DateTime.Now - lastStatsTime > TimeSpan.FromSeconds(1))
                    {
                        UpdateStatistics();
                        lastStatsTime = DateTime.Now;
                    }

                    await DownloadPiecesAsync(ct);
                    await Task.Delay(100, ct);
                }

                if (_torrent.IsComplete)
                {
                    _torrent.State = TorrentState.Seeding;
                    _torrent.CompletedDate = DateTime.Now;
                    if (_callbacks != null)
                    {
                        _ = Task.Run(async () => await _callbacks.OnDownloadCompletedAsync().ConfigureAwait(false));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo($"Загрузка отменена: {_torrent.Info.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка в цикле загрузки: {_torrent.Info.Name}", ex);
                _torrent.State = TorrentState.Error;
                if (_callbacks != null)
                {
                    _ = Task.Run(async () => await _callbacks.OnErrorOccurredAsync(ex.Message).ConfigureAwait(false));
                }
            }
        }

        #endregion

        #region Работа с пирами

        private async Task ConnectToPeersAsync(CancellationToken ct)
        {
            if (_torrent.BitField == null)
                return;

            // Получаем пиров из discovery
            await _lock.WaitAsync(ct);
            List<IPEndPoint> discoveredPeers;
            try
            {
                discoveredPeers = _discovery?.GetDiscoveredPeers() ?? [];
                foreach (var peer in discoveredPeers.Where(p => !IsSelfPeer(p)))
                {
                    var key = $"{peer.Address}:{peer.Port}";
                    if (!_peers.Any(p => $"{p.Address}:{p.Port}" == key))
                        _peers.Add(peer);
                }
            }
            finally
            {
                _lock.Release();
            }

            // Получаем список пиров для подключения
            await _lock.WaitAsync(ct);
            List<IPEndPoint> peersToConnect;
            try
            {
                var connectedEndPoints = _connections.Where(c => c.IsConnected)
                    .Select(c => c.EndPoint).ToList();
                
                // Увеличиваем количество пиров для попытки подключения
                // Берем больше пиров, так как многие могут не подключиться
                var maxPeersToTry = Math.Max(100, _maxConnections * 2);
                peersToConnect = _peers
                    .Where(p => !IsSelfPeer(p))
                    .Where(p => !connectedEndPoints.Contains(p))
                    .Take(maxPeersToTry)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }

            if (peersToConnect.Count == 0)
                return;

            Logger.LogInfo($"Подключение к {peersToConnect.Count} пирам (лимит соединений: {_maxConnections})");

            // Параллельное подключение с ограничением по количеству одновременных подключений
            // Подключаемся к большему количеству пиров, но ограничиваем параллельные попытки
            var maxConcurrentConnections = Math.Min(_maxConnections, 50); // Максимум 50 параллельных подключений
            var connectionTasks = new List<Task>();
            var semaphore = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
            
            foreach (var peer in peersToConnect)
            {
                // Пропускаем если уже достигли лимита подключенных пиров
                await _lock.WaitAsync(ct);
                try
                {
                    if (_connections.Count(c => c.IsConnected) >= _maxConnections)
                        break;
                }
                finally
                {
                    _lock.Release();
                }
                
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await ConnectToPeerAsync(peer, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);
                
                connectionTasks.Add(task);
            }
            
            // Ждем завершения всех попыток подключения с таймаутом
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectionTimeout));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            try
            {
                await Task.WhenAll(connectionTasks).WaitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                Logger.LogInfo($"Таймаут подключения к пирам (часть подключений может продолжаться)");
            }
            catch (OperationCanceledException)
            {
                // Нормальная отмена
            }

            await _lock.WaitAsync(ct);
            try
            {
                var connected = _connections.Count(c => c.IsConnected);
                var unchoked = _connections.Count(c => c.IsConnected && !c.PeerChoked);
                Logger.LogInfo($"Подключено: {connected}, разблокировано: {unchoked}");
                
                // Отправляем PEX пиров подключенным пирам для обмена списками пиров
                await SendPexPeersToConnectedPeersAsync();
            }
            finally
            {
                _lock.Release();
            }
        }
        
        /// <summary>
        /// Отправляет список пиров подключенным пирам через PEX
        /// </summary>
        private async Task SendPexPeersToConnectedPeersAsync()
        {
            // Ограничиваем частоту отправки PEX
            if ((DateTime.UtcNow - _lastPexSend).TotalSeconds < PexSendInterval)
                return;
            
            try
            {
                // Получаем список всех известных пиров (кроме подключенных)
                var connectedEndPoints = _connections
                    .Where(c => c.IsConnected)
                    .Select(c => c.EndPoint)
                    .ToHashSet();
                
                var peersToShare = _peers
                    .Where(p => !IsSelfPeer(p))
                    .Where(p => !connectedEndPoints.Contains(p))
                    .Take(50) // Отправляем до 50 пиров через PEX
                    .ToList();
                
                if (peersToShare.Count == 0)
                    return;
                
                // Отправляем список пиров всем подключенным пирам, которые поддерживают PEX
                var pexTasks = _connections
                    .Where(c => c.IsConnected && c.PeerExchange?.IsSupported == true)
                    .Select(async connection =>
                    {
                        try
                        {
                            await connection.PeerExchange!.SendPexPeersAsync(peersToShare);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[PEX] Error sending peers to {connection.EndPoint}: {ex.Message}");
                        }
                    });
                
                await Task.WhenAll(pexTasks);
                _lastPexSend = DateTime.UtcNow;
                
                Logger.LogInfo($"[PEX] Sent {peersToShare.Count} peers to {_connections.Count(c => c.IsConnected && c.PeerExchange?.IsSupported == true)} peers via PEX");
            }
            catch (Exception ex)
            {
                Logger.LogError("[PEX] Error sending PEX peers", ex);
            }
        }

        private async Task ConnectToPeerAsync(IPEndPoint peer, CancellationToken ct)
        {
            var connection = new PeerConnection(peer, _peerId, _torrent.Info.InfoHash, _torrent.BitField!);
            SetupConnectionHandlers(connection);

            try
            {
                if (await connection.ConnectAsync(ct))
                        {
                    await _lock.WaitAsync(ct);
                            try
                            {
                                _connections.Add(connection);
                                _torrent.ConnectedPeers = _connections.Count;
                            }
                            finally
                            {
                        _lock.Release();
                            }
                            
                            _chokeManager.AddPeer(connection);
                            if (connection.PeerBitField != null)
                                _piecePicker.UpdatePeerBitField(connection.PeerBitField);
                        }
                        else
                        {
                            connection.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                Logger.LogError($"Ошибка подключения к {peer}", ex);
                        connection.Dispose();
                    }
        }

        private void SetupConnectionHandlers(PeerConnection connection)
        {
            var callbacks = new PeerConnectionCallbacksWrapperForDownloader(this, connection);
            connection.SetCallbacks(callbacks);
        }
        
        internal void HandlePieceData(PeerConnection connection, PeerConnection.PieceDataEventArgs e)
        {
            try
            {
                _requestManager.HandleBlockReceivedAsync(connection, e.PieceIndex, e.Begin, e.Data).Wait();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка обработки данных куска: {e.PieceIndex}/{e.Begin}", ex);
            }
        }

        internal async Task RemoveConnectionAsync(PeerConnection connection)
        {
            await _lock.WaitAsync();
            try
            {
                _connections.Remove(connection);
                _torrent.ConnectedPeers = _connections.Count;
            }
            finally
            {
                _lock.Release();
            }
            
            await _requestManager.CancelPeerRequestsAsync(connection);
            _chokeManager.RemovePeer(connection);
            connection.Dispose();
        }

        internal void AddPeer(IPEndPoint peer)
        {
            if (_lock.CurrentCount == 0)
            {
                var key = $"{peer.Address}:{peer.Port}";
                if (!_peers.Any(p => $"{p.Address}:{p.Port}" == key))
                    _peers.Add(peer);
            }
            else
            {
                _lock.Wait();
                try
                {
                    var key = $"{peer.Address}:{peer.Port}";
                    if (!_peers.Any(p => $"{p.Address}:{p.Port}" == key))
                        _peers.Add(peer);
                    }
                    finally
                    {
                    _lock.Release();
                }
                }
            }

        #endregion

        #region Загрузка кусков

        private async Task DownloadPiecesAsync(CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            List<PeerConnection> availableConnections;
            try
            {
                availableConnections = _connections
                    .Where(c => c.IsConnected && !c.PeerChoked)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }

            if (availableConnections.Count == 0)
                return;

            // Получаем загружаемые куски
            HashSet<int> downloadingPieces = [];
            await _lock.WaitAsync(ct);
            try
            {
                foreach (var state in _pieceStates.Values.Where(s => s.IsDownloading))
                        downloadingPieces.Add(state.PieceIndex);
            }
            finally
            {
                _lock.Release();
            }

            // Выбираем куски для загрузки (Rarest First)
            var piecesToDownload = _piecePicker.PickPieces(MaxConcurrentPieces, downloadingPieces);
            if (piecesToDownload.Count == 0)
                return;

            Logger.LogInfo($"Выбрано {piecesToDownload.Count} кусков для загрузки");
            await Task.WhenAll(piecesToDownload.Select(p => DownloadPieceAsync(p, ct)));
            }

        private async Task DownloadPieceAsync(int pieceIndex, CancellationToken ct)
        {
            if (pieceIndex < 0 || pieceIndex >= _torrent.Info.PieceCount)
                return;

            // Помечаем как загружаемый
            await _lock.WaitAsync(ct);
            try
            {
                if (_pieceStates[pieceIndex].IsDownloaded || _pieceStates[pieceIndex].IsDownloading)
                    return;
                _pieceStates[pieceIndex].IsDownloading = true;
                _piecePicker.MarkDownloading(pieceIndex);
            }
            finally
            {
                _lock.Release();
            }

            try
            {
                var pieceLength = GetPieceLength(pieceIndex);
                _pieceBuffers[pieceIndex] = new byte[pieceLength];
                var pieceData = _pieceBuffers[pieceIndex];

                var totalBlocks = (pieceLength + BlockSize - 1) / BlockSize;
                HashSet<int> completedBlocks = [];
                var downloadedBytes = 0;
                var blockQueue = new Queue<int>(Enumerable.Range(0, totalBlocks));
                Dictionary<int, Task<(bool, int, int)>> activeTasks = [];

                while ((blockQueue.Count > 0 || activeTasks.Count > 0) && !ct.IsCancellationRequested)
                {
                    // Запускаем новые задачи
                    while (blockQueue.Count > 0 && activeTasks.Count < _maxConcurrentBlocks)
                {
                        var blockIndex = blockQueue.Dequeue();
                        var begin = blockIndex * BlockSize;
                        var length = Math.Min(BlockSize, pieceLength - begin);
                        var bi = blockIndex;
                        
                        activeTasks[blockIndex] = Task.Run(async () =>
                        {
                            var result = await DownloadBlockAsync(pieceIndex, begin, length, pieceData, ct);
                            return (result.success, result.length, bi);
                        }, ct);
                }
                
                    if (activeTasks.Count == 0)
                        break;
                    
                    var completed = await Task.WhenAny(activeTasks.Values);
                    var result = await completed;
                    activeTasks.Remove(result.Item3);
                    
                    if (result.Item1)
                    {
                        completedBlocks.Add(result.Item3);
                        downloadedBytes += result.Item2;
                        
                        if (completedBlocks.Count % 50 == 0)
                            Logger.LogInfo($"Кусок {pieceIndex}: {completedBlocks.Count}/{totalBlocks} блоков");
                    }
                    else
                    {
                        blockQueue.Enqueue(result.Item3);
                    }
                }
                
                // Проверяем хеш
                if (downloadedBytes == pieceLength && VerifyPieceHash(pieceIndex, pieceData))
                {
                    await SavePieceAsync(pieceIndex, pieceData);
                    
                    await _lock.WaitAsync(ct);
                    try
                    {
                        _pieceStates[pieceIndex].IsDownloaded = true;
                        _piecePicker.UnmarkDownloading(pieceIndex);
                        if (_torrent.BitField != null)
                            _torrent.BitField[pieceIndex] = true;
                        _torrent.DownloadedBytes += pieceLength;
                    }
                    finally
                    {
                        _lock.Release();
                    }

                    Logger.LogInfo($"Кусок {pieceIndex} загружен!");
                    if (_callbacks != null)
                    {
                        _ = Task.Run(async () => await _callbacks.OnProgressUpdatedAsync(_torrent.DownloadedBytes).ConfigureAwait(false));
                        _ = Task.Run(async () => await _callbacks.OnStateChangedAsync().ConfigureAwait(false));
                    }
                }
                else
                {
                    Logger.LogWarning($"Кусок {pieceIndex} не прошел проверку: загружено={downloadedBytes}/{pieceLength}");
                    await _lock.WaitAsync(ct);
                    try
                    {
                        _pieceStates[pieceIndex].IsDownloading = false;
                        _piecePicker.UnmarkDownloading(pieceIndex);
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка загрузки куска {pieceIndex}", ex);
            }
            finally
            {
                await _lock.WaitAsync();
                try
                {
                    if (!_pieceStates[pieceIndex].IsDownloaded)
                    {
                        _pieceStates[pieceIndex].IsDownloading = false;
                        _piecePicker.UnmarkDownloading(pieceIndex);
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }
        }
        
        private async Task<(bool success, int length)> DownloadBlockAsync(
            int pieceIndex, int begin, int length, byte[] pieceData, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<byte[]>();
            PeerConnection? peer = null;
            
            try
            {
                peer = SelectBestPeer(pieceIndex, ct);
                
                // Ожидание доступного пира
                for (int i = 0; i < 10 && peer == null && !ct.IsCancellationRequested; i++)
                    {
                    await Task.Delay(1000, ct);
                    peer = SelectBestPeer(pieceIndex, ct);
                    }
                    
                if (peer == null)
                        return (false, 0);
                
                if (!await _requestManager.RequestBlockAsync(peer, pieceIndex, begin, length, tcs))
                    return (false, 0);
                
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(BlockTimeout));
                using var combined = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                
                try
                {
                    var blockData = await tcs.Task.WaitAsync(combined.Token);
                    
                    if (begin + blockData.Length <= pieceData.Length)
                    {
                        Array.Copy(blockData, 0, pieceData, begin, blockData.Length);
                        await _downloadLimiter.WaitIfNeededAsync(blockData.Length, ct);
                        return (true, blockData.Length);
                    }
                    return (false, 0);
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    Logger.LogWarning($"Таймаут блока: кусок={pieceIndex}, смещение={begin}");
                    if (peer != null)
                        await _requestManager.CancelPeerRequestsAsync(peer);
                    return (false, 0);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка загрузки блока: кусок={pieceIndex}, смещение={begin}", ex);
                if (peer != null)
                    await _requestManager.CancelPeerRequestsAsync(peer);
                return (false, 0);
            }
        }
        
        private PeerConnection? SelectBestPeer(int pieceIndex, CancellationToken ct)
        {
            _lock.Wait(ct);
            try
            {
                return _connections
                    .Where(c => c.IsConnected && !c.PeerChoked)
                    .Where(c => c.PeerBitField != null && pieceIndex < c.PeerBitField.Length && c.PeerBitField[pieceIndex])
                    .Where(c => _requestManager.GetActiveRequestCount(c) < _requestManager.MaxRequestsPerPeer)
                    .OrderByDescending(c => c.DownloadSpeed)
                    .ThenBy(c => _requestManager.GetActiveRequestCount(c))
                    .FirstOrDefault();
            }
            finally
            {
                _lock.Release();
        }
        }

        #endregion

        #region Обработчики сообщений

        internal void HandlePexPeers(List<IPEndPoint> peers)
        {
            foreach (var peer in peers)
                AddPeer(peer);
        }

        internal void HandlePeerBitfieldUpdated(PeerConnection connection)
        {
            if (connection.PeerBitField != null)
                _piecePicker.UpdatePeerBitField(connection.PeerBitField);
        }

        internal void HandleHaveMessage(PeerConnection connection, int pieceIndex)
        {
            _piecePicker.UpdateHave(pieceIndex);
        }

        internal async Task HandlePeerRequestAsync(PeerConnection connection, PeerConnection.RequestEventArgs e)
        {
            try
            {
                await _lock.WaitAsync();
                bool hasPiece;
                try
                {
                    hasPiece = _pieceStates.ContainsKey(e.PieceIndex) && _pieceStates[e.PieceIndex].IsDownloaded;
                }
                finally
                {
                    _lock.Release();
                }
                
                if (!hasPiece)
                    return;
                
                var pieceData = await ReadPieceAsync(e.PieceIndex);
                if (pieceData == null || e.Begin + e.Length > pieceData.Length)
                    return;
                
                var blockData = new byte[e.Length];
                Array.Copy(pieceData, e.Begin, blockData, 0, e.Length);
                await connection.SendPieceAsync(e.PieceIndex, e.Begin, blockData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка обработки запроса от {connection.EndPoint}", ex);
            }
        }
        
        #endregion

        #region Входящие соединения

        private void StartListener(CancellationToken ct)
        {
            for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                    _listener = new TcpListener(IPAddress.Any, _port);
                    _listener.Start();
                    
                    var endpoint = (IPEndPoint)_listener.LocalEndpoint;
                    _port = endpoint.Port;
                    
                    Logger.LogInfo($"Прослушивание входящих соединений на порту {_port}");
                    
                    _listenerTask = Task.Run(async () =>
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            try
                            {
                                var client = await _listener.AcceptTcpClientAsync();
                                // Настраиваем параметры сокета
                                client.NoDelay = true;  // Отключаем алгоритм Nagle
                                client.LingerState = new LingerOption(false, 0);  // LingerState = 0
                                _ = HandleIncomingAsync(client, ct);
                            }
                            catch (ObjectDisposedException) { break; }
                            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted) { }
                            catch (Exception ex)
                            {
                                if (!ct.IsCancellationRequested)
                                    Logger.LogError("Ошибка приема соединения", ex);
                            }
                        }
                    }, ct);
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    _port = FindFreePort(49152, 65535);
            }
            catch (Exception ex)
            {
                    Logger.LogError($"Ошибка запуска слушателя на порту {_port}", ex);
                    return;
                }
            }
        }

        private async Task HandleIncomingAsync(TcpClient client, CancellationToken ct)
        {
            PeerConnection? connection = null;
            try
            {
                var endpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
                
                if (IsSelfPeer(endpoint))
                {
                    try
                    {
                        client.Close();
                        client.Dispose();
                    }
                    catch { }
                    return;
                }

                connection = await PeerConnection.FromIncomingConnectionAsync(
                    client, _peerId, _torrent.Info.InfoHash, _torrent.BitField!, ct);

                if (connection == null)
                {
                    // КРИТИЧНО: Гарантируем закрытие TcpClient если соединение не установлено
                    try
                    {
                        client.Close();
                        client.Dispose();
                    }
                    catch { }
                    return;
                }

                SetupConnectionHandlers(connection);

                await _lock.WaitAsync(ct);
                try
                {
                    _connections.Add(connection);
                    _torrent.ConnectedPeers = _connections.Count;
                }
                finally
                {
                    _lock.Release();
                }
                
                _chokeManager.AddPeer(connection);
                if (connection.PeerBitField != null)
                    _piecePicker.UpdatePeerBitField(connection.PeerBitField);

                // Ожидаем Bitfield
                for (int i = 0; i < 15 && connection.IsConnected && connection.PeerBitField == null; i++)
                {
                    await Task.Delay(200, ct);
                    if (connection.PeerBitField != null)
                        _piecePicker.UpdatePeerBitField(connection.PeerBitField);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка входящего соединения", ex);
                // КРИТИЧНО: Гарантируем закрытие всех ресурсов даже при ошибках
                connection?.Dispose();
                try
                {
                    if (client.Connected)
                    {
                        client.Close();
                    }
                    client.Dispose();
                }
                catch { }
            }
        }

        #endregion

        #region Вспомогательные методы

        private void InitializeLocalIPs()
            {
            try
            {
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in hostEntry.AddressList)
                    _localIPs.Add(ip);
                _localIPs.Add(IPAddress.Loopback);
                _localIPs.Add(IPAddress.IPv6Loopback);
            }
            catch (Exception ex)
                {
                Logger.LogWarning($"Ошибка получения локальных IP: {ex.Message}");
                }
            }
            
        private void DetectExternalIPAsync()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await HttpClientService.Instance.GetAsync("https://api.ipify.org", null, timeoutSeconds: 5).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseText = response.GetBodyAsString().Trim();
                        if (IPAddress.TryParse(responseText, out var ip))
                        {
                            _externalIP = ip;
                            Logger.LogInfo($"Внешний IP: {ip}");
                        }
                    }
                }
                catch { }
            });
        }

        private void InitializePieceStates()
        {
            _lock.Wait();
            try
            {
                for (int i = 0; i < _torrent.Info.PieceCount; i++)
                {
                    _pieceStates[i] = new PieceState
                    {
                        PieceIndex = i,
                        IsDownloaded = _torrent.BitField?[i] ?? false,
                        IsDownloading = false
                    };
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
                if (elapsed > 0)
                                {
                    var downloaded = _torrent.DownloadedBytes - _lastDownloadedBytes;
                    _torrent.DownloadSpeed = (long)(downloaded / elapsed);
                    _lastDownloadedBytes = _torrent.DownloadedBytes;
                    _lastSpeedUpdate = now;
                }

                _lock.Wait();
                try
                {
                    _torrent.ConnectedPeers = _connections.Count(c => c.IsConnected);
                    _torrent.TotalPeers = _peers.Count;
                        }
                finally
                {
                    _lock.Release();
                }

                if (_callbacks != null)
                {
                    _ = Task.Run(async () => await _callbacks.OnStateChangedAsync().ConfigureAwait(false));
                }
                }
                catch (Exception ex)
                {
                Logger.LogError("Ошибка обновления статистики", ex);
            }
        }

        private int GetPieceLength(int pieceIndex)
        {
            if (pieceIndex == _torrent.Info.PieceCount - 1)
            {
                var remainder = _torrent.Info.TotalSize % _torrent.Info.PieceLength;
                return remainder > 0 ? (int)remainder : _torrent.Info.PieceLength;
            }
            return _torrent.Info.PieceLength;
        }

        private bool VerifyPieceHash(int pieceIndex, byte[] pieceData)
        {
            if (pieceIndex >= _torrent.Info.PieceCount)
                return false;

            // Примечание: Верификация хэшей перемещена в Engine/TorrentMetadata
            // TorrentDownloader устарел и не используется
            return true;
        }

        private async Task SavePieceAsync(int pieceIndex, byte[] pieceData)
        {
            var pieceLength = GetPieceLength(pieceIndex);
            var pieceOffset = (long)pieceIndex * _torrent.Info.PieceLength;
            var pieceEnd = pieceOffset + pieceLength;
            
            foreach (var file in _torrent.Info.Files)
            {
                var fileStart = file.Offset;
                var fileEnd = file.Offset + file.Length;
                
                if (pieceOffset < fileEnd && pieceEnd > fileStart)
                {
                    var filePath = Path.Combine(_downloadPath, file.Path);
                    var directory = Path.GetDirectoryName(filePath);
                    if (directory != null)
                        Directory.CreateDirectory(directory);

                    var writeStart = Math.Max(pieceOffset, fileStart);
                    var writeEnd = Math.Min(pieceEnd, fileEnd);
                    var writeLength = (int)(writeEnd - writeStart);
                    var bufferOffset = (int)(writeStart - pieceOffset);
                    var fileOffset = writeStart - fileStart;

                    if (writeLength > 0)
                    {
                        using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                        fs.Seek(fileOffset, SeekOrigin.Begin);
                        await TaskTimeoutHelper.TimeoutAsync(
                            fs.WriteAsync(pieceData.AsMemory(bufferOffset, writeLength)),
                            TimeSpan.FromSeconds(60));
                    }
                }
            }
        }

        private async Task<byte[]?> ReadPieceAsync(int pieceIndex)
        {
            try
            {
                var pieceLength = GetPieceLength(pieceIndex);
                var pieceData = new byte[pieceLength];
                var pieceOffset = (long)pieceIndex * _torrent.Info.PieceLength;
                var pieceEnd = pieceOffset + pieceLength;
                var bytesRead = 0;
                
                foreach (var file in _torrent.Info.Files)
                {
                    var fileStart = file.Offset;
                    var fileEnd = file.Offset + file.Length;
                    
                    if (pieceOffset < fileEnd && pieceEnd > fileStart)
                    {
                        var filePath = Path.Combine(_downloadPath, file.Path);
                        if (!File.Exists(filePath))
                            return null;
                        
                        var readStart = Math.Max(pieceOffset, fileStart);
                        var readEnd = Math.Min(pieceEnd, fileEnd);
                        var readLength = (int)(readEnd - readStart);
                        var bufferOffset = (int)(readStart - pieceOffset);
                        var fileOffset = readStart - fileStart;
                        
                        if (readLength > 0)
                        {
                            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            bytesRead += await TaskTimeoutHelper.TimeoutAsync(
                                fs.ReadAsync(pieceData, bufferOffset, readLength),
                                TimeSpan.FromSeconds(60));
                        }
                    }
                }
                
                return bytesRead == pieceLength ? pieceData : null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка чтения куска {pieceIndex}", ex);
                return null;
            }
        }

        internal bool IsSelfPeer(IPEndPoint peer)
        {
            if (_localIPs.Contains(peer.Address))
            {
                if (peer.Port == _port)
                    return true;
                if (peer.Address.Equals(IPAddress.Loopback) || peer.Address.Equals(IPAddress.IPv6Loopback))
                    return true;
            }
            
            if (_externalIP != null && peer.Address.Equals(_externalIP) && peer.Port == _port)
                return true;
            
            return false;
            }

        private int FindFreePort(int minPort, int maxPort)
        {
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                var port = random.Next(minPort, maxPort);
                try
                {
                    using var listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (SocketException) { }
            }
            return random.Next(minPort, maxPort);
        }

        private string GeneratePeerId()
        {
            // Формат: -TC0001-xxxxxxxxxxxx (TC = TorrentClient, 0001 = версия)
            var random = new Random();
            var chars = new char[12];
            for (int i = 0; i < 12; i++)
                chars[i] = (char)random.Next(32, 127);
            return "-TC0001-" + new string(chars);
        }

        #endregion

        #region Вложенные классы

        private class PieceState
        {
            public int PieceIndex { get; set; }
            public bool IsDownloaded { get; set; }
            public bool IsDownloading { get; set; }
        }

        #endregion
    }
}
