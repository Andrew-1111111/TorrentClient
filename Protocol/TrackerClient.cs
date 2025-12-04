namespace TorrentClient.Protocol
{
    public class TrackerClient
    {
        private static readonly BencodeParser _bencodeParser = new();

        private readonly Dictionary<string, string> _trackerCookies;
        private readonly Dictionary<string, Dictionary<string, string>> _trackerHeaders;

        public TrackerClient(TrackerClientOptions? options = null)
        {
            var opts = options ?? new TrackerClientOptions();
            _trackerCookies = opts.TrackerCookies;
            _trackerHeaders = opts.TrackerHeaders;
        }

        public async Task<List<IPEndPoint>> AnnounceAsync(string announceUrl, string infoHash, string peerId, 
            long downloaded, long uploaded, long left, int port, string? eventType = null, int? numwant = null)
        {
            // Проверка валидности URL
            if (string.IsNullOrWhiteSpace(announceUrl))
            {
                Logger.LogWarning("Announce URL is empty or null");
                return [];
            }

            if (!Uri.TryCreate(announceUrl, UriKind.Absolute, out var uri))
            {
                Logger.LogError($"Invalid announce URL format: {announceUrl}");
                return [];
            }

            // Поддержка UDP трекеров
            if (uri.Scheme == "udp")
            {
                return await AnnounceUdpAsync(uri, infoHash, peerId, downloaded, uploaded, left, port, eventType, numwant ?? 200);
            }

            // Поддержка TCP трекеров
            if (uri.Scheme == "tcp")
            {
                return await AnnounceTcpAsync(uri, infoHash, peerId, downloaded, uploaded, left, port, eventType, numwant ?? 200);
            }

            // HTTP/HTTPS трекеры
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                Logger.LogError($"Unsupported announce URL scheme: {uri.Scheme} for URL: {announceUrl}");
                return [];
            }

            try
            {
                var uriBuilder = new UriBuilder(announceUrl);
                List<string> queryParams = [];
                var existingQuery = uriBuilder.Query;
                
                // URL-encode info_hash (raw bytes) - в BitTorrent это специальное кодирование
                var infoHashBytes = Convert.FromHexString(infoHash);
                var encodedInfoHash = UrlEncodeBytes(infoHashBytes);
                queryParams.Add($"info_hash={encodedInfoHash}");
                
                // Peer ID должен быть URL-encoded как строка (для HTTP трекеров)
                queryParams.Add($"peer_id={Uri.EscapeDataString(peerId)}");
                queryParams.Add($"port={port}");
                queryParams.Add($"uploaded={uploaded}");
                queryParams.Add($"downloaded={downloaded}");
                queryParams.Add($"left={left}");
                queryParams.Add("compact=1");
                queryParams.Add("no_peer_id=1"); // Не требуем peer_id в ответе (для compact формата)
                
                // Генерируем уникальный key для идентификации клиента
                var random = new Random();
                var key = random.Next(0, int.MaxValue);
                queryParams.Add($"key={key}");
                
                // Запрашиваем максимум пиров (200 - стандартное значение для хорошего охвата)
                var requestedPeers = numwant ?? 200;
                queryParams.Add($"numwant={requestedPeers}");
                
                // Добавляем параметр ip=0 для получения пиров со всех IP (некоторые трекеры требуют)
                // queryParams.Add("ip=0"); // Раскомментируйте, если нужно
                
                if (!string.IsNullOrEmpty(eventType))
                {
                    queryParams.Add($"event={Uri.EscapeDataString(eventType)}");
                }

                var newQueryString = string.Join("&", queryParams);
                if (!string.IsNullOrWhiteSpace(existingQuery))
                {
                    var normalizedExisting = existingQuery.TrimStart('?');
                    if (!string.IsNullOrWhiteSpace(normalizedExisting))
                    {
                        newQueryString = $"{normalizedExisting}&{newQueryString}";
                    }
                }

                uriBuilder.Query = newQueryString;
                var url = uriBuilder.ToString();

                Logger.LogInfo($"Announcing to tracker: {announceUrl}");
                Logger.LogInfo($"Request URL: {url}");
                Logger.LogInfo($"Request parameters: numwant={requestedPeers}, compact=1, no_peer_id=1, key={key}");
                
                // Применяем пользовательские заголовки
                Dictionary<string, string> headers = [];
                ApplyCustomHeaders(uriBuilder.Uri, headers);
                
                var response = await HttpClientService.Instance.GetAsync(url, headers, timeoutSeconds: 30, cancellationToken: default).ConfigureAwait(false);
                
                // Проверяем статус код ответа
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = response.GetBodyAsString();
                    Logger.LogError($"Tracker returned error status: {response.StatusCode} ({response.StatusMessage}) for URL: {announceUrl}. Response: {responseContent}");
                    return [];
                }

                var responseBytes = response.Body;
                Logger.LogInfo($"Tracker response size: {responseBytes.Length} bytes");
                
                // Логируем полный ответ для диагностики (если ответ не слишком большой)
                if (responseBytes.Length < 500)
                {
                    var hexPreview = BitConverter.ToString(responseBytes).Replace("-", " ");
                    Logger.LogInfo($"Tracker response (full hex): {hexPreview}");
                    
                    // Также пробуем прочитать как строку для диагностики
                    try
                    {
                        var textPreview = Encoding.UTF8.GetString(responseBytes);
                        if (textPreview.All(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c) || c < 128))
                        {
                            Logger.LogInfo($"Tracker response (as text): {textPreview}");
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки при попытке прочитать как текст
                    }
                }
                else
                {
                    var hexPreview = BitConverter.ToString(responseBytes, 0, 500).Replace("-", " ");
                    Logger.LogInfo($"Tracker response (first 500 bytes, hex): {hexPreview}...");
                }
                
                // Логируем полный ответ для диагностики (если ответ не слишком большой)
                if (responseBytes.Length < 500)
                {
                    var hexPreview = BitConverter.ToString(responseBytes).Replace("-", " ");
                    Logger.LogInfo($"Tracker response (full hex): {hexPreview}");
                    
                    // Также пробуем прочитать как строку для диагностики
                    try
                    {
                        var textPreview = Encoding.UTF8.GetString(responseBytes);
                        if (textPreview.All(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c) || c < 128))
                        {
                            Logger.LogInfo($"Tracker response (as text): {textPreview}");
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки при попытке прочитать как текст
                    }
                }
                else
                {
                    var hexPreview = BitConverter.ToString(responseBytes, 0, 500).Replace("-", " ");
                    Logger.LogInfo($"Tracker response (first 500 bytes, hex): {hexPreview}...");
                }
                
                var peers = ParseAnnounceResponse(responseBytes);
                
                // Фильтруем self peers из ответа трекера
                List<IPEndPoint> filteredPeers = [];
                foreach (var peer in peers)
                {
                    // Проверяем, не является ли это наш собственный IP и порт
                    // (трекеры часто возвращают наш собственный IP)
                    // Мы не можем проверить это здесь, так как не знаем наш порт
                    // Фильтрация будет выполнена в TorrentDownloader
                    filteredPeers.Add(peer);
                }
                
                Logger.LogInfo($"Received {peers.Count} peers from tracker: {announceUrl} (requested: {requestedPeers}, after filtering: {filteredPeers.Count})");
                
                // Если получили меньше пиров, чем запрашивали, логируем предупреждение
                if (filteredPeers.Count < requestedPeers && filteredPeers.Count > 0)
                {
                    Logger.LogInfo($"Tracker returned only {filteredPeers.Count} peers out of {requestedPeers} requested. This may be normal for new clients or low-activity torrents.");
                }
                else if (filteredPeers.Count == 0)
                {
                    Logger.LogWarning($"Tracker returned no peers: {announceUrl}. Torrent may be inactive or tracker may not have peers.");
                }
                
                return filteredPeers;
            }
            catch (SocketException ex)
            {
                Logger.LogError($"Socket error when announcing to tracker: {announceUrl} - {ex.Message}", ex);
                return [];
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError($"Request timeout when announcing to tracker: {announceUrl}", ex);
                return [];
            }
            catch (UriFormatException ex)
            {
                Logger.LogError($"Invalid URL format: {announceUrl}", ex);
                return [];
            }
            catch (ArgumentException ex)
            {
                Logger.LogError($"Invalid argument when announcing to tracker: {announceUrl}. Error: {ex.Message}", ex);
                return [];
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error when announcing to tracker: {announceUrl}", ex);
                return [];
            }
        }

        private static List<IPEndPoint> ParseAnnounceResponse(byte[] data)
        {
            List<IPEndPoint> peers = [];
            
            try
            {
                var dict = _bencodeParser.Parse<BDictionary>(data);
                
                // Логируем все ключи в ответе для диагностики
                Logger.LogInfo($"Tracker response keys: {string.Join(", ", dict.Keys)}");
                
                // Проверяем наличие других полей
                if (dict.ContainsKey("failure reason"))
                {
                    var failureReasonObj = dict["failure reason"];
                    string failureReason;
                    
                    // failure reason может быть BString или другой тип
                    if (failureReasonObj is BString failureBString)
                    {
                        failureReason = failureBString.ToString();
                    }
                    else
                    {
                        failureReason = failureReasonObj?.ToString() ?? "Unknown error";
                    }
                    
                    Logger.LogWarning($"Tracker returned failure: {failureReason}");
                    
                    // Некоторые трекеры возвращают и ошибку, и пиров - парсим пиров в любом случае
                    // Если peers нет или пустой, вернем пустой список
                    // НО: если это "Torrent not registered", это может означать, что торрент не зарегистрирован
                    // на трекере, но пиры все равно могут быть возвращены (например, из кеша)
                }
                
                if (dict.ContainsKey("warning message"))
                {
                    var warningObj = dict["warning message"];
                    string warning;
                    
                    if (warningObj is BString warningBString)
                    {
                        warning = warningBString.ToString();
                    }
                    else
                    {
                        warning = warningObj?.ToString() ?? "Unknown warning";
                    }
                    
                    Logger.LogWarning($"Tracker warning: {warning}");
                }
                
                if (!dict.ContainsKey("peers"))
                {
                    Logger.LogWarning("Tracker response does not contain 'peers' key");
                    return peers;
                }

                var peersValue = dict["peers"];
                
                // Компактный формат: пиры представлены массивом байт (BString)
                if (peersValue is BString peersBString)
                {
                    var peersBytes = peersBString.Value.ToArray(); // Преобразуем ReadOnlyMemory<byte> в byte[]
                    Logger.LogInfo($"Parsing compact peers format, bytes length: {peersBytes.Length}");
                    
                    // Логируем hex-превью данных peers для диагностики
                    if (peersBytes.Length <= 100)
                    {
                        var peersHex = BitConverter.ToString(peersBytes).Replace("-", " ");
                        Logger.LogInfo($"Peers data (hex): {peersHex}");
                    }
                    else
                    {
                        var peersHex = BitConverter.ToString(peersBytes, 0, 100).Replace("-", " ");
                        Logger.LogInfo($"Peers data (first 100 bytes, hex): {peersHex}...");
                    }
                    
                    // Компактный формат: 6 байт на пира (4 байта IP + 2 байта порт)
                    var expectedPeers = peersBytes.Length / 6;
                    if (peersBytes.Length % 6 != 0)
                    {
                        Logger.LogWarning($"Peers bytes length ({peersBytes.Length}) is not divisible by 6, expected {expectedPeers} peers, will parse {expectedPeers}");
                    }
                    
                    Logger.LogInfo($"Parsing {expectedPeers} peers from compact format (total bytes: {peersBytes.Length})");
                    
                    for (var i = 0; i <= peersBytes.Length - 6; i += 6)
                    {
                        var ipBytes = new byte[4];
                        Array.Copy(peersBytes, i, ipBytes, 0, 4);
                        var ip = new IPAddress(ipBytes);
                        
                        // Порт в формате big-endian (сетевой порядок байт)
                        var port = (ushort)((peersBytes[i + 4] << 8) | peersBytes[i + 5]);
                        
                        if (port > 0)
                        {
                            peers.Add(new IPEndPoint(ip, port));
                            Logger.LogInfo($"Parsed peer: {ip}:{port}");
                        }
                        else
                        {
                            Logger.LogWarning($"Invalid port {port} for peer {ip} at offset {i}");
                        }
                    }
                    
                    Logger.LogInfo($"Successfully parsed {peers.Count} peers from compact format (expected {expectedPeers})");
                    
                    if (peers.Count < expectedPeers)
                    {
                        Logger.LogWarning($"Parsed {peers.Count} peers but expected {expectedPeers} - possible parsing issue");
                    }
                }
                // Non-compact format: peers is a list of dictionaries
                else if (peersValue is BList peersList)
                {
                    Logger.LogInfo($"Parsing non-compact peers format, list length: {peersList.Count}");
                    
                    foreach (var peerObj in peersList)
                    {
                        if (peerObj is BDictionary peerDict)
                        {
                            if (peerDict.ContainsKey("ip") && peerDict.ContainsKey("port"))
                            {
                                var ipObj = peerDict["ip"];
                                var portObj = peerDict["port"];
                                
                                string? ipStr = null;
                                if (ipObj is BString ipBString)
                                {
                                    ipStr = ipBString.ToString();
                                }
                                else
                                {
                                    ipStr = ipObj?.ToString();
                                }
                                
                                int port = 0;
                                if (portObj is BNumber portNumber)
                                {
                                    port = (int)portNumber.Value;
                                }
                                
                                if (!string.IsNullOrEmpty(ipStr) && 
                                    IPAddress.TryParse(ipStr, out var ip) &&
                                    port > 0 && port <= 65535)
                                {
                                    peers.Add(new IPEndPoint(ip, port));
                                }
                                else
                                {
                                    Logger.LogWarning($"Invalid peer data: ip={ipStr}, port={port}");
                                }
                            }
                        }
                    }
                    
                    Logger.LogInfo($"Parsed {peers.Count} peers from non-compact format");
                }
                else
                {
                    Logger.LogWarning($"Unknown peers format: {peersValue?.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error parsing tracker announce response", ex);
            }

            return peers;
        }

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

        private void ApplyCustomHeaders(Uri uri, Dictionary<string, string> headers)
        {
            var host = uri.Host;

            if (_trackerCookies.TryGetValue(host, out var hostCookie) && !string.IsNullOrWhiteSpace(hostCookie))
            {
                headers["Cookie"] = hostCookie;
            }
            else if (_trackerCookies.TryGetValue("*", out var wildcardCookie) && !string.IsNullOrWhiteSpace(wildcardCookie))
            {
                headers["Cookie"] = wildcardCookie;
            }

            if (_trackerHeaders.TryGetValue(host, out var hostHeaders))
            {
                foreach (var header in hostHeaders)
                {
                    headers[header.Key] = header.Value;
                }
            }
            else if (_trackerHeaders.TryGetValue("*", out var wildcardHeaders))
            {
                foreach (var header in wildcardHeaders)
                {
                    headers[header.Key] = header.Value;
                }
            }
        }

        private static async Task<List<IPEndPoint>> AnnounceUdpAsync(Uri trackerUri, string infoHash, string peerId,
            long downloaded, long uploaded, long left, int port, string? eventType, int numwant)
        {
            List<IPEndPoint> peers = [];
            
            try
            {
                Logger.LogInfo($"Announcing to UDP tracker: {trackerUri}");
                
                // Получаем IP адрес и порт трекера
                IPAddress? trackerIp = null;
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(trackerUri.Host);
                    if (hostEntry.AddressList.Length == 0)
                    {
                        Logger.LogWarning($"Could not resolve host: {trackerUri.Host}");
                        return peers;
                    }
                    
                    // Предпочитаем IPv4 адреса
                    trackerIp = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                    trackerIp ??= hostEntry.AddressList[0];
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"DNS resolution failed for {trackerUri.Host}: {ex.Message}");
                    return peers;
                }
                
                var trackerPort = trackerUri.Port > 0 ? trackerUri.Port : 80;
                var trackerEndPoint = new IPEndPoint(trackerIp, trackerPort);
                
                Logger.LogInfo($"UDP tracker endpoint: {trackerEndPoint}");
                
                using var udpClient = new UdpClient();
                udpClient.Client.ReceiveTimeout = 8000; // 8 секунд таймаут
                
                // Шаг 1: Connect Request
                var connectionId = await ConnectUdpAsync(udpClient, trackerEndPoint);
                if (connectionId == 0)
                {
                    Logger.LogInfo($"Failed to get connection ID from UDP tracker: {trackerUri} (tracker may be down or unreachable)");
                    return peers;
                }
                
                Logger.LogInfo($"Got connection ID from UDP tracker: {connectionId:X16}");
                
                // Шаг 2: Announce Request
                peers = await AnnounceUdpRequestAsync(udpClient, trackerEndPoint, connectionId, infoHash, peerId,
                    downloaded, uploaded, left, port, eventType, numwant);
                
                Logger.LogInfo($"Received {peers.Count} peers from UDP tracker: {trackerUri}");
            }
            catch (SocketException ex)
            {
                // Не логируем как ошибку, так как многие UDP трекеры могут быть недоступны
                Logger.LogInfo($"UDP tracker {trackerUri} unavailable: {ex.SocketErrorCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"Error announcing to UDP tracker: {trackerUri} - {ex.Message}");
            }
            
            return peers;
        }

        private static async Task<long> ConnectUdpAsync(UdpClient udpClient, IPEndPoint trackerEndPoint)
        {
            var random = new Random();
            var transactionId = random.Next();
            
            // Connect Request: connection_id (8 bytes), action (4 bytes), transaction_id (4 bytes)
            var request = new byte[16];
            
            // connection_id = 0x41727101980 (magic number для connect)
            var magicConnectionId = 0x41727101980L;
            var connectionIdBytes = BitConverter.GetBytes(magicConnectionId);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(connectionIdBytes);
            Array.Copy(connectionIdBytes, 0, request, 0, 8);
            
            // action = 0 (connect)
            var actionBytes = BitConverter.GetBytes(0);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(actionBytes);
            Array.Copy(actionBytes, 0, request, 8, 4);
            
            // transaction_id
            var transactionIdBytes = BitConverter.GetBytes(transactionId);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(transactionIdBytes);
            Array.Copy(transactionIdBytes, 0, request, 12, 4);
            
            // Отправляем запрос с повторными попытками
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await TaskTimeoutHelper.TimeoutAsync(
                        udpClient.SendAsync(request, request.Length, trackerEndPoint),
                        TimeSpan.FromSeconds(5));
                    
                    // Ждем ответ (таймаут 5 секунд)
                    var result = await TaskTimeoutHelper.TimeoutAsync(
                        udpClient.ReceiveAsync(),
                        TimeSpan.FromSeconds(5));
                    
                    if (result.Buffer.Length < 16)
                    {
                        Logger.LogWarning($"UDP connect response too short: {result.Buffer.Length} bytes");
                        continue;
                    }
                    
                    // Проверяем transaction_id
                    var responseTransactionId = BitConverter.ToInt32(result.Buffer, 4);
                    if (BitConverter.IsLittleEndian)
                        responseTransactionId = IPAddress.NetworkToHostOrder(responseTransactionId);
                    
                    if (responseTransactionId != transactionId)
                    {
                        Logger.LogWarning($"UDP connect response transaction_id mismatch: expected {transactionId}, got {responseTransactionId}");
                        continue;
                    }
                    
                    // Проверяем action (должен быть 0)
                    var responseAction = BitConverter.ToInt32(result.Buffer, 0);
                    if (BitConverter.IsLittleEndian)
                        responseAction = IPAddress.NetworkToHostOrder(responseAction);
                    
                    if (responseAction != 0)
                    {
                        Logger.LogWarning($"UDP connect response action mismatch: expected 0, got {responseAction}");
                        continue;
                    }
                    
                    // Получаем connection_id (8 bytes, начиная с байта 8)
                    var responseConnectionIdBytes = new byte[8];
                    Array.Copy(result.Buffer, 8, responseConnectionIdBytes, 0, 8);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(responseConnectionIdBytes);
                    var connectionId = BitConverter.ToInt64(responseConnectionIdBytes);
                    
                    return connectionId;
                }
                catch (OperationCanceledException)
                {
                    // Таймаут - не логируем, просто пробуем снова
                    if (attempt < 1)
                        await Task.Delay(500); // Небольшая задержка перед повтором
                }
                catch (Exception ex)
                {
                    Logger.LogInfo($"UDP connect attempt {attempt + 1} failed: {ex.Message}");
                }
            }
            
            return 0;
        }

        private static async Task<List<IPEndPoint>> AnnounceUdpRequestAsync(UdpClient udpClient, IPEndPoint trackerEndPoint,
            long connectionId, string infoHash, string peerId, long downloaded, long uploaded, long left,
            int port, string? eventType, int numwant)
        {
            List<IPEndPoint> peers = [];
            var random = new Random();
            var transactionId = random.Next();
            
            // Announce Request структура:
            // connection_id (8 bytes)
            // action (4 bytes) = 1
            // transaction_id (4 bytes)
            // info_hash (20 bytes)
            // peer_id (20 bytes)
            // downloaded (8 bytes)
            // left (8 bytes)
            // uploaded (8 bytes)
            // event (4 bytes): 0=none, 1=completed, 2=started, 3=stopped
            // IP address (4 bytes) = 0 (use sender's IP)
            // key (4 bytes) = random
            // num_want (4 bytes) = -1 (default) or specified
            // port (2 bytes)
            
            var request = new byte[98];
            var offset = 0;
            
            // connection_id
            var connectionIdBytes = BitConverter.GetBytes(connectionId);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(connectionIdBytes);
            Array.Copy(connectionIdBytes, 0, request, offset, 8);
            offset += 8;
            
            // action = 1 (announce)
            var actionBytes = BitConverter.GetBytes(1);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(actionBytes);
            Array.Copy(actionBytes, 0, request, offset, 4);
            offset += 4;
            
            // transaction_id
            var transactionIdBytes = BitConverter.GetBytes(transactionId);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(transactionIdBytes);
            Array.Copy(transactionIdBytes, 0, request, offset, 4);
            offset += 4;
            
            // info_hash
            var infoHashBytes = Convert.FromHexString(infoHash);
            Array.Copy(infoHashBytes, 0, request, offset, 20);
            offset += 20;
            
            // peer_id
            var peerIdBytes = Encoding.ASCII.GetBytes(peerId);
            if (peerIdBytes.Length > 20)
                Array.Resize(ref peerIdBytes, 20);
            Array.Copy(peerIdBytes, 0, request, offset, peerIdBytes.Length);
            offset += 20;
            
            // downloaded
            var downloadedBytes = BitConverter.GetBytes(downloaded);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(downloadedBytes);
            Array.Copy(downloadedBytes, 0, request, offset, 8);
            offset += 8;
            
            // left
            var leftBytes = BitConverter.GetBytes(left);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(leftBytes);
            Array.Copy(leftBytes, 0, request, offset, 8);
            offset += 8;
            
            // uploaded
            var uploadedBytes = BitConverter.GetBytes(uploaded);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(uploadedBytes);
            Array.Copy(uploadedBytes, 0, request, offset, 8);
            offset += 8;
            
            // event: 0=none, 1=completed, 2=started, 3=stopped
            int eventValue = 0;
            if (eventType == "started") eventValue = 2;
            else if (eventType == "completed") eventValue = 1;
            else if (eventType == "stopped") eventValue = 3;
            
            var eventBytes = BitConverter.GetBytes(eventValue);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(eventBytes);
            Array.Copy(eventBytes, 0, request, offset, 4);
            offset += 4;
            
            // IP address = 0 (use sender's IP)
            var ipBytes = BitConverter.GetBytes(0);
            Array.Copy(ipBytes, 0, request, offset, 4);
            offset += 4;
            
            // key (random)
            var key = random.Next();
            var keyBytes = BitConverter.GetBytes(key);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(keyBytes);
            Array.Copy(keyBytes, 0, request, offset, 4);
            offset += 4;
            
            // num_want
            var numwantBytes = BitConverter.GetBytes(numwant);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(numwantBytes);
            Array.Copy(numwantBytes, 0, request, offset, 4);
            offset += 4;
            
            // port
            var portBytes = BitConverter.GetBytes((ushort)port);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(portBytes);
            Array.Copy(portBytes, 0, request, offset, 2);
            
            // Отправляем запрос с повторными попытками
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await TaskTimeoutHelper.TimeoutAsync(
                        udpClient.SendAsync(request, request.Length, trackerEndPoint),
                        TimeSpan.FromSeconds(5));
                    
                    // Ждем ответ (таймаут 5 секунд)
                    var result = await TaskTimeoutHelper.TimeoutAsync(
                        udpClient.ReceiveAsync(),
                        TimeSpan.FromSeconds(5));
                    
                    if (result.Buffer.Length < 20)
                    {
                        Logger.LogWarning($"UDP announce response too short: {result.Buffer.Length} bytes");
                        continue;
                    }
                    
                    // Проверяем transaction_id
                    var responseTransactionId = BitConverter.ToInt32(result.Buffer, 4);
                    if (BitConverter.IsLittleEndian)
                        responseTransactionId = IPAddress.NetworkToHostOrder(responseTransactionId);
                    
                    if (responseTransactionId != transactionId)
                    {
                        Logger.LogWarning($"UDP announce response transaction_id mismatch: expected {transactionId}, got {responseTransactionId}");
                        continue;
                    }
                    
                    // Проверяем action (должен быть 1)
                    var responseAction = BitConverter.ToInt32(result.Buffer, 0);
                    if (BitConverter.IsLittleEndian)
                        responseAction = IPAddress.NetworkToHostOrder(responseAction);
                    
                    if (responseAction != 1)
                    {
                        if (responseAction == 3) // Ответ с ошибкой
                        {
                            var errorMessage = result.Buffer.Length > 8 
                                ? Encoding.UTF8.GetString(result.Buffer, 8, result.Buffer.Length - 8)
                                : "Unknown error";
                            Logger.LogWarning($"UDP tracker returned error: {errorMessage}");
                        }
                        else
                        {
                            Logger.LogWarning($"UDP announce response action mismatch: expected 1, got {responseAction}");
                        }
                        continue;
                    }
                    
                    // Парсим ответ: action (4), transaction_id (4), interval (4), leechers (4), seeders (4), peers (6 bytes each)
                    if (result.Buffer.Length < 20)
                    {
                        Logger.LogWarning($"UDP announce response too short for header: {result.Buffer.Length} bytes");
                        continue;
                    }
                    
                    var interval = BitConverter.ToInt32(result.Buffer, 8);
                    if (BitConverter.IsLittleEndian)
                        interval = IPAddress.NetworkToHostOrder(interval);
                    
                    var leechers = BitConverter.ToInt32(result.Buffer, 12);
                    if (BitConverter.IsLittleEndian)
                        leechers = IPAddress.NetworkToHostOrder(leechers);
                    
                    var seeders = BitConverter.ToInt32(result.Buffer, 16);
                    if (BitConverter.IsLittleEndian)
                        seeders = IPAddress.NetworkToHostOrder(seeders);
                    
                    Logger.LogInfo($"UDP tracker response: interval={interval}s, leechers={leechers}, seeders={seeders}");
                    
                    // Парсим пиров (начиная с байта 20)
                    var peersDataLength = result.Buffer.Length - 20;
                    if (peersDataLength > 0 && peersDataLength % 6 == 0)
                    {
                        var peersCount = peersDataLength / 6;
                        for (var i = 0; i < peersCount; i++)
                        {
                            var peerOffset = 20 + i * 6;
                            var peerIpBytes = new byte[4];
                            Array.Copy(result.Buffer, peerOffset, peerIpBytes, 0, 4);
                            var ip = new IPAddress(peerIpBytes);
                            
                            var peerPortBytes = new byte[2];
                            Array.Copy(result.Buffer, peerOffset + 4, peerPortBytes, 0, 2);
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(peerPortBytes);
                            var peerPort = BitConverter.ToUInt16(peerPortBytes, 0);
                            
                            if (peerPort > 0)
                            {
                                peers.Add(new IPEndPoint(ip, peerPort));
                            }
                        }
                        
                        Logger.LogInfo($"Parsed {peers.Count} peers from UDP tracker response");
                    }
                    
                    return peers;
                }
                catch (OperationCanceledException)
                {
                    // Таймаут - не логируем, просто пробуем снова
                    if (attempt < 1)
                        await Task.Delay(500); // Небольшая задержка перед повтором
                }
                catch (Exception ex)
                {
                    Logger.LogInfo($"UDP announce attempt {attempt + 1} failed: {ex.Message}");
                }
            }
            
            return peers;
        }

        private static async Task<List<IPEndPoint>> AnnounceTcpAsync(Uri trackerUri, string infoHash, string peerId,
            long downloaded, long uploaded, long left, int port, string? eventType, int numwant)
        {
            List<IPEndPoint> peers = [];
            
            try
            {
                Logger.LogInfo($"Announcing to TCP tracker: {trackerUri}");
                
                // Получаем IP адрес и порт трекера
                IPAddress? trackerIp = null;
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(trackerUri.Host);
                    if (hostEntry.AddressList.Length == 0)
                    {
                        Logger.LogWarning($"Could not resolve host: {trackerUri.Host}");
                        return peers;
                    }
                    
                    // Предпочитаем IPv4 адреса
                    trackerIp = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                    trackerIp ??= hostEntry.AddressList[0];
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"DNS resolution failed for {trackerUri.Host}: {ex.Message}");
                    return peers;
                }
                
                var trackerPort = trackerUri.Port > 0 ? trackerUri.Port : 80;
                var trackerEndPoint = new IPEndPoint(trackerIp, trackerPort);
                
                Logger.LogInfo($"TCP tracker endpoint: {trackerEndPoint}");
                
                using var tcpClient = new TcpClient();
                tcpClient.NoDelay = true;  // Отключаем алгоритм Nagle
                tcpClient.LingerState = new LingerOption(false, 0);  // LingerState = 0
                tcpClient.ReceiveTimeout = 10000; // 10 секунд таймаут
                tcpClient.SendTimeout = 10000;
                
                // Подключаемся к трекеру с таймаутом
                try
                {
                    await TaskTimeoutHelper.TimeoutAsync(
                        tcpClient.ConnectAsync(trackerEndPoint.Address, trackerEndPoint.Port),
                        TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    Logger.LogInfo($"TCP tracker connection timeout: {trackerUri}");
                    return peers;
                }
                Logger.LogInfo($"Connected to TCP tracker: {trackerEndPoint}");
                
                using var stream = tcpClient.GetStream();
                
                // Формируем announce запрос в bencode формате
                var requestDict = new BDictionary
                {
                    ["info_hash"] = new BString(Convert.FromHexString(infoHash)),
                    ["peer_id"] = new BString(Encoding.ASCII.GetBytes(peerId)),
                    ["port"] = new BNumber(port),
                    ["uploaded"] = new BNumber(uploaded),
                    ["downloaded"] = new BNumber(downloaded),
                    ["left"] = new BNumber(left),
                    ["compact"] = new BNumber(1),
                    ["numwant"] = new BNumber(numwant)
                };
                
                if (!string.IsNullOrEmpty(eventType))
                {
                    requestDict["event"] = new BString(Encoding.ASCII.GetBytes(eventType));
                }
                
                // Кодируем запрос в bencode
                var requestBytes = requestDict.EncodeAsBytes();
                
                // Отправляем запрос
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(requestBytes.AsMemory()),
                    TimeSpan.FromSeconds(10));
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.FlushAsync(),
                    TimeSpan.FromSeconds(5));
                Logger.LogInfo($"Sent TCP announce request ({requestBytes.Length} bytes)");
                
                // Читаем ответ с таймаутом
                List<byte> responseBuffer = [];
                var buffer = new byte[4096];
                
                try
                {
                    while (true)
                    {
                        var bytesRead = await TaskTimeoutHelper.TimeoutAsync(
                            stream.ReadAsync(buffer.AsMemory()),
                            TimeSpan.FromSeconds(10));
                        if (bytesRead == 0)
                        {
                            Logger.LogInfo($"TCP tracker closed connection: {trackerUri}");
                            break;
                        }
                        
                        responseBuffer.AddRange(buffer.Take(bytesRead));
                        
                        // Проверяем, завершен ли bencode объект
                        if (responseBuffer.Count > 0 && responseBuffer[0] == (byte)'d')
                        {
                            try
                            {
                                // Пробуем распарсить - если успешно, значит получили полный ответ
                                var dict = _bencodeParser.Parse<BDictionary>([.. responseBuffer]);
                                Logger.LogInfo($"Successfully parsed bencode dictionary from TCP tracker");
                                break;
                            }
                            catch (Exception parseEx)
                            {
                                // Еще не весь ответ получен, продолжаем читать
                                Logger.LogInfo($"Bencode parse attempt failed (incomplete data?): {parseEx.Message}, bytes so far: {responseBuffer.Count}");
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    Logger.LogInfo($"TCP tracker read timeout: {trackerUri}");
                }
                
                if (responseBuffer.Count == 0)
                {
                    Logger.LogWarning($"No response from TCP tracker: {trackerUri}");
                    return peers;
                }
                
                Logger.LogInfo($"Received TCP tracker response ({responseBuffer.Count} bytes)");
                
                // Логируем hex дамп для отладки
                var hexDump = string.Join(" ", responseBuffer.Take(Math.Min(100, responseBuffer.Count)).Select(b => $"{b:X2}"));
                Logger.LogInfo($"TCP tracker response (first 100 bytes hex): {hexDump}");
                
                // Парсим ответ
                try
                {
                    peers = ParseAnnounceResponse([.. responseBuffer]);
                    Logger.LogInfo($"Received {peers.Count} peers from TCP tracker: {trackerUri}");
                }
                catch (Exception parseEx)
                {
                    Logger.LogWarning($"Failed to parse TCP tracker response: {parseEx.Message}");
                }
            }
            catch (SocketException ex)
            {
                Logger.LogInfo($"TCP tracker {trackerUri} unavailable: {ex.SocketErrorCode} - {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo($"TCP tracker {trackerUri} operation cancelled (timeout)");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error announcing to TCP tracker: {trackerUri} - {ex.GetType().Name}: {ex.Message}");
                Logger.LogInfo($"Stack trace: {ex.StackTrace}");
            }
            
            return peers;
        }

    }

}

