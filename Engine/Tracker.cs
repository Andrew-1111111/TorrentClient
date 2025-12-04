using System.Buffers.Binary;
using TorrentClient.Engine.Interfaces;
using TorrentClient.Protocol;
using TorrentClient.Utilities;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Клиент трекеров для HTTP и UDP трекеров
    /// Основан на BEP 3 (HTTP) и BEP 15 (UDP)
    /// </summary>
    public class Tracker(string announceUrl, byte[] infoHash, byte[] peerId, int port) : IDisposable
    {
        #region Статические поля

        private static readonly BencodeParser _bencodeParser = new();

        #endregion

        #region Поля

        private readonly string _announceUrl = announceUrl;
        private readonly byte[] _infoHash = infoHash;
        private readonly byte[] _peerId = peerId;
        private readonly int _port = port;

        #endregion

        #region Свойства

        /// <summary>URL анонса</summary>
        public string AnnounceUrl => _announceUrl;
        
        /// <summary>Интервал между анонсами (секунды)</summary>
        public int Interval { get; private set; } = 1800;
        
        /// <summary>Минимальный интервал</summary>
        public int MinInterval { get; private set; } = 60;
        
        /// <summary>Количество сидов</summary>
        public int Seeders { get; private set; }
        
        /// <summary>Количество личей</summary>
        public int Leechers { get; private set; }

        #endregion

        #region Асинхронные колбэки (замена событий)

        private ITrackerCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(ITrackerCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        #region Анонс

        /// <summary>Выполняет анонс на трекер</summary>
        public async Task<List<IPEndPoint>> AnnounceAsync(
            long downloaded,
            long uploaded,
            long left,
            string? eventType = null,
            int numWant = 200,
            CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(_announceUrl, UriKind.Absolute, out var uri))
            {
                Logger.LogWarning($"[Tracker] Неверный URL: {_announceUrl}");
                return [];
            }

            try
            {
                return uri.Scheme.ToLower() switch
                {
                    "http" or "https" => await AnnounceHttpAsync(uri, downloaded, uploaded, left, eventType, numWant, cancellationToken),
                    "udp" => await AnnounceUdpAsync(uri, downloaded, uploaded, left, eventType, numWant, cancellationToken),
                    _ => throw new NotSupportedException($"Неподдерживаемая схема трекера: {uri.Scheme}")
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Tracker] Ошибка анонса {_announceUrl}: {ex.Message}");
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(ex.Message).ConfigureAwait(false));
                }
                return [];
            }
        }

        #endregion

        #region HTTP анонс

        private async Task<List<IPEndPoint>> AnnounceHttpAsync(
            Uri uri,
            long downloaded,
            long uploaded,
            long left,
            string? eventType,
            int numWant,
            CancellationToken cancellationToken)
        {
            var queryParams = new List<string>
            {
                $"info_hash={UrlEncodeBytes(_infoHash)}",
                $"peer_id={UrlEncodeBytes(_peerId)}",
                $"port={_port}",
                $"uploaded={uploaded}",
                $"downloaded={downloaded}",
                $"left={left}",
                "compact=1",
                $"numwant={numWant}"
            };

            if (!string.IsNullOrEmpty(eventType))
            {
                queryParams.Add($"event={Uri.EscapeDataString(eventType)}");
            }

            var existingQuery = uri.Query.TrimStart('?');
            var newQuery = string.Join("&", queryParams);
            var fullQuery = string.IsNullOrEmpty(existingQuery) ? newQuery : $"{existingQuery}&{newQuery}";

            var requestUri = new UriBuilder(uri) { Query = fullQuery }.Uri;

            Logger.LogInfo($"[Tracker] HTTP анонс на {uri.Host}");

            var response = await HttpClientService.Instance.GetAsync(requestUri.ToString(), null, timeoutSeconds: 30, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"Tracker returned error status: {response.StatusCode} ({response.StatusMessage})";
                Logger.LogWarning($"[Tracker] {errorMessage}");
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(errorMessage).ConfigureAwait(false));
                }
                return [];
            }

            var responseBytes = response.Body;
            return ParseHttpResponse(responseBytes);
        }

        private List<IPEndPoint> ParseHttpResponse(byte[] data)
        {
            List<IPEndPoint> peers = [];

            try
            {
                var dict = _bencodeParser.Parse<BDictionary>(data);

                // Проверяем на ошибку
                if (dict.TryGetValue("failure reason", out var failure))
                {
                    var reason = failure is BString failureStr ? failureStr.ToString() : failure?.ToString() ?? "Неизвестно";
                    Logger.LogWarning($"[Tracker] Ошибка: {reason}");
                    // Захватываем ссылку в локальную переменную для предотвращения race condition
                    var errorCallbacks = _callbacks;
                    if (errorCallbacks != null)
                    {
                        SafeTaskRunner.RunSafe(async () => await errorCallbacks.OnErrorAsync(reason).ConfigureAwait(false));
                    }
                }

                // Парсим интервал
                if (dict.TryGetValue("interval", out var interval) && interval is BNumber intervalNum)
                {
                    Interval = (int)intervalNum.Value;
                }

                if (dict.TryGetValue("min interval", out var minInterval) && minInterval is BNumber minIntervalNum)
                {
                    MinInterval = (int)minIntervalNum.Value;
                }

                // Парсим сидов/личей
                if (dict.TryGetValue("complete", out var complete) && complete is BNumber completeNum)
                {
                    Seeders = (int)completeNum.Value;
                }

                if (dict.TryGetValue("incomplete", out var incomplete) && incomplete is BNumber incompleteNum)
                {
                    Leechers = (int)incompleteNum.Value;
                }

                // Парсим пиров
                if (dict.TryGetValue("peers", out var peersObj))
                {
                    if (peersObj is BString peersStr)
                    {
                        // Компактный формат
                        var peersBytes = peersStr.Value.ToArray();
                        peers = ParseCompactPeers(peersBytes);
                    }
                    else if (peersObj is BList peersList)
                    {
                        // Словарный формат
                        peers = ParseDictPeers(peersList);
                    }
                }

                // Парсим peers6 (IPv6)
                if (dict.TryGetValue("peers6", out var peers6Obj) && peers6Obj is BString peers6Str)
                {
                    var peers6Bytes = peers6Str.Value.ToArray();
                    peers.AddRange(ParseCompactPeers6(peers6Bytes));
                }

                Logger.LogInfo($"[Tracker] Получено {peers.Count} пиров (сиды: {Seeders}, личи: {Leechers})");
            }
            catch (Exception ex)
            {
                Logger.LogError("[Tracker] Ошибка парсинга ответа", ex);
            }

            // Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null && peers.Count > 0)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnPeersReceivedAsync(peers).ConfigureAwait(false));
            }
            return peers;
        }

        #endregion

        #region UDP анонс

        private async Task<List<IPEndPoint>> AnnounceUdpAsync(
            Uri uri,
            long downloaded,
            long uploaded,
            long left,
            string? eventType,
            int numWant,
            CancellationToken cancellationToken)
        {
            List<IPEndPoint> peers = [];

            // Разрешаем имя хоста
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();

            if (address == null)
            {
                Logger.LogWarning($"[Tracker] Не удалось разрешить {uri.Host}");
                return peers;
            }

            var port = uri.Port > 0 ? uri.Port : 80;
            var endpoint = new IPEndPoint(address, port);

            Logger.LogInfo($"[Tracker] UDP анонс на {uri.Host}:{port}");

            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 8000;

            // Шаг 1: Подключение
            var connectionId = await UdpConnectAsync(udp, endpoint, cancellationToken);
            if (connectionId == 0)
            {
                Logger.LogWarning($"[Tracker] UDP подключение не удалось к {uri.Host}");
                return peers;
            }

            // Шаг 2: Анонс
            peers = await UdpAnnounceAsync(udp, endpoint, connectionId, downloaded, uploaded, left, eventType, numWant, cancellationToken);

            // Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null && peers.Count > 0)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnPeersReceivedAsync(peers).ConfigureAwait(false));
            }
            return peers;
        }

        private static async Task<long> UdpConnectAsync(UdpClient udp, IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            var transactionId = Random.Shared.Next();
            var request = new byte[16];

            // Connection ID (магическое число)
            BinaryPrimitives.WriteInt64BigEndian(request.AsSpan(0), 0x41727101980L);
            // Действие: connect (0)
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(8), 0);
            // Transaction ID
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(12), transactionId);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await udp.SendAsync(request, endpoint, cancellationToken);

                    var result = await TaskTimeoutHelper.TimeoutAsync(
                        udp.ReceiveAsync(cancellationToken),
                        TimeSpan.FromSeconds(5));

                    if (result.Buffer.Length < 16) continue;

                    var responseAction = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(0));
                    var responseTransId = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(4));

                    if (responseAction != 0 || responseTransId != transactionId) continue;

                    return BinaryPrimitives.ReadInt64BigEndian(result.Buffer.AsSpan(8));
                }
                catch (TimeoutException)
                {
                    // Таймаут, повтор
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Отмена, повтор
                }
            }

            return 0;
        }

        private async Task<List<IPEndPoint>> UdpAnnounceAsync(
            UdpClient udp,
            IPEndPoint endpoint,
            long connectionId,
            long downloaded,
            long uploaded,
            long left,
            string? eventType,
            int numWant,
            CancellationToken cancellationToken)
        {
            List<IPEndPoint> peers = [];
            var transactionId = Random.Shared.Next();
            var request = new byte[98];

            // Connection ID
            BinaryPrimitives.WriteInt64BigEndian(request.AsSpan(0), connectionId);
            // Действие: announce (1)
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(8), 1);
            // Transaction ID
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(12), transactionId);
            // Хэш info
            Array.Copy(_infoHash, 0, request, 16, 20);
            // Peer ID
            Array.Copy(_peerId, 0, request, 36, 20);
            // Downloaded
            BinaryPrimitives.WriteInt64BigEndian(request.AsSpan(56), downloaded);
            // Left
            BinaryPrimitives.WriteInt64BigEndian(request.AsSpan(64), left);
            // Uploaded
            BinaryPrimitives.WriteInt64BigEndian(request.AsSpan(72), uploaded);
            // Event
            int eventValue = eventType switch
            {
                "started" => 2,
                "completed" => 1,
                "stopped" => 3,
                _ => 0
            };
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(80), eventValue);
            // IP (0 = использовать адрес отправителя)
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(84), 0);
            // Key
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(88), Random.Shared.Next());
            // Количество запрашиваемых пиров
            BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(92), numWant);
            // Port
            BinaryPrimitives.WriteInt16BigEndian(request.AsSpan(96), (short)_port);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await udp.SendAsync(request, endpoint, cancellationToken);

                    var result = await TaskTimeoutHelper.TimeoutAsync(
                        udp.ReceiveAsync(cancellationToken),
                        TimeSpan.FromSeconds(5));

                    if (result.Buffer.Length < 20) continue;

                    var responseAction = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(0));
                    var responseTransId = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(4));

                    if (responseAction == 3) // Ошибка
                    {
                        var errorMsg = Encoding.UTF8.GetString(result.Buffer, 8, result.Buffer.Length - 8);
                        Logger.LogWarning($"[Tracker] UDP ошибка: {errorMsg}");
                        // Захватываем ссылку в локальную переменную для предотвращения race condition
                        var callbacks = _callbacks;
                        if (callbacks != null)
                        {
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(errorMsg).ConfigureAwait(false));
                        }
                        return peers;
                    }

                    if (responseAction != 1 || responseTransId != transactionId) continue;

                    Interval = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(8));
                    Leechers = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(12));
                    Seeders = BinaryPrimitives.ReadInt32BigEndian(result.Buffer.AsSpan(16));

                    // Парсим пиров (6 байт каждый: 4 IP + 2 порт)
                    for (int i = 20; i + 6 <= result.Buffer.Length; i += 6)
                    {
                        var ip = new IPAddress(result.Buffer.AsSpan(i, 4));
                        var peerPort = BinaryPrimitives.ReadUInt16BigEndian(result.Buffer.AsSpan(i + 4));
                        if (peerPort > 0)
                        {
                            peers.Add(new IPEndPoint(ip, peerPort));
                        }
                    }

                    Logger.LogInfo($"[Tracker] UDP получено {peers.Count} пиров (сиды: {Seeders}, личи: {Leechers})");
                    return peers;
                }
                catch (TimeoutException)
                {
                    // Таймаут, повтор
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Отмена, повтор
                }
            }

            return peers;
        }

        #endregion

        #region Парсинг пиров

        private static List<IPEndPoint> ParseCompactPeers(byte[] data)
        {
            List<IPEndPoint> peers = [];
            for (int i = 0; i + 6 <= data.Length; i += 6)
            {
                var ip = new IPAddress(data.AsSpan(i, 4));
                var port = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i + 4));
                if (port > 0)
                {
                    peers.Add(new IPEndPoint(ip, port));
                }
            }
            return peers;
        }

        private static List<IPEndPoint> ParseCompactPeers6(byte[] data)
        {
            List<IPEndPoint> peers = [];
            for (int i = 0; i + 18 <= data.Length; i += 18)
            {
                var ip = new IPAddress(data.AsSpan(i, 16));
                var port = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i + 16));
                if (port > 0)
                {
                    peers.Add(new IPEndPoint(ip, port));
                }
            }
            return peers;
        }

        private static List<IPEndPoint> ParseDictPeers(BList peersList)
        {
            List<IPEndPoint> peers = [];
            foreach (var peerObj in peersList)
            {
                if (peerObj is BDictionary peerDict)
                {
                    string? ip = null;
                    int port = 0;

                    if (peerDict.TryGetValue("ip", out var ipObj))
                    {
                        ip = ipObj is BString ipStr ? ipStr.ToString() : ipObj?.ToString();
                    }

                    if (peerDict.TryGetValue("port", out var portObj) && portObj is BNumber portNum)
                    {
                        port = (int)portNum.Value;
                    }

                    if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var ipAddr) && port > 0)
                    {
                        peers.Add(new IPEndPoint(ipAddr, port));
                    }
                }
            }
            return peers;
        }

        #endregion

        #region Вспомогательные методы

        private static string UrlEncodeBytes(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                if ((b >= '0' && b <= '9') || (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') ||
                    b == '-' || b == '_' || b == '.' || b == '~')
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append($"%{b:X2}");
                }
            }
            return sb.ToString();
        }

        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
            GC.SuppressFinalize(this);
        }
        
        #endregion
    }

    /// <summary>
    /// Управляет несколькими трекерами для торрента
    /// </summary>
    public class TrackerManager : IDisposable
    {
        #region Поля

        private readonly List<Tracker> _trackers = [];
        private readonly byte[] _infoHash;
        private readonly byte[] _peerId;
        private readonly int _port;
        private readonly HashSet<string> _seenPeers = [];
        private readonly SemaphoreSlim _seenPeersLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _announceTask;
        
        /// <summary>Максимальное количество известных пиров (ограничение памяти)</summary>
        private const int MaxSeenPeers = 5000; // Уменьшено для более агрессивной очистки
        
        // Колбэки для трекеров (не нужно хранить, так как используем SetCallbacks)

        #endregion

        // События заменены на асинхронные колбэки через ITrackerCallbacks
        private ITrackerCallbacks? _callbacks;
        
        /// <summary>Устанавливает колбэки для замены событий</summary>
        internal void SetCallbacks(ITrackerCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #region Конструктор

        /// <summary>Публичные трекеры для максимального охвата пиров</summary>
        private static readonly string[] PublicTrackers =
        [
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://open.stealth.si:80/announce",
            "udp://tracker.torrent.eu.org:451/announce",
            "udp://exodus.desync.com:6969/announce",
            "udp://tracker.openbittorrent.com:6969/announce",
            "udp://open.demonii.com:1337/announce",
            "udp://tracker.moeking.me:6969/announce",
            "udp://explodie.org:6969/announce",
            "udp://tracker1.bt.moack.co.kr:80/announce",
            "udp://tracker.theoks.net:6969/announce",
            "udp://tracker-udp.gbitt.info:80/announce",
            "udp://opentracker.io:6969/announce",
            "udp://bt1.archive.org:6969/announce",
            "udp://bt2.archive.org:6969/announce",
            "http://tracker.opentrackr.org:1337/announce",
            "http://tracker.openbittorrent.com:80/announce",
            "https://tracker.gbitt.info:443/announce"
        ];
        
        public TrackerManager(byte[] infoHash, byte[] peerId, int port, IEnumerable<string> trackerUrls)
        {
            _infoHash = infoHash;
            _peerId = peerId;
            _port = port;

            // Добавляем трекеры из торрента
            var allUrls = new HashSet<string>(trackerUrls, StringComparer.OrdinalIgnoreCase);
            
            // Добавляем публичные трекеры для максимального охвата
            foreach (var publicTracker in PublicTrackers)
            {
                allUrls.Add(publicTracker);
            }

            foreach (var url in allUrls)
            {
                var tracker = new Tracker(url, infoHash, peerId, port);
                // Устанавливаем колбэки для трекера
                var trackerCallbacks = new TrackerCallbacksWrapper(this);
                tracker.SetCallbacks(trackerCallbacks);
                _trackers.Add(tracker);
            }

            Logger.LogInfo($"[TrackerManager] Инициализирован с {_trackers.Count} трекерами (включая публичные)");
        }

        #endregion

        #region Поля дополнительные
        
        private bool _disposed;
        
        #endregion
        
        #region Обработка пиров

        internal async Task OnPeersReceivedAsync(List<IPEndPoint> peers)
        {
            // Защита от вызова после Dispose
            if (_disposed) return;
            
            foreach (var peer in peers)
            {
                if (_disposed) return;
                
                var key = peer.ToString();
                
                try
                {
                    await _seenPeersLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (_disposed) return;
                        
                        // Ограничение размера коллекции для предотвращения утечки памяти
                        if (_seenPeers.Count >= MaxSeenPeers)
                        {
                            // Удаляем случайные старые записи, оставляя место для новых
                            var toRemove = _seenPeers.Take(_seenPeers.Count - MaxSeenPeers + 1000).ToList();
                            foreach (var oldKey in toRemove)
                            {
                                _seenPeers.Remove(oldKey);
                            }
                        }
                        
                        if (_seenPeers.Add(key))
                        {
                            // Захватываем ссылку в локальную переменную для предотвращения race condition
                            var callbacks = _callbacks;
                            if (callbacks != null)
                            {
                                SafeTaskRunner.RunSafe(async () => await callbacks.OnPeersReceivedAsync([peer]).ConfigureAwait(false));
                            }
                        }
                    }
                    finally
                    {
                        _seenPeersLock.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        #endregion

        #region Управление

        /// <summary>Запускает периодические анонсы</summary>
        public void Start(long left)
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            _announceTask = AnnounceLoopAsync(left, _cts.Token);
        }

        /// <summary>Останавливает анонсы</summary>
        public void Stop()
        {
            _cts?.Cancel();
            
            // Ожидаем завершения задачи анонсов
            if (_announceTask != null)
            {
                try
                {
                    _announceTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }
                _announceTask = null;
            }
            
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>Очищает список известных пиров для повторного обнаружения</summary>
        public void ClearSeenPeers()
        {
            _seenPeersLock.Wait();
            try
            {
                _seenPeers.Clear();
                Logger.LogInfo("[TrackerManager] Список известных пиров очищен");
            }
            finally
            {
                _seenPeersLock.Release();
            }
        }

        private async Task AnnounceLoopAsync(long left, CancellationToken cancellationToken)
        {
            // Начальный анонс на все трекеры
            await AnnounceToAllAsync(0, 0, left, "started", cancellationToken);

            // Второй анонс через 5 секунд для получения большего числа пиров (агрессивный старт)
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                await AnnounceToAllAsync(0, 0, left, null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            // Третий анонс через 10 секунд
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                await AnnounceToAllAsync(0, 0, left, null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Периодические анонсы каждые 15 секунд
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                    await AnnounceToAllAsync(0, 0, left, null, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>Анонс на все трекеры</summary>
        public async Task AnnounceToAllAsync(long downloaded, long uploaded, long left, string? eventType, CancellationToken cancellationToken = default)
        {
            // numWant = 500 - запрашиваем максимум пиров
            var tasks = _trackers.Select(t => t.AnnounceAsync(downloaded, uploaded, left, eventType, 500, cancellationToken));
            await Task.WhenAll(tasks);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            Stop();
            
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
            
            // Очищаем колбэки трекеров
            foreach (var tracker in _trackers)
            {
                tracker.SetCallbacks(null!);
                tracker.Dispose();
            }
            _trackers.Clear();
            
            _cts?.Dispose();
            _seenPeersLock.Dispose();
            
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
