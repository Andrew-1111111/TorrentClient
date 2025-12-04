using System.Security.Cryptography;
using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// DHT Node - узел в Distributed Hash Table
    /// Реализация BEP5: http://www.bittorrent.org/beps/bep_0005.html
    /// </summary>
    public class DhtNode
    {
        public byte[] NodeId { get; set; }
        public IPEndPoint EndPoint { get; set; }

        public DhtNode(byte[] nodeId, IPEndPoint endPoint)
        {
            NodeId = nodeId;
            EndPoint = endPoint;
        }

        public static DhtNode FromCompact(byte[] compact)
        {
            if (compact.Length < 26) // 20 bytes node ID + 4 bytes IP + 2 bytes port
                throw new ArgumentException("Invalid compact format");

            var nodeId = new byte[20];
            Array.Copy(compact, 0, nodeId, 0, 20);

            var ipBytes = new byte[4];
            Array.Copy(compact, 20, ipBytes, 0, 4);
            var ip = new IPAddress(ipBytes);

            var portBytes = new byte[2];
            Array.Copy(compact, 22, portBytes, 0, 2);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(portBytes);
            var port = BitConverter.ToUInt16(portBytes, 0);

            return new DhtNode(nodeId, new IPEndPoint(ip, port));
        }

        public byte[] ToCompact()
        {
            var result = new byte[26];
            Array.Copy(NodeId, 0, result, 0, 20);
            var ipBytes = EndPoint.Address.GetAddressBytes();
            Array.Copy(ipBytes, 0, result, 20, 4);
            var portBytes = BitConverter.GetBytes((ushort)EndPoint.Port);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(portBytes);
            Array.Copy(portBytes, 0, result, 24, 2);
            return result;
        }
    }

    /// <summary>
    /// DHT Client - клиент для работы с Distributed Hash Table
    /// </summary>
    public class DhtClient : IDisposable
    {
        private UdpClient? _udpClient;
        private readonly byte[] _nodeId;
        private readonly int _dhtPort;
        private readonly Dictionary<string, DateTime> _pendingQueries = new();
        private readonly List<DhtNode> _bootstrapNodes = new();
        private readonly List<DhtNode> _knownNodes = new();
        private Task? _listenerTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Random _random = new();
        private int _transactionId = 0;
        
        /// <summary>Максимальное количество известных DHT узлов (ограничение памяти)</summary>
        private const int MaxKnownNodes = 2000;
        
        /// <summary>Максимальное количество ожидающих запросов (ограничение памяти)</summary>
        private const int MaxPendingQueries = 500;
        
        /// <summary>Время жизни ожидающего запроса (секунды)</summary>
        private const int PendingQueryTimeoutSeconds = 30;

        #region Асинхронные колбэки (замена событий)

        private IDhtCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(IDhtCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        public DhtClient(int dhtPort)
        {
            _dhtPort = dhtPort;
            _nodeId = GenerateNodeId();
            InitializeBootstrapNodes();
        }

        private byte[] GenerateNodeId()
        {
            // Генерируем случайный 20-байтовый Node ID
            var nodeId = new byte[20];
            _random.NextBytes(nodeId);
            return nodeId;
        }

        /// <summary>DHT Bootstrap узлы</summary>
        private static readonly (string Host, int Port)[] BootstrapHosts =
        [
            ("router.bittorrent.com", 6881),
            ("router.utorrent.com", 6881),
            ("dht.libtorrent.org", 25401),
            ("dht.transmissionbt.com", 6881),
            ("router.bitcomet.com", 6881),
            ("dht.aelitis.com", 6881),
        ];
        
        private void InitializeBootstrapNodes()
        {
            // Добавляем все bootstrap узлы параллельно
            var tasks = BootstrapHosts.Select(async host =>
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(host.Host);
                    var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
                    if (ip != null)
                    {
                        lock (_bootstrapNodes)
                        {
                            _bootstrapNodes.Add(new DhtNode(new byte[20], new IPEndPoint(ip, host.Port)));
                        }
                        Logger.LogInfo($"[DHT] Added bootstrap node: {host.Host} ({ip}:{host.Port})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[DHT] Failed to resolve {host.Host}: {ex.Message}");
                }
            }).ToArray();
            
            // Ждём завершения с таймаутом
            try
            {
                Task.WaitAll(tasks, TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { }
            
            Logger.LogInfo($"[DHT] Initialized {_bootstrapNodes.Count} bootstrap nodes");
        }

        public void Start()
        {
            try
            {
                _udpClient = new UdpClient(_dhtPort);
                Logger.LogInfo($"[DHT] Started DHT client on port {_dhtPort}");

                _listenerTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
                
                // Подключаемся к bootstrap узлам
                SafeTaskRunner.RunSafe(async () =>
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token); // Ждем немного перед подключением
                    Logger.LogInfo("[DHT] Starting bootstrap process");
                    await BootstrapAsync();
                    Logger.LogInfo($"[DHT] Bootstrap completed. Known nodes: {_knownNodes.Count}");
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("[DHT] Error starting DHT client", ex);
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            if (_udpClient == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        HandleDhtMessage(result.Buffer, result.RemoteEndPoint);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[DHT] Error receiving message", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[DHT] Error in listener loop", ex);
            }
        }

        private void HandleDhtMessage(byte[] data, IPEndPoint remoteEndPoint)
        {
            try
            {
                // DHT сообщения - это bencoded словари
                var message = BencodeDecode(data) as Dictionary<string, object>;
                if (message == null)
                {
                    Logger.LogWarning($"[DHT] Failed to decode message from {remoteEndPoint}");
                    return;
                }

                // Обрабатываем ответы на наши запросы
                if (message.TryGetValue("y", out var y))
                {
                    string messageTypeStr;
                    if (y is byte[] yBytes)
                    {
                        messageTypeStr = Encoding.ASCII.GetString(yBytes);
                    }
                    else if (y is string yStr)
                    {
                        messageTypeStr = yStr;
                    }
                    else
                    {
                        messageTypeStr = y?.ToString() ?? "";
                    }
                
                if (messageTypeStr == "r") // response
                {
                    HandleResponse(message, remoteEndPoint);
                }
                else if (messageTypeStr == "q") // query
                {
                    Logger.LogInfo($"[DHT] Received query from {remoteEndPoint}");
                    HandleQuery(message, remoteEndPoint);
                }
                else if (messageTypeStr == "e") // error
                {
                    Logger.LogWarning($"[DHT] Received error from {remoteEndPoint}");
                }
                else
                {
                    Logger.LogWarning($"[DHT] Unknown message type '{messageTypeStr}' from {remoteEndPoint}");
                }
                }
                else
                {
                    Logger.LogWarning($"[DHT] Message from {remoteEndPoint} has no 'y' field");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DHT] Error handling DHT message from {remoteEndPoint}", ex);
            }
        }

        private void HandleResponse(Dictionary<string, object> message, IPEndPoint remoteEndPoint)
        {
            if (message.TryGetValue("r", out var r) && r is Dictionary<string, object> response)
            {
                Logger.LogInfo($"[DHT] Received response from {remoteEndPoint}");
                
                // Обрабатываем get_peers ответ
                if (response.TryGetValue("values", out var values))
                {
                    List<IPEndPoint> peers = [];
                    
                    if (values is List<object> peerList)
                    {
                        foreach (var peerObj in peerList)
                        {
                            if (peerObj is byte[] peerBytes && peerBytes.Length == 6)
                            {
                                var ipBytes = new byte[4];
                                Array.Copy(peerBytes, 0, ipBytes, 0, 4);
                                var ip = new IPAddress(ipBytes);
                                
                                var portBytes = new byte[2];
                                Array.Copy(peerBytes, 4, portBytes, 0, 2);
                                if (BitConverter.IsLittleEndian)
                                    Array.Reverse(portBytes);
                                var port = BitConverter.ToUInt16(portBytes, 0);
                                
                                peers.Add(new IPEndPoint(ip, port));
                            }
                        }
                    }
                    else if (values is byte[] valuesBytes)
                    {
                        // Compact format: 6 байт на пира
                        for (int i = 0; i < valuesBytes.Length; i += 6)
                        {
                            if (i + 6 > valuesBytes.Length)
                                break;
                            
                            var ipBytes = new byte[4];
                            Array.Copy(valuesBytes, i, ipBytes, 0, 4);
                            var ip = new IPAddress(ipBytes);
                            
                            var portBytes = new byte[2];
                            Array.Copy(valuesBytes, i + 4, portBytes, 0, 2);
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(portBytes);
                            var port = BitConverter.ToUInt16(portBytes, 0);
                            
                            peers.Add(new IPEndPoint(ip, port));
                        }
                    }
                    
                    if (peers.Count > 0)
                    {
                        Logger.LogInfo($"[DHT] Found {peers.Count} peers via DHT from {remoteEndPoint}");
                        foreach (var peer in peers)
                        {
                            Logger.LogInfo($"[DHT]   - Peer: {peer}");
                        }
                        if (_callbacks != null)
                        {
                            var peersCopy = peers; // Захватываем копию для лямбды
                            SafeTaskRunner.RunSafe(async () => await _callbacks.OnPeersFoundAsync(peersCopy).ConfigureAwait(false));
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"[DHT] No peers in response from {remoteEndPoint} (values field present but empty)");
                    }
                }
                else
                {
                    Logger.LogInfo($"[DHT] Response from {remoteEndPoint} has no 'values' field (may have 'nodes' instead)");
                }

                // Обрабатываем nodes (для find_node ответов)
                if (response.TryGetValue("nodes", out var nodes))
                {
                    byte[]? nodesBytes = null;
                    if (nodes is byte[] bytes)
                    {
                        nodesBytes = bytes;
                    }
                    else if (nodes is string nodesStr)
                    {
                        nodesBytes = Encoding.UTF8.GetBytes(nodesStr);
                    }
                    
                    if (nodesBytes != null)
                    {
                        var nodesAdded = 0;
                        for (int i = 0; i < nodesBytes.Length; i += 26)
                        {
                            if (i + 26 > nodesBytes.Length)
                                break;
                            
                            var nodeBytes = new byte[26];
                            Array.Copy(nodesBytes, i, nodeBytes, 0, 26);
                            try
                            {
                                var node = DhtNode.FromCompact(nodeBytes);
                                // Фильтруем некорректные узлы (0.0.0.0:0, multicast, loopback)
                                if (node.EndPoint.Address.Equals(IPAddress.Any) || 
                                    node.EndPoint.Address.Equals(IPAddress.IPv6Any) ||
                                    node.EndPoint.Address.Equals(IPAddress.Loopback) ||
                                    node.EndPoint.Address.Equals(IPAddress.IPv6Loopback) ||
                                    node.EndPoint.Port == 0)
                                {
                                    continue; // Пропускаем некорректные узлы
                                }
                                
                                if (!_knownNodes.Any(n => n.EndPoint.Equals(node.EndPoint)))
                                {
                                    if (_knownNodes.Count >= MaxKnownNodes)
                                    {
                                        // Удаляем старые узлы, оставляя место для новых
                                        _knownNodes.RemoveAt(0);
                                    }
                                    _knownNodes.Add(node);
                                    nodesAdded++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"[DHT] Error parsing node from compact format: {ex.Message}");
                            }
                        }
                        if (nodesAdded > 0)
                        {
                            Logger.LogInfo($"[DHT] Added {nodesAdded} new nodes from {remoteEndPoint} (total known nodes: {_knownNodes.Count})");
                        }
                    }
                }
            }
            else
            {
                Logger.LogWarning($"[DHT] Response from {remoteEndPoint} has no 'r' field");
            }
        }

        private void HandleQuery(Dictionary<string, object> message, IPEndPoint remoteEndPoint)
        {
            // Обрабатываем входящие запросы от других узлов
            // Пока просто игнорируем, можно добавить поддержку позже
        }

        public async Task FindPeersAsync(string infoHash)
        {
            try
            {
                var infoHashBytes = Convert.FromHexString(infoHash);
                
                // Отправляем get_peers запросы к известным узлам
                var nodesToQuery = _knownNodes.Count > 0 ? _knownNodes : _bootstrapNodes;
                
                if (nodesToQuery.Count == 0)
                {
                    Logger.LogWarning("[DHT] No nodes available for peer search, waiting for bootstrap");
                    return;
                }
                
                Logger.LogInfo($"[DHT] Searching for peers via {nodesToQuery.Count} nodes (info hash: {infoHash})");
                
                // Запрашиваем у большего количества узлов (до 100 узлов для активного поиска)
                // Согласно документации, нужно запрашивать у многих узлов параллельно
                var nodesToQueryList = nodesToQuery.Take(100).ToList();
                List<Task> tasks = [];
                
                foreach (var node in nodesToQueryList)
                {
                    var nodeCopy = node; // Захватываем копию для лямбды
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await SendGetPeersQueryAsync(nodeCopy, infoHashBytes);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[DHT] Error sending get_peers to {nodeCopy.EndPoint}: {ex.Message}");
                        }
                    });
                    tasks.Add(task);
                    
                    // Минимальная задержка между запросами для более быстрого поиска
                    await Task.Delay(10);
                }
                
                // Ждем завершения всех запросов с таймаутом
                try
                {
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    Logger.LogInfo("[DHT] Some get_peers queries timed out, but continuing...");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DHT] Error finding peers for info hash: {infoHash}", ex);
            }
        }

        private async Task SendGetPeersQueryAsync(DhtNode node, byte[] infoHash)
        {
            try
            {
                if (_udpClient == null)
                    return;

                var transactionId = GetNextTransactionIdBytes();
                var query = new Dictionary<string, object>
                {
                    { "t", transactionId },
                    { "y", Encoding.ASCII.GetBytes("q") },
                    { "q", Encoding.ASCII.GetBytes("get_peers") },
                    { "a", new Dictionary<string, object>
                        {
                            { "id", _nodeId },
                            { "info_hash", infoHash }
                        }
                    }
                };

                var data = BencodeEncode(query);
                await _udpClient.SendAsync(data, data.Length, node.EndPoint);
                
                var transactionIdStr = BitConverter.ToString(transactionId).Replace("-", "");
                
                if (_pendingQueries.Count >= MaxPendingQueries)
                {
                    // Удаляем старые запросы (старше таймаута)
                    var now = DateTime.Now;
                    var expiredKeys = _pendingQueries
                        .Where(kv => (now - kv.Value).TotalSeconds > PendingQueryTimeoutSeconds)
                        .Select(kv => kv.Key)
                        .ToList();
                    foreach (var key in expiredKeys)
                    {
                        _pendingQueries.Remove(key);
                    }
                    
                    // Если всё ещё переполнено, удаляем самые старые
                    if (_pendingQueries.Count >= MaxPendingQueries)
                    {
                        var oldestKeys = _pendingQueries
                            .OrderBy(kv => kv.Value)
                            .Take(_pendingQueries.Count - MaxPendingQueries + 100)
                            .Select(kv => kv.Key)
                            .ToList();
                        foreach (var key in oldestKeys)
                        {
                            _pendingQueries.Remove(key);
                        }
                    }
                }
                
                _pendingQueries[transactionIdStr] = DateTime.Now;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable || 
                                             ex.SocketErrorCode == SocketError.HostUnreachable)
            {
                // Игнорируем ошибки с некорректными адресами (0.0.0.0 и т.д.)
                Logger.LogWarning($"[DHT] Cannot send to {node.EndPoint}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DHT] Error sending get_peers query to {node.EndPoint}", ex);
            }
        }

        private async Task BootstrapAsync()
        {
            try
            {
                Logger.LogInfo($"[DHT] Bootstrapping with {_bootstrapNodes.Count} bootstrap nodes");
                
                // Отправляем find_node запросы к bootstrap узлам
                foreach (var node in _bootstrapNodes)
                {
                    Logger.LogInfo($"[DHT] Sending find_node to bootstrap node: {node.EndPoint}");
                    await SendFindNodeQueryAsync(node, _nodeId);
                    await Task.Delay(200);
                }
                
                // Ждем ответы (до 5 секунд)
                await Task.Delay(5000);
                Logger.LogInfo($"[DHT] After bootstrap: {_knownNodes.Count} known nodes");
            }
            catch (Exception ex)
            {
                Logger.LogError("[DHT] Error bootstrapping DHT", ex);
            }
        }

        private async Task SendFindNodeQueryAsync(DhtNode node, byte[] targetNodeId)
        {
            try
            {
                if (_udpClient == null)
                    return;

                var transactionId = GetNextTransactionIdBytes();
                var query = new Dictionary<string, object>
                {
                    { "t", transactionId },
                    { "y", Encoding.ASCII.GetBytes("q") },
                    { "q", Encoding.ASCII.GetBytes("find_node") },
                    { "a", new Dictionary<string, object>
                        {
                            { "id", _nodeId },
                            { "target", targetNodeId }
                        }
                    }
                };

                var data = BencodeEncode(query);
                await _udpClient.SendAsync(data, data.Length, node.EndPoint);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DHT] Error sending find_node query to {node.EndPoint}", ex);
            }
        }

        private string GetNextTransactionId()
        {
            _transactionId = (_transactionId + 1) % 65536;
            return _transactionId.ToString();
        }
        
        private byte[] GetNextTransactionIdBytes()
        {
            _transactionId = (_transactionId + 1) % 65536;
            // Transaction ID должен быть 2 байта (big-endian)
            var bytes = BitConverter.GetBytes((ushort)_transactionId);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        // Простая реализация bencode encoding
        private static byte[] BencodeEncode(Dictionary<string, object> dict)
        {
            List<byte> result = [];
            result.Add((byte)'d');

            foreach (var kvp in dict)
            {
                // Key
                var keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                result.AddRange(Encoding.UTF8.GetBytes(keyBytes.Length.ToString()));
                result.Add((byte)':');
                result.AddRange(keyBytes);

                // Value
                if (kvp.Value is byte[] bytes)
                {
                    result.AddRange(Encoding.UTF8.GetBytes(bytes.Length.ToString()));
                    result.Add((byte)':');
                    result.AddRange(bytes);
                }
                else if (kvp.Value is string str)
                {
                    var strBytes = Encoding.UTF8.GetBytes(str);
                    result.AddRange(Encoding.UTF8.GetBytes(strBytes.Length.ToString()));
                    result.Add((byte)':');
                    result.AddRange(strBytes);
                }
                else if (kvp.Value is Dictionary<string, object> subDict)
                {
                    result.AddRange(BencodeEncode(subDict));
                }
            }

            result.Add((byte)'e');
            return result.ToArray();
        }

        // Простая реализация bencode decoding
        private static object? BencodeDecode(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            int pos = 0;
            return DecodeValue(data, ref pos);
        }

        private static object? DecodeValue(byte[] data, ref int pos)
        {
            if (pos >= data.Length)
                return null;

            if (data[pos] == 'd')
            {
                pos++;
                Dictionary<string, object> dict = [];
                while (pos < data.Length && data[pos] != 'e')
                {
                    var keyBytes = DecodeString(data, ref pos);
                    if (keyBytes == null) break;
                    var key = Encoding.UTF8.GetString(keyBytes);
                    var value = DecodeValue(data, ref pos);
                    if (value != null)
                        dict[key] = value;
                }
                if (pos < data.Length && data[pos] == 'e')
                    pos++;
                return dict;
            }
            else if (data[pos] == 'l')
            {
                pos++;
                List<object> list = [];
                while (pos < data.Length && data[pos] != 'e')
                {
                    var value = DecodeValue(data, ref pos);
                    if (value != null)
                        list.Add(value);
                }
                if (pos < data.Length && data[pos] == 'e')
                    pos++;
                return list;
            }
            else if (data[pos] >= '0' && data[pos] <= '9')
            {
                // Возвращаем исходные байты, чтобы не потерять бинарные данные (info_hash, peers и т.п.)
                return DecodeString(data, ref pos);
            }
            else if (data[pos] == 'i')
            {
                pos++;
                var numStr = new StringBuilder();
                while (pos < data.Length && data[pos] != 'e')
                {
                    numStr.Append((char)data[pos]);
                    pos++;
                }
                if (pos < data.Length && data[pos] == 'e')
                    pos++;
                if (long.TryParse(numStr.ToString(), out var num))
                    return num;
                return null;
            }

            return null;
        }

        private static byte[]? DecodeString(byte[] data, ref int pos)
        {
            var lengthStr = new StringBuilder();
            while (pos < data.Length && data[pos] >= '0' && data[pos] <= '9')
            {
                lengthStr.Append((char)data[pos]);
                pos++;
            }
            if (pos >= data.Length || data[pos] != ':')
                return null;
            pos++;

            if (!int.TryParse(lengthStr.ToString(), out var length))
                return null;

            if (pos + length > data.Length)
                return null;

            var bytes = new byte[length];
            Array.Copy(data, pos, bytes, 0, length);
            pos += length;
            return bytes;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
            _listenerTask = null;
            
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
            
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch { }
            
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}


