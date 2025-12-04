using TorrentClient.Protocol.Interfaces;
using TorrentClient.Utilities;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// TorrentDiscovery - единый модуль для поиска пиров
    /// Объединяет DHT, Tracker, LSD, PEX
    /// </summary>
    public class TorrentDiscovery : IDisposable
    {
        private readonly string _infoHash;
        private readonly string _peerId;
        private readonly int _port;
        private readonly TrackerClient _trackerClient;
        private readonly List<string> _announceUrls;
        private readonly HashSet<IPEndPoint> _discoveredPeers = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        /// <summary>Максимальное количество обнаруженных пиров (ограничение памяти)</summary>
        private const int MaxDiscoveredPeers = 10000;
        
        private LocalServiceDiscovery? _lsd;
        private DhtClient? _dhtClient;
        private Task? _discoveryTask;
        private DateTime _lastTrackerAnnounce = DateTime.MinValue;
        private DateTime _lastDhtSearch = DateTime.MinValue;
        private DateTime _lastLsdAnnounce = DateTime.MinValue;
        private const int TrackerAnnounceInterval = 10; // Обновляем трекеры каждые 10 секунд
        private const int DhtSearchInterval = 15; // Обновляем DHT каждые 15 секунд
        private const int LsdAnnounceInterval = 60; // LSD каждые 60 секунд
        
        /// <summary>Флаг для предотвращения параллельных вызовов AnnounceToTrackersAsync (защита от утечки памяти)</summary>
        private volatile bool _isAnnouncing = false;
        
        // Обёртки колбэков для LSD и DHT
        private LocalServiceDiscoveryCallbacksWrapper? _lsdCallbacks;
        private DhtCallbacksWrapper? _dhtCallbacks;
        
        // Callback для получения актуальных данных о торренте
        private Func<long>? _getDownloadedBytes;
        private Func<long>? _getUploadedBytes;
        private Func<long>? _getLeftBytes;
        private Func<string?>? _getEvent;

        #region Асинхронные колбэки (замена событий)

        private ITorrentDiscoveryCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(ITorrentDiscoveryCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        public TorrentDiscovery(string infoHash, string peerId, int port, TrackerClient trackerClient, List<string> announceUrls)
        {
            _infoHash = infoHash;
            _peerId = peerId;
            _port = port;
            _trackerClient = trackerClient;
            _announceUrls = announceUrls ?? [];
        }
        
        public void SetTorrentStats(Func<long> getDownloadedBytes, Func<long> getUploadedBytes, Func<long> getLeftBytes, Func<string?> getEvent)
        {
            _getDownloadedBytes = getDownloadedBytes;
            _getUploadedBytes = getUploadedBytes;
            _getLeftBytes = getLeftBytes;
            _getEvent = getEvent;
        }

        public void Start()
        {
            // Запускаем LSD
            try
            {
                _lsd = new LocalServiceDiscovery(_infoHash, _port);
                _lsdCallbacks = new LocalServiceDiscoveryCallbacksWrapper(this);
                _lsd.SetCallbacks(_lsdCallbacks);
                _lsd.Start();
                Logger.LogInfo($"[Discovery] LSD started for info hash: {_infoHash}");
            }
            catch (Exception ex)
            {
                Logger.LogError("[Discovery] Error starting LSD", ex);
            }

            // Запускаем DHT
            try
            {
                var dhtPort = _port + 1;
                _dhtClient = new DhtClient(dhtPort);
                _dhtCallbacks = new DhtCallbacksWrapper(this);
                _dhtClient.SetCallbacks(_dhtCallbacks);
                _dhtClient.Start();
                Logger.LogInfo($"[Discovery] DHT started on port {dhtPort} for info hash: {_infoHash}");
            }
            catch (Exception ex)
            {
                Logger.LogError("[Discovery] Error starting DHT", ex);
            }

            // Запускаем непрерывный поиск пиров
            _discoveryTask = Task.Run(() => DiscoveryLoopAsync(_cancellationTokenSource.Token));
        }

        private async Task DiscoveryLoopAsync(CancellationToken cancellationToken)
        {
            // Первый запрос к трекерам сразу (параллельно)
            var trackerTask = AnnounceToTrackersAsync(cancellationToken);
            
            // Первый запрос к DHT сразу (параллельно с трекерами)
            Task? dhtTask = null;
            if (_dhtClient != null)
            {
                dhtTask = _dhtClient.FindPeersAsync(_infoHash);
                _lastDhtSearch = DateTime.Now;
            }
            
            // Первый LSD announce
            if (_lsd != null)
            {
                _ = _lsd.AnnounceAsync();
                _lastLsdAnnounce = DateTime.Now;
            }
            
            // Ждём завершения первых запросов
            await trackerTask;
            if (dhtTask != null) await dhtTask;
            _lastTrackerAnnounce = DateTime.Now;
            
            // Второй запрос через 5 секунд для получения большего числа пиров
            await Task.Delay(5000, cancellationToken);
            await AnnounceToTrackersAsync(cancellationToken);
            _lastTrackerAnnounce = DateTime.Now;

            // Периодически обновляем
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(TrackerAnnounceInterval), cancellationToken);
                    
                    // Обновляем трекеры
                    _ = AnnounceToTrackersAsync(cancellationToken); // Не ждём завершения
                    _lastTrackerAnnounce = DateTime.Now;
                    
                    // Обновляем DHT
                    if (_dhtClient != null && (DateTime.Now - _lastDhtSearch).TotalSeconds >= DhtSearchInterval)
                    {
                        Logger.LogInfo("[Discovery] Searching for peers via DHT");
                        _ = _dhtClient.FindPeersAsync(_infoHash); // Не ждём завершения
                        _lastDhtSearch = DateTime.Now;
                    }
                    
                    // Отправляем LSD announce
                    if (_lsd != null && (DateTime.Now - _lastLsdAnnounce).TotalSeconds >= LsdAnnounceInterval)
                    {
                        _ = _lsd.AnnounceAsync();
                        _lastLsdAnnounce = DateTime.Now;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("[Discovery] Error in discovery loop", ex);
                }
            }
        }

        private async Task AnnounceToTrackersAsync(CancellationToken cancellationToken)
        {
            // КРИТИЧНО: Защита от параллельных вызовов для предотвращения утечки памяти
            if (_isAnnouncing)
                return;
            
            _isAnnouncing = true;
            try
            {
                // Получаем актуальные данные о торренте
                var downloaded = _getDownloadedBytes?.Invoke() ?? 0;
                var uploaded = _getUploadedBytes?.Invoke() ?? 0;
                var left = _getLeftBytes?.Invoke() ?? 0;
                var eventType = _getEvent?.Invoke() ?? "started";
                
                var allAnnounceUrls = new List<string>(_announceUrls);
                
                // Добавляем публичные трекеры для максимального охвата
                var publicTrackers = new List<string>
            {
                // Популярные UDP трекеры
                "udp://tracker.opentrackr.org:1337/announce",
                "udp://open.stealth.si:80/announce",
                "udp://tracker.torrent.eu.org:451/announce",
                "udp://tracker.bittor.pw:1337/announce",
                "udp://public.popcorn-tracker.org:6969/announce",
                "udp://tracker.dler.org:6969/announce",
                "udp://exodus.desync.com:6969/announce",
                "udp://open.demonii.com:1337/announce",
                "udp://tracker.openbittorrent.com:6969/announce",
                "udp://tracker.moeking.me:6969/announce",
                "udp://explodie.org:6969/announce",
                "udp://tracker1.bt.moack.co.kr:80/announce",
                "udp://tracker.theoks.net:6969/announce",
                "udp://tracker-udp.gbitt.info:80/announce",
                "udp://retracker01-msk-virt.corbina.net:80/announce",
                "udp://opentracker.io:6969/announce",
                "udp://new-line.net:6969/announce",
                "udp://bt1.archive.org:6969/announce",
                "udp://bt2.archive.org:6969/announce",
                // HTTP трекеры
                "http://tracker.opentrackr.org:1337/announce",
                "http://tracker.openbittorrent.com:80/announce",
                "https://tracker.tamersunion.org:443/announce",
                "https://tracker.gbitt.info:443/announce",
                "http://tracker.gbitt.info:80/announce",
                "http://tracker1.bt.moack.co.kr:80/announce",
                "https://tracker.lilithraws.org:443/announce",
                "http://bt.endpot.com:80/announce"
            };
            
                // Всегда добавляем публичные трекеры для максимального охвата пиров
                foreach (var tracker in publicTrackers)
                {
                    if (!allAnnounceUrls.Contains(tracker))
                    {
                        allAnnounceUrls.Add(tracker);
                    }
                }

                if (allAnnounceUrls.Count == 0)
                    return;

                List<Task> tasks = [];
                var peersFound = 0;
                
                foreach (var announceUrl in allAnnounceUrls)
                {
                    if (string.IsNullOrWhiteSpace(announceUrl))
                        continue;

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var peers = await _trackerClient.AnnounceAsync(
                                announceUrl,
                                _infoHash,
                                _peerId,
                                downloaded,
                                uploaded,
                                left,
                                _port,
                                eventType
                            );

                            if (peers.Count > 0)
                            {
                                Interlocked.Add(ref peersFound, peers.Count);
                                foreach (var peer in peers)
                                {
                                    OnPeerDiscovered(peer);
                                }
                                Logger.LogInfo($"[Discovery] Got {peers.Count} peers from {announceUrl}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[Discovery] Error announcing to {announceUrl}: {ex.Message}");
                        }
                    }, cancellationToken);
                    
                    tasks.Add(task);
                }

                try
                {
                    // Увеличиваем таймаут для трекеров, чтобы получить больше пиров
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (TimeoutException)
                {
                    Logger.LogInfo($"[Discovery] Tracker announce timeout (30s), found {peersFound} peers so far");
                }
                
                _lastTrackerAnnounce = DateTime.Now;
            }
            finally
            {
                // КРИТИЧНО: Освобождаем флаг для предотвращения утечки памяти
                _isAnnouncing = false;
            }
        }

        internal void OnPeerDiscovered(IPEndPoint peer)
        {
            _semaphore.Wait();
            try
            {
                // Ограничение размера коллекции для предотвращения утечки памяти
                if (_discoveredPeers.Count >= MaxDiscoveredPeers)
                {
                    // Удаляем случайные старые записи, оставляя место для новых
                    var toRemove = _discoveredPeers.Take(_discoveredPeers.Count - MaxDiscoveredPeers + 1000).ToList();
                    foreach (var oldPeer in toRemove)
                    {
                        _discoveredPeers.Remove(oldPeer);
                    }
                }
                
                if (_discoveredPeers.Add(peer))
                {
                    Logger.LogInfo($"[Discovery] New peer discovered: {peer}");
                    // Захватываем ссылку в локальную переменную для предотвращения race condition
                    var callbacks = _callbacks;
                    if (callbacks != null)
                    {
                        var peerCopy = peer; // Захватываем копию для лямбды
                        SafeTaskRunner.RunSafe(async () => await callbacks.OnPeerDiscoveredAsync(peerCopy).ConfigureAwait(false));
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public List<IPEndPoint> GetDiscoveredPeers()
        {
            _semaphore.Wait();
            try
            {
                return _discoveredPeers.ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _discoveryTask?.Wait(TimeSpan.FromSeconds(1));
            // КРИТИЧНО: Обнуляем задачу для предотвращения утечки памяти
            _discoveryTask = null;
            
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
            
            if (_lsd != null)
            {
                _lsd.SetCallbacks(null!);
            }
            
            if (_dhtClient != null)
            {
                _dhtClient.SetCallbacks(null!);
            }
            
            // Очищаем callbacks
            _getDownloadedBytes = null;
            _getUploadedBytes = null;
            _getLeftBytes = null;
            _getEvent = null;
            
            _lsd?.Dispose();
            _dhtClient?.Dispose();
            _semaphore.Dispose();
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

