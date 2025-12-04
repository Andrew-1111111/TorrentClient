using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Управляет соединениями с пирами (рой)
    /// </summary>
    public class Swarm : IDisposable, IAsyncDisposable
    {
        #region Поля

        private readonly TorrentMetadata _metadata;
        private readonly byte[] _peerId;
        private readonly int _port;
        private readonly Storage _storage;

        private readonly ConcurrentDictionary<string, Wire> _wires = new();
        private readonly ConcurrentDictionary<string, DateTime> _failedPeers = new();
        private readonly HashSet<string> _pendingConnections = [];
        private readonly HashSet<string> _knownPeers = [];
        private readonly SemaphoreSlim _pendingLock = new(1, 1);
        private readonly HashSet<string> _localIPs = [];
        
        // КРИТИЧНО: Храним обработчики событий для отписки и предотвращения утечек памяти
        private readonly ConcurrentDictionary<Wire, WireEventHandlers> _wireHandlers = new();
        
        /// <summary>Хранит обёртки колбэков Wire для корректной очистки</summary>
        private class WireEventHandlers
        {
            public WireCallbacksWrapper? CallbacksWrapper { get; set; }
        }
        
        /// <summary>Максимальное количество известных пиров (ограничение памяти)</summary>
        private const int MaxKnownPeers = 2000; // Уменьшено для более агрессивной очистки
        
        /// <summary>Максимальное количество неудачных пиров (ограничение памяти)</summary>
        private const int MaxFailedPeers = 500; // Уменьшено для более агрессивной очистки

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private Task? _downloadTask;
        private volatile bool _disposed;

        #endregion

        #region Конфигурация

        /// <summary>Максимальное количество соединений</summary>
        public int MaxConnections { get; set; } = 200;
        
        /// <summary>Максимальное количество полуоткрытых соединений</summary>
        public int MaxHalfOpen { get; set; } = 50;
        
        /// <summary>Максимум кусков для одновременного запроса</summary>
        public int MaxPiecesToRequest { get; set; } = 100; // Значение по умолчанию, перезаписывается настройками
        
        /// <summary>Максимальный размер буферов в памяти (байт) - ограничивает память</summary>
        public long MaxBufferMemory { get; set; } = 512 * 1024 * 1024; // 512 МБ для максимальной скорости с контролем утечек
        
        /// <summary>Максимум запросов на соединение (pipelining) - критично для скорости!</summary>
        public int MaxRequestsPerWire { get; set; } = 128; // Значение по умолчанию, перезаписывается настройками
        
        /// <summary>Таймаут подключения</summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>Задержка перед повторной попыткой подключения к пиру</summary>
        public TimeSpan PeerRetryDelay { get; set; } = TimeSpan.FromMinutes(1);

        #endregion

        #region Ограничение скорости

        private long? _maxDownloadSpeed;
        private long? _maxUploadSpeed;
        private long _downloadTokens;
        private long _uploadTokens;
        private DateTime _lastTokenRefill = DateTime.UtcNow;
        private readonly SemaphoreSlim _speedLimitLock = new(1, 1);

        /// <summary>Максимальная скорость загрузки (байт/сек, null = без ограничений)</summary>
        public long? MaxDownloadSpeed
        {
            get => _maxDownloadSpeed;
            set
            {
                _maxDownloadSpeed = value;
                // Сбрасываем токены при изменении лимита для мгновенного применения
                _speedLimitLock.Wait();
                try
                {
                    _downloadTokens = value ?? 0;
                    _lastTokenRefill = DateTime.UtcNow;
                }
                finally
                {
                    _speedLimitLock.Release();
                }
            }
        }

        /// <summary>Максимальная скорость отдачи (байт/сек, null = без ограничений)</summary>
        public long? MaxUploadSpeed
        {
            get => _maxUploadSpeed;
            set
            {
                _maxUploadSpeed = value;
                _speedLimitLock.Wait();
                try
                {
                    _uploadTokens = value ?? 0;
                    _lastTokenRefill = DateTime.UtcNow;
                }
                finally
                {
                    _speedLimitLock.Release();
                }
            }
        }

        #endregion

        #region Состояние

        /// <summary>Битовое поле загруженных кусков</summary>
        public BitArray Bitfield { get; private set; }
        
        /// <summary>Количество реально подключённых пиров</summary>
        public int ConnectedPeers => _wires.Values.Count(w => w.IsConnected);
        
        /// <summary>Количество активных пиров (unchoked)</summary>
        public int ActivePeers => _wires.Values.Count(w => w.IsConnected && !w.PeerChoking);
        
        /// <summary>Общее количество известных пиров</summary>
        public int TotalPeers
        {
            get
            {
                if (_disposed) return 0;
                _pendingLock.Wait();
                try { return _knownPeers.Count; }
                finally { _pendingLock.Release(); }
            }
        }
        
        /// <summary>Загружено байт (завершённые куски)</summary>
        public long Downloaded { get; private set; }
        
        /// <summary>Все полученные байты (для расчёта скорости)</summary>
        public long DownloadedBytes { get; private set; }
        
        /// <summary>Отдано байт</summary>
        public long Uploaded { get; private set; }
        
        /// <summary>Загрузка завершена</summary>
        public bool IsComplete => Bitfield.IsComplete;

        #endregion

        #region Управление кусками

        private readonly ConcurrentDictionary<int, PieceState> _pieceStates = new();
        private readonly ConcurrentDictionary<int, PooledBuffer> _pieceBuffers = new();
        private readonly int[] _pieceAvailability;
        private readonly ConcurrentDictionary<Wire, int> _pendingRequestsPerWire = new();
        private const int BlockSize = 16384; // 16 КБ
        
        /// <summary>Флаг для предотвращения параллельных вызовов RequestPieces (защита от утечки памяти)</summary>
        private volatile bool _isRequestingPieces = false;
        
        /// <summary>Словарь для отслеживания обрабатываемых кусков (защита от параллельной обработки одного куска)</summary>
        private readonly ConcurrentDictionary<int, bool> _processingPieces = new();

        /// <summary>Буфер из ArrayPool с отслеживанием размера</summary>
        private sealed class PooledBuffer
        {
            public byte[] Array { get; }
            public int Length { get; }
            
            public PooledBuffer(int length)
            {
                Array = ArrayPool<byte>.Shared.Rent(length);
                Length = length;
            }
            
            public void Return()
            {
                ArrayPool<byte>.Shared.Return(Array);
            }
        }

        private class PieceState
        {
            public bool Downloading { get; set; }
            public HashSet<int> ReceivedBlocks { get; } = [];
            public HashSet<int>? RequestedBlocks { get; set; }
            public Dictionary<int, DateTime>? RequestTimes { get; set; }
            public int TotalBlocks { get; set; }
            public DateTime StartTime { get; set; }
            
            /// <summary>Максимальный размер словаря RequestTimes для предотвращения утечки памяти</summary>
            private const int MaxRequestTimesSize = 2000; // Уменьшено для более агрессивной очистки
            /// <summary>Максимальный размер ReceivedBlocks для предотвращения утечки памяти</summary>
            private const int MaxReceivedBlocksSize = 5000; // Уменьшено для более агрессивной очистки
            /// <summary>Максимальный размер RequestedBlocks для предотвращения утечки памяти</summary>
            private const int MaxRequestedBlocksSize = 500; // Уменьшено для более агрессивной очистки
            
            /// <summary>Очищает запросы старше указанного времени</summary>
            public void ClearStaleRequests(TimeSpan timeout)
            {
                if (RequestedBlocks == null || RequestTimes == null) return;
                
                var now = DateTime.UtcNow;
                // ОПТИМИЗАЦИЯ: Удаляем напрямую без промежуточного списка
                var keysToRemove = new List<int>();
                foreach (var kv in RequestTimes)
                {
                    if (now - kv.Value > timeout)
                    {
                        keysToRemove.Add(kv.Key);
                    }
                }
                    
                foreach (var block in keysToRemove)
                {
                    RequestedBlocks.Remove(block);
                    RequestTimes.Remove(block);
                }
                
                // КРИТИЧНО: Ограничение размера словаря для предотвращения утечки памяти
                // ОПТИМИЗАЦИЯ: Используем более эффективный алгоритм частичной сортировки
                if (RequestTimes.Count > MaxRequestTimesSize)
                {
                    int toRemove = RequestTimes.Count - MaxRequestTimesSize;
                    // ОПТИМИЗАЦИЯ: Используем OrderBy только для нужного количества элементов
                    var oldestEntries = RequestTimes
                        .OrderBy(kv => kv.Value)
                        .Take(toRemove)
                        .Select(kv => kv.Key);
                    
                    foreach (var block in oldestEntries)
                    {
                        RequestedBlocks?.Remove(block);
                        RequestTimes.Remove(block);
                    }
                }
                
                // КРИТИЧНО: Ограничение размера RequestedBlocks
                // ОПТИМИЗАЦИЯ: Удаляем напрямую без промежуточного списка
                if (RequestedBlocks != null && RequestedBlocks.Count > MaxRequestedBlocksSize)
                {
                    int toRemove = RequestedBlocks.Count - MaxRequestedBlocksSize;
                    var toRemoveList = RequestedBlocks.Take(toRemove).ToList();
                    foreach (var block in toRemoveList)
                    {
                        RequestedBlocks.Remove(block);
                        RequestTimes?.Remove(block);
                    }
                }
            }
            
            /// <summary>Очищает старые ReceivedBlocks для предотвращения утечки памяти</summary>
            public void TrimReceivedBlocks()
            {
                // Если ReceivedBlocks слишком большой, оставляем только последние
                if (ReceivedBlocks.Count > MaxReceivedBlocksSize)
                {
                    var toKeep = ReceivedBlocks.OrderByDescending(x => x).Take(MaxReceivedBlocksSize).ToHashSet();
                    ReceivedBlocks.Clear();
                    foreach (var block in toKeep)
                    {
                        ReceivedBlocks.Add(block);
                    }
                }
            }
            
            /// <summary>Добавляет блок в ReceivedBlocks с проверкой лимита</summary>
            public bool TryAddReceivedBlock(int blockIndex)
            {
                // КРИТИЧНО: Проверяем лимит перед добавлением для предотвращения утечки
                if (ReceivedBlocks.Count >= MaxReceivedBlocksSize)
                {
                    // Если достигнут лимит, очищаем старые блоки перед добавлением нового
                    TrimReceivedBlocks();
                }
                
                // Добавляем только если не превышен лимит (после очистки)
                if (ReceivedBlocks.Count < MaxReceivedBlocksSize)
                {
                    return ReceivedBlocks.Add(blockIndex);
                }
                
                // Если лимит все еще превышен, не добавляем
                return false;
            }
        }

        #endregion

        // События заменены на асинхронные колбэки через ISwarmCallbacks
        private ISwarmCallbacks? _callbacks;
        
        /// <summary>Устанавливает колбэки для замены событий</summary>
        internal void SetCallbacks(ISwarmCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #region Конструктор

        public Swarm(TorrentMetadata metadata, byte[] peerId, int port, Storage storage, BitArray existingBitfield)
        {
            _metadata = metadata;
            _peerId = peerId;
            _port = port;
            _storage = storage;
            Bitfield = existingBitfield;
            _pieceAvailability = new int[metadata.PieceCount];

            // Собираем локальные IP для фильтрации self-пиров
            InitializeLocalIPs();

            // Вычисляем загруженные байты из существующего битового поля
            for (int i = 0; i < Bitfield.Length; i++)
            {
                if (Bitfield[i])
                {
                    var pieceLen = _metadata.GetPieceLength(i);
                    Downloaded += pieceLen;
                    DownloadedBytes += pieceLen;
                }
            }
        }

        private void InitializeLocalIPs()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork ||
                        ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        _localIPs.Add(ip.ToString());
                    }
                }
                _localIPs.Add("127.0.0.1");
                _localIPs.Add("::1");
                Logger.LogInfo($"[Swarm] Локальные IP: {string.Join(", ", _localIPs)}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Swarm] Не удалось получить локальные IP: {ex.Message}");
            }
        }

        #endregion

        #region Управление жизненным циклом

        /// <summary>Запускает рой</summary>
        public void Start()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();

            // Запускаем прослушивание входящих соединений
            StartListener();

            // Запускаем цикл загрузки или раздачи
            if (Bitfield.IsComplete)
            {
                _downloadTask = SeedingLoopAsync(_cts.Token);
            }
            else
            {
                _downloadTask = DownloadLoopAsync(_cts.Token);
            }

            Logger.LogInfo($"[Swarm] Запущен (существующих: {Bitfield.SetCount}/{Bitfield.Length} кусков)");
        }

        /// <summary>Останавливает рой</summary>
        public void Stop()
        {
            _cts?.Cancel();

            _listener?.Stop();
            _listener = null;

            // Ожидаем завершения фоновых задач
            if (_listenerTask != null)
            {
                try { _listenerTask.Wait(TimeSpan.FromSeconds(1)); }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }
                _listenerTask = null;
            }
            
            if (_downloadTask != null)
            {
                try { _downloadTask.Wait(TimeSpan.FromSeconds(1)); }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }
                _downloadTask = null;
            }

            _cts?.Dispose();
            _cts = null;

            // КРИТИЧНО: Отписываемся от всех событий перед освобождением
            foreach (var wire in _wires.Values)
            {
                UnsubscribeWireEvents(wire);
                wire.Dispose();
            }
            _wires.Clear();
            _wireHandlers.Clear();
            _pendingRequestsPerWire.Clear();

            ClearCollections();

            Logger.LogInfo("[Swarm] Остановлен");
        }
        
        /// <summary>Асинхронно останавливает рой</summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();

            _listener?.Stop();
            _listener = null;

            // Ожидаем завершения фоновых задач
            if (_listenerTask != null)
            {
                try { await _listenerTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
                _listenerTask = null;
            }
            
            if (_downloadTask != null)
            {
                try { await _downloadTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
                _downloadTask = null;
            }

            _cts?.Dispose();
            _cts = null;

            // КРИТИЧНО: Отписываемся от всех событий перед освобождением
            foreach (var wire in _wires.Values)
            {
                UnsubscribeWireEvents(wire);
            }
            
            // КРИТИЧНО: Агрессивное освобождение Wire с таймаутом для быстрого закрытия
            var disposeTasks = _wires.Values.Select(w => w.DisposeAsync().AsTask());
            try
            {
                await Task.WhenAll(disposeTasks).WaitAsync(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Если не успело - используем синхронный Dispose для оставшихся
                Logger.LogWarning("[Swarm] DisposeAsync не завершился за 300мс, используем синхронный Dispose");
                foreach (var wire in _wires.Values)
                {
                    try { wire.Dispose(); } catch { }
                }
            }
            
            _wires.Clear();
            _wireHandlers.Clear();
            _pendingRequestsPerWire.Clear();

            ClearCollections();

            Logger.LogInfo("[Swarm] Остановлен асинхронно");
        }
        
        private void ClearCollections()
        {
            // Очищаем ожидающие соединения и список неудачных пиров
            _pendingLock.Wait();
            try
            {
                _pendingConnections.Clear();
            }
            finally
            {
                _pendingLock.Release();
            }

            // Очищаем список неудачных пиров для повторных попыток
            _failedPeers.Clear();
            
            // Очищаем список известных пиров для свежего подсчёта при следующем запуске
            _pendingLock.Wait();
            try
            {
                _knownPeers.Clear();
            }
            finally
            {
                _pendingLock.Release();
            }
            
            // КРИТИЧНО: Возвращаем ВСЕ буферы в ArrayPool
            var bufferKeys = _pieceBuffers.Keys.ToList();
            foreach (var key in bufferKeys)
            {
                if (_pieceBuffers.TryRemove(key, out var buffer))
                {
                    try { buffer.Return(); }
                    catch { /* игнорируем ошибки возврата */ }
                }
            }
            _pieceBuffers.Clear();
            _pieceStates.Clear();
            
            // Очищаем счётчики запросов
            _pendingRequestsPerWire.Clear();
            
            // Очищаем флаги обработки кусков
            _processingPieces.Clear();
            
            // Сбрасываем доступность кусков
            Array.Clear(_pieceAvailability);
            
            Logger.LogInfo($"[Swarm] Коллекции очищены, память освобождена");
        }
        
        private void ClearEvents()
        {
            _callbacks = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            Stop();
            ClearEvents();
            _cts?.Dispose();
            _pendingLock.Dispose();
            _speedLimitLock.Dispose();
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            
            await StopAsync().ConfigureAwait(false);
            ClearEvents();
            _cts?.Dispose();
            _pendingLock.Dispose();
            _speedLimitLock.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Прослушивание входящих соединений

        private void StartListener()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                Logger.LogInfo($"[Swarm] Прослушивание порта {_port}");

                _listenerTask = Task.Run(async () =>
                {
                    while (_cts != null && !_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                            // Настраиваем параметры сокета
                            client.NoDelay = true;  // Отключаем алгоритм Nagle
                            client.LingerState = new LingerOption(false, 0);  // LingerState = 0
                            _ = HandleIncomingConnectionAsync(client);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[Swarm] Ошибка прослушивателя: {ex.Message}");
                        }
                    }
                });
            }
            catch (SocketException ex)
            {
                Logger.LogWarning($"[Swarm] Не удалось запустить прослушиватель на порту {_port}: {ex.Message}");
            }
        }

        private async Task HandleIncomingConnectionAsync(TcpClient client)
        {
            var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
            var key = endpoint?.ToString() ?? "unknown";
            Wire? wire = null;

            try
            {
                if (_wires.Count >= MaxConnections)
                {
                    Logger.LogInfo($"[Swarm] Отклонено входящее соединение от {key}: достигнут лимит соединений");
                    try
                    {
                        client.Close();
                        client.Dispose();
                    }
                    catch { }
                    return;
                }

                Logger.LogInfo($"[Swarm] Входящее соединение от {key}");

                wire = new Wire(_peerId, _metadata.InfoHash, _metadata.PieceCount);
                SetupWireEvents(wire);

                if (await wire.AcceptAsync(client, _cts?.Token ?? default))
                {
                    if (_wires.TryAdd(key, wire))
                    {
                        await SendBitfieldAndInterestedAsync(wire);
                        Logger.LogInfo($"[Swarm] Принято соединение от {key}");
                        wire = null; // Успешно добавлен, не нужно освобождать
                    }
                    else
                    {
                        UnsubscribeWireEvents(wire);
                        wire.Dispose();
                        wire = null;
                    }
                }
                else
                {
                    UnsubscribeWireEvents(wire);
                    wire.Dispose();
                    wire = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Swarm] Ошибка обработки входящего соединения от {key}: {ex.Message}");
                // КРИТИЧНО: Гарантируем закрытие всех ресурсов даже при ошибках
                if (wire != null)
                {
                    try
                    {
                        UnsubscribeWireEvents(wire);
                        wire.Dispose();
                    }
                    catch { }
                }
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

        #region Управление пирами

        /// <summary>Добавляет пира для подключения</summary>
        public async Task AddPeerAsync(IPEndPoint endpoint)
        {
            if (_disposed) return;
            
            var key = endpoint.ToString();

            // Отслеживаем всех известных пиров
            if (_disposed) return;
            await _pendingLock.WaitAsync();
            try
            {
                if (_disposed) return;
                // КРИТИЧНО: Ограничиваем размер коллекции для предотвращения утечки памяти
                if (_knownPeers.Count >= MaxKnownPeers && !_knownPeers.Contains(key))
                {
                    // Удаляем самые старые записи (FIFO - удаляем первые добавленные)
                    var toRemove = _knownPeers.Take(_knownPeers.Count - MaxKnownPeers + 100).ToList();
                    foreach (var oldKey in toRemove)
                    {
                        _knownPeers.Remove(oldKey);
                    }
                }
                if (!_knownPeers.Contains(key))
                {
                    _knownPeers.Add(key);
                }
            }
            finally
            {
                _pendingLock.Release();
            }

            // Пропуск self-пиров
            var peerIP = endpoint.Address.ToString();
            if (_localIPs.Contains(peerIP))
            {
                Logger.LogInfo($"[Swarm] Пропуск self-пира: {key}");
                return;
            }

            // Пропуск уже подключённых
            if (_wires.ContainsKey(key)) return;

            if (_disposed) return;
            await _pendingLock.WaitAsync();
            try
            {
                if (_disposed) return;
                if (_pendingConnections.Contains(key)) return;
                if (_pendingConnections.Count >= MaxHalfOpen) return;
                _pendingConnections.Add(key);
            }
            finally
            {
                _pendingLock.Release();
            }

            // Пропуск недавно неудачных
            if (_failedPeers.TryGetValue(key, out var failTime))
            {
                if (DateTime.UtcNow - failTime < PeerRetryDelay)
                {
                    await RemoveFromPendingAsync(key);
                    return;
                }
                _failedPeers.TryRemove(key, out _);
            }

            // Пропуск при достижении лимита соединений
            if (_wires.Count >= MaxConnections)
            {
                await RemoveFromPendingAsync(key);
                return;
            }

            Logger.LogInfo($"[Swarm] Подключение к {key}");

            var wire = new Wire(_peerId, _metadata.InfoHash, _metadata.PieceCount);
            SetupWireEvents(wire);

            try
            {
                if (await wire.ConnectAsync(endpoint, _cts?.Token ?? default))
                {
                    if (_wires.TryAdd(key, wire))
                    {
                        await SendBitfieldAndInterestedAsync(wire);
                        Logger.LogInfo($"[Swarm] Подключено к {key}");
                    }
                    else
                    {
                        UnsubscribeWireEvents(wire);
                        wire.Dispose();
                    }
                }
                else
                {
                    // КРИТИЧНО: Ограничиваем размер коллекции для предотвращения утечки памяти
                    if (_failedPeers.Count >= MaxFailedPeers && !_failedPeers.ContainsKey(key))
                    {
                        // Удаляем самые старые записи
                        var oldestEntries = _failedPeers
                            .OrderBy(kv => kv.Value)
                            .Take(_failedPeers.Count - MaxFailedPeers + 50)
                            .Select(kv => kv.Key)
                            .ToList();
                        foreach (var oldKey in oldestEntries)
                        {
                            _failedPeers.TryRemove(oldKey, out _);
                        }
                    }
                    _failedPeers[key] = DateTime.UtcNow;
                    UnsubscribeWireEvents(wire);
                    wire.Dispose();
                }
            }
            catch
            {
                // КРИТИЧНО: Ограничиваем размер коллекции для предотвращения утечки памяти
                if (_failedPeers.Count >= MaxFailedPeers && !_failedPeers.ContainsKey(key))
                {
                    // Удаляем самые старые записи
                    var oldestEntries = _failedPeers
                        .OrderBy(kv => kv.Value)
                        .Take(_failedPeers.Count - MaxFailedPeers + 50)
                        .Select(kv => kv.Key)
                        .ToList();
                    foreach (var oldKey in oldestEntries)
                    {
                        _failedPeers.TryRemove(oldKey, out _);
                    }
                }
                _failedPeers[key] = DateTime.UtcNow;
                UnsubscribeWireEvents(wire);
                wire.Dispose();
            }
            finally
            {
                await RemoveFromPendingAsync(key);
            }
        }

        private async Task RemoveFromPendingAsync(string key)
        {
            if (_disposed) return;
            await _pendingLock.WaitAsync();
            try
            {
                if (_disposed) return;
                _pendingConnections.Remove(key);
            }
            finally
            {
                _pendingLock.Release();
            }
        }

        private void SetupWireEvents(Wire wire)
        {
            // Устанавливаем колбэки для Wire вместо событий
            var wireCallbacks = new WireCallbacksWrapper(this, wire);
            wire.SetCallbacks(wireCallbacks);
            
            // Сохраняем обёртку для очистки при удалении
            _wireHandlers[wire] = new WireEventHandlers { CallbacksWrapper = wireCallbacks };
        }
        
        /// <summary>Очищает колбэки Wire для предотвращения утечек памяти</summary>
        private void UnsubscribeWireEvents(Wire wire)
        {
            if (_wireHandlers.TryRemove(wire, out var handlers))
            {
                wire.SetCallbacks(null!);
            }
        }

        private async Task SendBitfieldAndInterestedAsync(Wire wire)
        {
            // Отправляем наше битовое поле
            await wire.SendBitfieldAsync(Bitfield);
            // Интерес отправим после получения их битового поля
        }

        #endregion

        #region Обработчики событий пиров

        internal async void OnPeerBitfieldAsync(Wire wire, BitArray peerBitfield)
        {
            try
            {
                // Обновляем доступность кусков
                for (int i = 0; i < peerBitfield.Length && i < _pieceAvailability.Length; i++)
                {
                    if (peerBitfield[i])
                    {
                        Interlocked.Increment(ref _pieceAvailability[i]);
                    }
                }

                // Проверяем, есть ли у пира нужные нам куски
                if (peerBitfield.HasPiecesWeNeed(Bitfield))
                {
                    await wire.SendInterestedAsync();
                    Logger.LogInfo($"[Swarm] Заинтересованы в {wire.RemoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Swarm] Ошибка в OnPeerBitfield: {ex.Message}");
            }
        }

        internal void OnPeerHave(Wire wire, int pieceIndex)
        {
            if (pieceIndex >= 0 && pieceIndex < _pieceAvailability.Length)
            {
                Interlocked.Increment(ref _pieceAvailability[pieceIndex]);
            }

            // Если нам нужен этот кусок и мы не заинтересованы - становимся заинтересованы
            if (!Bitfield[pieceIndex] && !wire.AmInterested)
            {
                _ = wire.SendInterestedAsync();
            }
        }

        internal static void OnPeerUnchoke(Wire wire)
        {
            Logger.LogInfo($"[Swarm] Разблокированы пиром {wire.RemoteEndPoint}");
            // Цикл загрузки обработает запросы кусков
        }

        internal static async void OnPeerInterestedAsync(Wire wire)
        {
            try
            {
                Logger.LogInfo($"[Swarm] Пир {wire.RemoteEndPoint} заинтересован в наших данных");
                
                // Разблокируем заинтересованных пиров (простая стратегия)
                if (wire.AmChoking)
                {
                    await wire.SendUnchokeAsync();
                    Logger.LogInfo($"[Swarm] Разблокировали {wire.RemoteEndPoint} для отдачи");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Swarm] Ошибка в OnPeerInterested: {ex.Message}");
            }
        }

        internal async void OnPeerRequestAsync(Wire wire, Wire.BlockRequest request)
        {
            try
            {
                // Отвечаем только если у нас есть кусок и пир не заблокирован
                if (!wire.AmChoking && request.Index >= 0 && request.Index < Bitfield.Length && Bitfield[request.Index])
                {
                    var pieceData = await _storage.ReadPieceAsync(request.Index);
                    if (pieceData != null && request.Begin >= 0 && request.Begin + request.Length <= pieceData.Length)
                    {
                        // Ожидаем токены для ограничения скорости отдачи
                        await WaitForUploadTokensAsync(request.Length, _cts?.Token ?? default);

                        // Отправляем блок напрямую без копирования в промежуточный массив
                        await wire.SendPieceAsync(request.Index, request.Begin, pieceData, request.Begin, request.Length);
                        Uploaded += request.Length;
                        
                        // Логируем отдачу (реже, чтобы не спамить)
                        if (Uploaded % (1024 * 1024) < request.Length)
                        {
                            Logger.LogInfo($"[Swarm] Отдано: {Uploaded / (1024 * 1024)} МБ");
                        }
                    }
                }
                else if (wire.AmChoking)
                {
                    Logger.LogWarning($"[Swarm] Запрос от {wire.RemoteEndPoint} отклонён - пир заблокирован");
                }
                else if (!Bitfield[request.Index])
                {
                    Logger.LogWarning($"[Swarm] Запрос куска {request.Index} от {wire.RemoteEndPoint} - у нас нет этого куска");
                }
            }
            catch (OperationCanceledException)
            {
                // Игнорируем - торрент остановлен
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Swarm] Ошибка в OnPeerRequest: {ex.Message}");
            }
        }

        internal async void OnPieceReceivedAsync(Wire wire, Wire.PieceData data)
        {
            int pieceIndex = -1;
            try
            {
                pieceIndex = data.Index;
                
                // КРИТИЧНО: Защита от параллельной обработки одного куска (может вызывать утечку памяти)
                // Если кусок уже обрабатывается, пропускаем этот вызов
                if (!_processingPieces.TryAdd(pieceIndex, true))
                {
                    return; // Уже обрабатывается
                }
                
                var begin = data.Begin;
                var blockIndex = begin / BlockSize;

                // Проверка валидности индекса
                if (pieceIndex < 0 || pieceIndex >= _metadata.PieceCount)
                {
                    Logger.LogWarning($"[Swarm] Неверный индекс куска: {pieceIndex}");
                    return;
                }

                // Уменьшаем счётчик ожидающих запросов для этого соединения
                _pendingRequestsPerWire.AddOrUpdate(wire, 0, (_, v) => Math.Max(0, v - 1));

                // КРИТИЧНО: Проверяем лимит PieceState перед созданием
                // Если лимит превышен, пытаемся очистить неиспользуемые состояния
                if (_pieceStates.Count >= 100 && !_pieceStates.ContainsKey(pieceIndex))
                {
                    // Быстрая очистка неиспользуемых состояний (более агрессивная)
                    var unusedStates = _pieceStates
                        .Where(kvp => !kvp.Value.Downloading && 
                                     (kvp.Value.RequestedBlocks == null || kvp.Value.RequestedBlocks.Count == 0) &&
                                     kvp.Value.ReceivedBlocks.Count == 0 &&
                                     DateTime.UtcNow - kvp.Value.StartTime > TimeSpan.FromSeconds(15)) // Уменьшено с 30 до 15 секунд
                        .Take(20) // Увеличено с 10 до 20 для более агрессивной очистки
                        .ToList();
                    
                    foreach (var unused in unusedStates)
                    {
                        var unusedState = unused.Value;
                        _pieceStates.TryRemove(unused.Key, out _);
                        if (_pieceBuffers.TryRemove(unused.Key, out var buffer))
                        {
                            try { buffer.Return(); } catch { }
                        }
                        // КРИТИЧНО: Очищаем коллекции в PieceState перед удалением
                        unusedState.ReceivedBlocks.Clear();
                        unusedState.RequestedBlocks?.Clear();
                        unusedState.RequestTimes?.Clear();
                    }
                }

                // Получаем или создаём состояние куска
                var pieceLength = _metadata.GetPieceLength(pieceIndex);
                var totalBlocks = (pieceLength + BlockSize - 1) / BlockSize;

                var state = _pieceStates.GetOrAdd(pieceIndex, _ => new PieceState
                {
                    TotalBlocks = totalBlocks,
                    StartTime = DateTime.UtcNow
                });

                // Получаем или создаём буфер куска из ArrayPool
                var pooledBuffer = _pieceBuffers.GetOrAdd(pieceIndex, _ => new PooledBuffer(pieceLength));

                // Копируем данные блока
                if (begin >= 0 && begin + data.Data.Length <= pooledBuffer.Length)
                {
                    Array.Copy(data.Data, 0, pooledBuffer.Array, begin, data.Data.Length);
                    // КРИТИЧНО: Используем TryAddReceivedBlock для предотвращения утечки памяти
                    state.TryAddReceivedBlock(blockIndex);
                    
                    // Удаляем из списка запрошенных (блок получен)
                    state.RequestedBlocks?.Remove(blockIndex);
                    state.RequestTimes?.Remove(blockIndex);

                    // Учитываем полученные байты для расчёта скорости
                    DownloadedBytes += data.Data.Length;
                    
                    // Потребляем токены для ограничения скорости
                    ConsumeDownloadTokens(data.Data.Length);

                    // Логируем реже для уменьшения спама
                    if (state.ReceivedBlocks.Count % 50 == 0 || state.ReceivedBlocks.Count == totalBlocks)
                    {
                        Logger.LogInfo($"[Swarm] Кусок {pieceIndex}: {state.ReceivedBlocks.Count}/{totalBlocks} блоков от {wire.RemoteEndPoint}");
                    }
                }

                // Проверяем завершённость куска
                if (state.ReceivedBlocks.Count >= totalBlocks)
                {
                    // Создаём массив точного размера для записи (буфер из пула может быть больше)
                    var pieceData = new byte[pooledBuffer.Length];
                    Array.Copy(pooledBuffer.Array, pieceData, pooledBuffer.Length);
                    
                    // Проверяем и сохраняем кусок
                    if (await _storage.WritePieceAsync(pieceIndex, pieceData))
                    {
                        Bitfield[pieceIndex] = true;
                        Downloaded += pieceLength;

                        _pieceStates.TryRemove(pieceIndex, out _);
                        if (_pieceBuffers.TryRemove(pieceIndex, out var removedBuffer))
                        {
                            removedBuffer.Return(); // Возвращаем буфер в пул
                        }

                        Logger.LogInfo($"[Swarm] Кусок {pieceIndex} завершён! ({Bitfield.SetCount}/{Bitfield.Length})");

                        // Уведомляем всех пиров о наличии куска
                        foreach (var w in _wires.Values.Where(w => w.IsConnected))
                        {
                            _ = w.SendHaveAsync(pieceIndex);
                        }

                        // КРИТИЧНО: Используем SafeTaskRunner для предотвращения утечки памяти через необработанные исключения
                        // КРИТИЧНО: Захватываем ссылку в локальную переменную для предотвращения race condition
                        var callbacks = _callbacks;
                        if (callbacks != null)
                        {
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnPieceCompletedAsync(pieceIndex).ConfigureAwait(false));
                        }

                        // Проверяем завершённость загрузки
                        if (Bitfield.IsComplete)
                        {
                            Logger.LogInfo("[Swarm] Загрузка завершена!");
                            // КРИТИЧНО: Используем SafeTaskRunner для предотвращения утечки памяти через необработанные исключения
                            // Используем уже захваченную ссылку callbacks
                            if (callbacks != null)
                            {
                                SafeTaskRunner.RunSafe(async () => await callbacks.OnDownloadCompleteAsync().ConfigureAwait(false));
                            }
                        }
                    }
                    else
                    {
                        // Проверка хэша не прошла, повторим
                        state.ReceivedBlocks.Clear();
                        state.RequestedBlocks?.Clear();
                        state.RequestTimes?.Clear();
                        if (_pieceBuffers.TryRemove(pieceIndex, out var failedBuffer))
                        {
                            failedBuffer.Return(); // Возвращаем буфер в пул
                        }
                        Logger.LogWarning($"[Swarm] Проверка куска {pieceIndex} не прошла, повторим");
                    }

                    state.Downloading = false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Swarm] Ошибка в OnPieceReceived: {ex.Message}");
                
                // КРИТИЧНО: При любой ошибке возвращаем буфер в пул, чтобы предотвратить утечку памяти
                if (pieceIndex >= 0)
                {
                    // Сбрасываем состояние куска
                    if (_pieceStates.TryGetValue(pieceIndex, out var errorState))
                    {
                        errorState.ReceivedBlocks.Clear();
                        errorState.RequestedBlocks?.Clear();
                        errorState.RequestTimes?.Clear();
                        errorState.Downloading = false;
                    }
                    
                    // Возвращаем буфер в пул
                    if (_pieceBuffers.TryRemove(pieceIndex, out var errorBuffer))
                    {
                        try
                        {
                            errorBuffer.Return();
                            Logger.LogInfo($"[Swarm] Возвращён буфер куска {pieceIndex} в пул после ошибки");
                        }
                        catch (Exception returnEx)
                        {
                            Logger.LogWarning($"[Swarm] Ошибка возврата буфера в пул: {returnEx.Message}");
                        }
                    }
                }
            }
            finally
            {
                // КРИТИЧНО: Освобождаем флаг обработки куска
                if (pieceIndex >= 0)
                {
                    _processingPieces.TryRemove(pieceIndex, out _);
                }
            }
        }

        internal void OnPeerDisconnected(Wire wire)
        {
            var key = wire.RemoteEndPoint?.ToString() ?? "unknown";
            _wires.TryRemove(key, out _);
            _pendingRequestsPerWire.TryRemove(wire, out _);

            // Обновляем доступность
            if (wire.PeerBitfield != null)
            {
                for (int i = 0; i < wire.PeerBitfield.Length && i < _pieceAvailability.Length; i++)
                {
                    if (wire.PeerBitfield[i])
                    {
                        Interlocked.Decrement(ref _pieceAvailability[i]);
                    }
                }
            }

            Logger.LogInfo($"[Swarm] Отключён от {key} (осталось: {_wires.Count})");
            
            // КРИТИЧНО: Отписываемся от событий перед освобождением для предотвращения утечек памяти
            UnsubscribeWireEvents(wire);
            
            // Освобождаем ресурсы Wire
            wire.Dispose();
        }

        #endregion

        #region Цикл загрузки

        private async Task DownloadLoopAsync(CancellationToken cancellationToken)
        {
            var lastCleanup = DateTime.UtcNow;
            var lastStats = DateTime.UtcNow;
            var lastRequestTime = DateTime.UtcNow;
            
            while (!cancellationToken.IsCancellationRequested && !Bitfield.IsComplete)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    
                    // Проверяем, есть ли токены для загрузки (только если установлен лимит)
                    if (MaxDownloadSpeed.HasValue && MaxDownloadSpeed.Value > 0 && !HasDownloadTokens())
                    {
                        // Небольшая задержка при отсутствии токенов для снижения нагрузки на CPU
                        await Task.Delay(10, cancellationToken);
                        continue;
                    }

                    // Оптимизация: запрашиваем куски не чаще чем раз в 5мс для снижения нагрузки на CPU
                    if ((now - lastRequestTime).TotalMilliseconds >= 5)
                    {
                        RequestPieces(cancellationToken);
                        lastRequestTime = now;
                    }
                    
                    // Минимальная задержка для предотвращения 100% загрузки CPU
                    // 2мс оптимально для баланса скорости и нагрузки на CPU
                    await Task.Delay(2, cancellationToken);

                    // Отправляем статистику каждые 250мс для более плавного отображения
                    if ((now - lastStats).TotalMilliseconds >= 250)
                    {
                        // КРИТИЧНО: Используем SafeTaskRunner для предотвращения утечки памяти через необработанные исключения
                        // КРИТИЧНО: Захватываем ссылку в локальную переменную для предотвращения race condition
                        var callbacks = _callbacks;
                        if (callbacks != null)
                        {
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnStatsUpdatedAsync(Downloaded, DownloadedBytes, Uploaded, ConnectedPeers).ConfigureAwait(false));
                        }
                        lastStats = now;
                    }

                    // КРИТИЧНО: Очистка отключённых соединений и застрявших запросов каждые 3 секунды
                    // Увеличена частота для более агрессивной очистки утечек памяти
                    if ((now - lastCleanup).TotalSeconds >= 3)
                    {
                        await CleanupDisconnectedWiresAsync();
                        CleanupStaleRequests();
                        lastCleanup = now;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("[Swarm] Ошибка цикла загрузки", ex);
                }
            }
        }
        
        /// <summary>Очищает застрявшие запросы для всех кусков</summary>
        private void CleanupStaleRequests()
        {
            var staleTimeout = TimeSpan.FromSeconds(30);
            var pieceTimeout = TimeSpan.FromMinutes(3); // Оптимизировано для баланса скорости и очистки
            var cleanedRequests = 0;
            var resetPieces = 0;
            var removedStates = 0;
            
            // КРИТИЧНО: Ограничиваем общее количество PieceState (не только Downloading)
            // Это предотвращает накопление неиспользуемых состояний
            const int MaxTotalPieces = 100; // Уменьшено с 150 до 100 для более агрессивной очистки
            const int MaxConcurrentPieces = 80; // Уменьшено с 100 до 80 для более агрессивной очистки
            
            // Если общее количество PieceState превышает лимит, удаляем самые старые неиспользуемые
            if (_pieceStates.Count > MaxTotalPieces)
            {
                var toRemove = _pieceStates
                    .Where(kvp => !kvp.Value.Downloading && 
                                 (kvp.Value.RequestedBlocks == null || kvp.Value.RequestedBlocks.Count == 0) &&
                                 kvp.Value.ReceivedBlocks.Count == 0)
                    .OrderBy(kvp => kvp.Value.StartTime)
                    .Take(_pieceStates.Count - MaxTotalPieces)
                    .ToList();
                
                foreach (var kvp in toRemove)
                {
                    var pieceIndex = kvp.Key;
                    var state = kvp.Value;
                    _pieceStates.TryRemove(pieceIndex, out _);
                    if (_pieceBuffers.TryRemove(pieceIndex, out var buffer))
                    {
                        try { buffer.Return(); } catch { }
                    }
                    // КРИТИЧНО: Очищаем коллекции в PieceState перед удалением
                    state.ReceivedBlocks.Clear();
                    state.RequestedBlocks?.Clear();
                    state.RequestTimes?.Clear();
                    removedStates++;
                }
            }
            
            var activePieces = _pieceStates.Values.Count(s => s.Downloading);
            if (activePieces > MaxConcurrentPieces)
            {
                // Сбрасываем самые старые куски без прогресса
                var oldestPieces = _pieceStates
                    .Where(kvp => kvp.Value.Downloading)
                    .OrderBy(kvp => kvp.Value.StartTime)
                    .Take(activePieces - MaxConcurrentPieces)
                    .ToList();
                
                foreach (var kvp in oldestPieces)
                {
                    var pieceIndex = kvp.Key;
                    var state = kvp.Value;
                    
                    // Сбрасываем только если нет прогресса
                    // ОПТИМИЗАЦИЯ: Используем более эффективную проверку
                    if (state.ReceivedBlocks.Count == 0)
                    {
                        _pieceStates.TryRemove(pieceIndex, out _);
                        if (_pieceBuffers.TryRemove(pieceIndex, out var buffer))
                        {
                            try { buffer.Return(); } catch { }
                        }
                        // КРИТИЧНО: Очищаем коллекции в PieceState перед удалением
                        state.ReceivedBlocks.Clear();
                        state.RequestedBlocks?.Clear();
                        state.RequestTimes?.Clear();
                        removedStates++;
                    }
                }
            }
            
            foreach (var kvp in _pieceStates.ToList())
            {
                var pieceIndex = kvp.Key;
                var state = kvp.Value;
                
                // Пропускаем уже загруженные куски
                if (Bitfield[pieceIndex])
                {
                    _pieceStates.TryRemove(pieceIndex, out _);
                    if (_pieceBuffers.TryRemove(pieceIndex, out var completedBuffer))
                    {
                        try { completedBuffer.Return(); } catch { }
                    }
                    removedStates++;
                    continue;
                }
                
                // КРИТИЧНО: Удаляем PieceState, которые не используются (не Downloading, нет запросов, нет полученных блоков)
                // Уменьшено время с 1 минуты до 30 секунд для более агрессивной очистки
                if (!state.Downloading && 
                    (state.RequestedBlocks == null || state.RequestedBlocks.Count == 0) &&
                    state.ReceivedBlocks.Count == 0 &&
                    DateTime.UtcNow - state.StartTime > TimeSpan.FromSeconds(30))
                {
                    _pieceStates.TryRemove(pieceIndex, out _);
                    if (_pieceBuffers.TryRemove(pieceIndex, out var unusedBuffer))
                    {
                        try { unusedBuffer.Return(); } catch { }
                    }
                    // КРИТИЧНО: Очищаем коллекции в PieceState перед удалением
                    state.ReceivedBlocks.Clear();
                    state.RequestedBlocks?.Clear();
                    state.RequestTimes?.Clear();
                    removedStates++;
                    continue;
                }
                
                // КРИТИЧНО: Очищаем ReceivedBlocks если слишком большой
                state.TrimReceivedBlocks();
                
                // Очищаем застрявшие запросы
                // ОПТИМИЗАЦИЯ: Используем более эффективную проверку
                if (state.RequestedBlocks != null && state.RequestedBlocks.Count != 0)
                {
                    var beforeCount = state.RequestedBlocks.Count;
                    state.ClearStaleRequests(staleTimeout);
                    cleanedRequests += beforeCount - state.RequestedBlocks.Count;
                }
                
                // Сбрасываем кусок если загружается слишком долго без прогресса
                if (state.Downloading && DateTime.UtcNow - state.StartTime > pieceTimeout)
                {
                    // Если получено меньше половины блоков - сбрасываем
                    if (state.ReceivedBlocks.Count < state.TotalBlocks / 2)
                    {
                        state.ReceivedBlocks.Clear();
                        state.RequestedBlocks?.Clear();
                        state.RequestTimes?.Clear();
                        state.Downloading = false;
                        state.StartTime = DateTime.UtcNow;
                        
                        // КРИТИЧНО: Возвращаем буфер в пул для предотвращения утечки памяти
                        if (_pieceBuffers.TryRemove(pieceIndex, out var staleBuffer))
                        {
                            try
                            {
                                staleBuffer.Return(); // Возвращаем буфер в пул
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"[Swarm] Ошибка возврата буфера куска {pieceIndex} в пул: {ex.Message}");
                            }
                        }
                        
                        resetPieces++;
                        Logger.LogWarning($"[Swarm] Сброс застрявшего куска {pieceIndex}");
                    }
                }
                
                // Дополнительная проверка: если кусок уже загружен, но состояние не удалено
                if (Bitfield[pieceIndex] && _pieceStates.ContainsKey(pieceIndex))
                {
                    _pieceStates.TryRemove(pieceIndex, out _);
                    if (_pieceBuffers.TryRemove(pieceIndex, out var completedBuffer))
                    {
                        try
                        {
                            completedBuffer.Return(); // Возвращаем буфер в пул
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[Swarm] Ошибка возврата буфера завершённого куска {pieceIndex} в пул: {ex.Message}");
                        }
                    }
                    removedStates++;
                }
            }
            
            if (cleanedRequests > 0 || resetPieces > 0 || removedStates > 0)
            {
                Logger.LogInfo($"[Swarm] Очистка: {cleanedRequests} запросов, {resetPieces} сброшенных кусков, {removedStates} удалённых состояний");
            }
        }

        /// <summary>Цикл раздачи (seeding) - для полностью загруженных торрентов</summary>
        private async Task SeedingLoopAsync(CancellationToken cancellationToken)
        {
            var lastCleanup = DateTime.UtcNow;
            var lastStats = DateTime.UtcNow;
            
            Logger.LogInfo("[Swarm] Запущен цикл раздачи (seeding)");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // В режиме раздачи просто поддерживаем соединения и отвечаем на запросы
                    await Task.Delay(100, cancellationToken);

                    var now = DateTime.UtcNow;
                    
                    // Отправляем статистику каждые 500мс
                    if ((now - lastStats).TotalMilliseconds >= 500)
                    {
                        // КРИТИЧНО: Используем SafeTaskRunner для предотвращения утечки памяти через необработанные исключения
                        // КРИТИЧНО: Захватываем ссылку в локальную переменную для предотвращения race condition
                        var callbacks = _callbacks;
                        if (callbacks != null)
                        {
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnStatsUpdatedAsync(Downloaded, DownloadedBytes, Uploaded, ConnectedPeers).ConfigureAwait(false));
                        }
                        lastStats = now;
                    }

                    // КРИТИЧНО: Очистка отключённых соединений каждые 5 секунд
                    // Увеличена частота для более агрессивной очистки утечек памяти
                    if ((now - lastCleanup).TotalSeconds >= 5)
                    {
                        await CleanupDisconnectedWiresAsync();
                        lastCleanup = now;
                    }
                    
                    // Разблокируем заинтересованных пиров, которые ещё заблокированы
                    foreach (var wire in _wires.Values.Where(w => w.IsConnected && w.PeerInterested && w.AmChoking))
                    {
                        try
                        {
                            await wire.SendUnchokeAsync();
                            Logger.LogInfo($"[Swarm] Разблокировали {wire.RemoteEndPoint} для раздачи");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[Swarm] Ошибка разблокировки пира: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("[Swarm] Ошибка цикла раздачи", ex);
                }
            }
        }

        /// <summary>Проверяет наличие токенов для загрузки (локальный + глобальный лимит)</summary>
        private bool HasDownloadTokens()
        {
            // Проверяем глобальный лимит (без потребления токенов)
            if (GlobalSpeedLimiter.Instance.MaxDownloadSpeed.HasValue && 
                GlobalSpeedLimiter.Instance.MaxDownloadSpeed.Value > 0)
            {
                // Проверяем глобальный лимит без блокировки для производительности
                if (!GlobalSpeedLimiter.Instance.TryConsumeDownload(0))
                    return false;
            }
            
            // Проверяем локальный лимит
            if (MaxDownloadSpeed == null || MaxDownloadSpeed.Value <= 0)
                return true;

            // Используем неблокирующую проверку для производительности
            if (!_speedLimitLock.Wait(0))
                return true; // Если не можем заблокировать - продолжаем для производительности
            
            try
            {
                RefillTokens();
                return _downloadTokens >= BlockSize;
            }
            finally
            {
                _speedLimitLock.Release();
            }
        }

        /// <summary>Потребляет токены загрузки (локальный + глобальный)</summary>
        public void ConsumeDownloadTokens(int bytes)
        {
            // Потребляем глобальные токены
            GlobalSpeedLimiter.Instance.TryConsumeDownload(bytes);
            
            // Потребляем локальные токены
            if (MaxDownloadSpeed == null || MaxDownloadSpeed.Value <= 0)
                return;

            _speedLimitLock.Wait();
            try
            {
                _downloadTokens -= bytes;
            }
            finally
            {
                _speedLimitLock.Release();
            }
        }

        private async Task CleanupDisconnectedWiresAsync()
        {
            var disconnectedKeys = _wires
                .Where(kv => !kv.Value.IsConnected)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in disconnectedKeys)
            {
                if (_wires.TryRemove(key, out var wire))
                {
                    // КРИТИЧНО: Отписываемся от событий перед освобождением
                    UnsubscribeWireEvents(wire);
                    wire.Dispose();
                }
            }
            
            // КРИТИЧНО: Очистка застрявших соединений в _pendingConnections (старше 30 секунд)
            // Это предотвращает накопление застрявших соединений
            await _pendingLock.WaitAsync();
            try
            {
                // Удаляем пиры из _pendingConnections, которые уже подключены или неактивны
                var connectedKeys = _wires.Keys.ToHashSet();
                var toRemoveFromPending = _pendingConnections
                    .Where(k => connectedKeys.Contains(k))
                    .ToList();
                foreach (var key in toRemoveFromPending)
                {
                    _pendingConnections.Remove(key);
                }
            }
            finally
            {
                _pendingLock.Release();
            }
            
            // Очистка устаревших неудачных пиров (старше PeerRetryDelay)
            var now = DateTime.UtcNow;
            var expiredPeers = _failedPeers
                .Where(kv => now - kv.Value > PeerRetryDelay)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in expiredPeers)
            {
                _failedPeers.TryRemove(key, out _);
            }
            
            // КРИТИЧНО: Ограничение размера _failedPeers (более агрессивная очистка)
            if (_failedPeers.Count > MaxFailedPeers)
            {
                var oldestPeers = _failedPeers
                    .OrderBy(kv => kv.Value)
                    .Take(_failedPeers.Count - MaxFailedPeers)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var key in oldestPeers)
                {
                    _failedPeers.TryRemove(key, out _);
                }
            }
            
            // КРИТИЧНО: Ограничение размера _knownPeers (более агрессивная очистка)
            await _pendingLock.WaitAsync();
            try
            {
                if (_knownPeers.Count > MaxKnownPeers)
                {
                    // Удаляем случайные пиры, оставляя подключённые
                    var connectedKeys = _wires.Keys.ToHashSet();
                    var toRemove = _knownPeers
                        .Where(k => !connectedKeys.Contains(k))
                        .Take(_knownPeers.Count - MaxKnownPeers)
                        .ToList();
                    foreach (var key in toRemove)
                    {
                        _knownPeers.Remove(key);
                    }
                }
            }
            finally
            {
                _pendingLock.Release();
            }

            var connected = _wires.Values.Count(w => w.IsConnected);
            var unchoked = _wires.Values.Count(w => w.IsConnected && !w.PeerChoking);
            var interestedInUs = _wires.Values.Count(w => w.IsConnected && w.PeerInterested);
            Logger.LogInfo($"[Swarm] Пиры: {connected} подкл, {unchoked} разблок, {interestedInUs} хотят наши | ↓{DownloadedBytes:N0} ↑{Uploaded:N0}");
        }

        private void RequestPieces(CancellationToken cancellationToken)
        {
            // Получаем разблокированные соединения с нужными кусками
            var availableWires = _wires.Values
                .Where(w => w.IsConnected && !w.PeerChoking && w.PeerBitfield != null)
                .ToList();

            if (availableWires.Count == 0) return;

            // Ограничение памяти: вычисляем текущее использование буферов
            var currentBuffers = _pieceBuffers.Count;
            var pieceSize = _metadata.PieceLength;
            var currentMemory = (long)currentBuffers * pieceSize;
            
            // Не создавать новые буферы если превышен лимит памяти или количества
            if (currentBuffers >= MaxPiecesToRequest || currentMemory >= MaxBufferMemory)
            {
                return; // Ждём пока часть буферов освободится
            }

            // Вычисляем общую пропускную способность (сколько запросов можем отправить)
            int totalAvailableSlots = availableWires.Sum(w => MaxRequestsPerWire - _pendingRequestsPerWire.GetValueOrDefault(w, 0));
            if (totalAvailableSlots <= 0) return;

            // Находим куски для загрузки - ограничиваем памятью и количеством
            var maxByMemory = (int)((MaxBufferMemory - currentMemory) / pieceSize);
            var availableSlots = Math.Min(MaxPiecesToRequest - currentBuffers, maxByMemory);
            // ОПТИМИЗАЦИЯ: Используем все доступные слоты для максимальной скорости
            var piecesToDownload = GetPiecesToRequest(Math.Min(Math.Max(1, availableSlots), totalAvailableSlots));

            // Собираем все запросы для параллельной отправки
            List<Task> requestTasks = [];
            int totalRequestsSent = 0;
            const int maxRequestsPerIteration = 5000; // ОПТИМИЗАЦИЯ: Увеличено для максимальной скорости закачки

            foreach (var pieceIndex in piecesToDownload)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (totalRequestsSent >= maxRequestsPerIteration) break;

                // ОПТИМИЗАЦИЯ: Находим соединения с этим куском
                var wiresWithPiece = availableWires
                    .Where(w => w.PeerBitfield != null &&
                               pieceIndex < w.PeerBitfield.Length &&
                               w.PeerBitfield[pieceIndex])
                    .ToList(); // Материализуем для многократного использования

                if (wiresWithPiece.Count == 0) continue;

                // КРИТИЧНО: Проверяем, не превышен ли лимит PieceState перед созданием нового
                if (_pieceStates.Count >= 150)
                {
                    // Если лимит превышен, пропускаем этот кусок
                    continue;
                }

                // Получаем или создаём состояние куска
                var pieceLength = _metadata.GetPieceLength(pieceIndex);
                var totalBlocks = (pieceLength + BlockSize - 1) / BlockSize;

                var state = _pieceStates.GetOrAdd(pieceIndex, _ => new PieceState
                {
                    TotalBlocks = totalBlocks,
                    StartTime = DateTime.UtcNow
                });

                state.Downloading = true;

                // Отслеживаем запрошенные блоки с временем запроса
                var requestedBlocks = state.RequestedBlocks ??= [];
                var requestTimes = state.RequestTimes ??= [];
                
                // КРИТИЧНО: Очищаем застрявшие запросы перед добавлением новых
                state.ClearStaleRequests(TimeSpan.FromSeconds(30));

                // Запрашиваем ВСЕ отсутствующие блоки куска
                for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
                {
                    if (state.ReceivedBlocks.Contains(blockIndex)) continue;
                    if (requestedBlocks.Contains(blockIndex)) continue;
                    if (totalRequestsSent >= maxRequestsPerIteration) break;
                    
                    // КРИТИЧНО: Проверяем лимиты перед добавлением для предотвращения утечки
                    if (requestedBlocks.Count >= 500 || requestTimes.Count >= 2000)
                    {
                        // Если достигнут лимит, очищаем старые запросы
                        state.ClearStaleRequests(TimeSpan.FromSeconds(10)); // Более агрессивная очистка
                        
                        // Если лимит все еще превышен, пропускаем этот блок
                        if (requestedBlocks.Count >= 500 || requestTimes.Count >= 2000)
                        {
                            continue;
                        }
                    }

                    // Находим соединение с минимальной очередью запросов
                    var selectedWire = wiresWithPiece
                        .Where(w => _pendingRequestsPerWire.GetValueOrDefault(w, 0) < MaxRequestsPerWire)
                        .MinBy(w => _pendingRequestsPerWire.GetValueOrDefault(w, 0));

                    if (selectedWire == null) break; // Все соединения для этого куска заняты

                    var begin = blockIndex * BlockSize;
                    var length = Math.Min(BlockSize, pieceLength - begin);

                    _pendingRequestsPerWire.AddOrUpdate(selectedWire, 1, (_, v) => v + 1);
                    requestedBlocks.Add(blockIndex);
                    requestTimes[blockIndex] = DateTime.UtcNow;
                    
                    // Добавляем запрос в список для параллельной отправки
                    requestTasks.Add(selectedWire.SendRequestAsync(pieceIndex, begin, length));
                    totalRequestsSent++;
                }
            }
            
            // КРИТИЧНО: Отправляем все запросы параллельно с обработкой ошибок
            // Используем fire-and-forget паттерн для предотвращения утечки памяти
            if (requestTasks.Count > 0)
            {
                // КРИТИЧНО: Защита от накопления задач - если предыдущая задача еще выполняется, пропускаем
                if (_isRequestingPieces)
                {
                    // Очищаем задачи, чтобы не удерживать ссылки
                    requestTasks.Clear();
                    return;
                }
                
                _isRequestingPieces = true;
                
                // Запускаем задачи и обрабатываем ошибки асинхронно, не удерживая ссылки
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.WhenAll(requestTasks).ConfigureAwait(false);
                    }
                    catch (AggregateException aggEx)
                    {
                        // КРИТИЧНО: Обрабатываем ошибки, чтобы не удерживать ссылки на исключения
                        // Используем только сообщения, не сохраняя ссылки на объекты исключений
                        try
                        {
                            foreach (var ex in aggEx.InnerExceptions)
                            {
                                // Логируем только критические ошибки
                                if (ex is not OperationCanceledException)
                                {
                                    Logger.LogWarning($"[Swarm] Ошибка отправки запроса: {ex.Message}");
                                }
                            }
                        }
                        finally
                        {
                            // КРИТИЧНО: Явно очищаем ссылку на AggregateException для предотвращения утечки памяти
                            aggEx = null!;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is not OperationCanceledException)
                        {
                            Logger.LogWarning($"[Swarm] Ошибка отправки запросов: {ex.Message}");
                        }
                    }
                    finally
                    {
                        // КРИТИЧНО: Очищаем ссылки на задачи для предотвращения утечки памяти
                        requestTasks.Clear();
                        _isRequestingPieces = false;
                    }
                });
            }
        }

        private List<int> GetPiecesToRequest(int count)
        {
            List<(int index, int availability, bool downloading)> pieces = [];

            for (int i = 0; i < _metadata.PieceCount; i++)
            {
                // Пропускаем уже загруженные куски
                if (Bitfield[i]) continue;

                // Проверяем, загружается ли уже
                bool downloading = _pieceStates.TryGetValue(i, out var state) && state.Downloading;

                // Учитываем только доступные куски
                var availability = _pieceAvailability[i];
                if (availability > 0)
                {
                    pieces.Add((i, availability, downloading));
                }
            }

            // Приоритет: загружаемые куски, затем редкие
            return [.. pieces
                .OrderByDescending(p => p.downloading)
                .ThenBy(p => p.availability)
                .ThenBy(p => p.index)
                .Take(count)
                .Select(p => p.index)];
        }

        #endregion

        #region Ограничение скорости (Token Bucket)

        /// <summary>Ожидает токены для загрузки</summary>
        private async Task WaitForDownloadTokensAsync(int bytes, CancellationToken ct)
        {
            if (MaxDownloadSpeed == null || MaxDownloadSpeed.Value <= 0)
                return;

            while (!ct.IsCancellationRequested)
            {
                await _speedLimitLock.WaitAsync(ct);
                try
                {
                    RefillTokens();

                    if (_downloadTokens >= bytes)
                    {
                        _downloadTokens -= bytes;
                        return;
                    }
                }
                finally
                {
                    _speedLimitLock.Release();
                }

                // Ждём немного и пробуем снова
                await Task.Delay(10, ct);
            }
        }

        /// <summary>Ожидает токены для отдачи (локальный + глобальный лимит)</summary>
        private async Task WaitForUploadTokensAsync(int bytes, CancellationToken ct)
        {
            // Сначала проверяем глобальный лимит
            await GlobalSpeedLimiter.Instance.WaitForUploadTokensAsync(bytes, ct).ConfigureAwait(false);
            
            // Затем локальный лимит торрента
            if (MaxUploadSpeed == null || MaxUploadSpeed.Value <= 0)
                return;

            while (!ct.IsCancellationRequested)
            {
                await _speedLimitLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    RefillTokens();

                    if (_uploadTokens >= bytes)
                    {
                        _uploadTokens -= bytes;
                        return;
                    }
                }
                finally
                {
                    _speedLimitLock.Release();
                }

                // Ждём немного и пробуем снова
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
        }

        /// <summary>Пополняет токены на основе прошедшего времени</summary>
        private void RefillTokens()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastTokenRefill).TotalSeconds;

            if (elapsed > 0)
            {
                if (MaxDownloadSpeed.HasValue)
                {
                    _downloadTokens += (long)(MaxDownloadSpeed.Value * elapsed);
                    // Ограничиваем накопление токенов одной секундой
                    _downloadTokens = Math.Min(_downloadTokens, MaxDownloadSpeed.Value);
                }

                if (MaxUploadSpeed.HasValue)
                {
                    _uploadTokens += (long)(MaxUploadSpeed.Value * elapsed);
                    _uploadTokens = Math.Min(_uploadTokens, MaxUploadSpeed.Value);
                }

                _lastTokenRefill = now;
            }
        }

        #endregion
    }
}
