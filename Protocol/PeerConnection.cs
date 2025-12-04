using System;
using TorrentClient.Protocol.Interfaces;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Битовые флаги для Reserved Bytes в BitTorrent handshake
    /// Согласно спецификации BitTorrent:
    /// - BEP 3: The BitTorrent Protocol Specification
    /// - BEP 5: DHT Protocol
    /// - BEP 6: Fast Extension
    /// - BEP 11: Peer Exchange (PEX)
    /// 
    /// Reserved bytes находятся в позициях 20-27 в handshake (8 байт, индексы 20-27)
    /// Каждый бит указывает на поддержку определенного расширения протокола
    /// </summary>
    [Flags]
    public enum ReservedBytesFlags
    {
        /// <summary>
        /// Нет установленных флагов
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Bit 63 (byte[20], bit 0): Поддержка Extension Protocol
        /// BEP 5: Необходим для PEX (BEP 11) и других расширений протокола
        /// Позволяет использовать расширенные сообщения для обмена дополнительной информацией
        /// </summary>
        ExtensionProtocol = 1 << 0, // byte[20], bit 0
        
        /// <summary>
        /// Bit 64 (byte[27], bit 0): Поддержка DHT
        /// BEP 5: Distributed Hash Table (DHT) Protocol
        /// Указывает, что клиент поддерживает DHT для поиска пиров без трекеров
        /// </summary>
        DHT = 1 << 1, // byte[27], bit 0
        
        /// <summary>
        /// Bit 62 (byte[27], bit 2): Расширение Fast Peers
        /// BEP 6: Fast Extension
        /// Оптимизирует обмен сообщениями для более быстрой загрузки
        /// </summary>
        FastPeers = 1 << 2, // byte[27], bit 2
        
        /// <summary>
        /// Bit 61 (byte[27], bit 3): NAT Traversal (обход NAT)
        /// BEP 10: NAT Traversal - устаревший, но некоторые клиенты используют
        /// </summary>
        NATTraversal = 1 << 3, // byte[27], bit 3
        
        /// <summary>
        /// Bit 60 (byte[27], bit 4): Azureus Messaging Protocol (протокол обмена сообщениями Azureus)
        /// Устаревший протокол обмена сообщениями
        /// </summary>
        AzureusMessaging = 1 << 4, // byte[27], bit 4
    }
    
    public class PeerConnection : IDisposable, IAsyncDisposable
    {
        
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly string _peerId;
        private readonly string _infoHash;
        private readonly BitField _bitField;
        private BitField? _peerBitField; // Bitfield полученный от пира
        private bool _isConnected;
        private bool _isChoked = true;
        private bool _isInterested;
        private bool _peerChoked = true;
        private bool _peerInterested;
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
        private CancellationTokenSource? _cancellationTokenSource;
        internal PeerExchange? _peerExchange;
        private Task? _readTask;
        private byte[]? _remoteReservedBytes; // Reserved bytes от пира для проверки поддержки расширений

        // Внутренние поля для доступа из статического метода
        internal TcpClient? TcpClient { set => _tcpClient = value; }
        internal NetworkStream? Stream { set => _stream = value; }
        internal bool SetIsConnected { set => _isConnected = value; }
        internal CancellationTokenSource? CancellationTokenSource { set => _cancellationTokenSource = value; }

        public IPEndPoint EndPoint { get; }
        public bool IsConnected => _isConnected;
        public bool IsChoked => _isChoked;
        public bool PeerChoked => _peerChoked;
        public bool PeerInterested => _peerInterested;
        public long DownloadSpeed { get; internal set; }
        public long UploadSpeed { get; internal set; }
        public BitField? PeerBitField => _peerBitField; // Доступ к bitfield пира для определения доступных кусков

        public class PieceDataEventArgs : EventArgs
        {
            public int PieceIndex { get; set; }
            public int Begin { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        #region Асинхронные колбэки (замена событий)

        private IPeerConnectionCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(IPeerConnectionCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion
        
        public class HaveEventArgs : EventArgs
        {
            public int PieceIndex { get; set; }
        }
        
        public class RequestEventArgs : EventArgs
        {
            public int PieceIndex { get; set; }
            public int Begin { get; set; }
            public int Length { get; set; }
        }

        public PeerExchange? PeerExchange => _peerExchange;
        
        /// <summary>Обработчик получения пиров через PEX (для корректной отписки)</summary>
        internal void OnPexPeersReceived(object? sender, List<IPEndPoint> peers)
        {
            if (_callbacks != null)
            {
                _ = Task.Run(async () => await _callbacks.OnPexPeersReceivedAsync(peers).ConfigureAwait(false));
            }
        }

        public PeerConnection(IPEndPoint endPoint, string peerId, string infoHash, BitField bitField)
        {
            EndPoint = endPoint;
            _peerId = peerId;
            _infoHash = infoHash;
            _bitField = bitField;
        }

        // Создает PeerConnection из входящего подключения (пир подключился к нам)
        // В этом случае пир отправляет handshake первым, мы читаем его и отправляем ответ
        public static async Task<PeerConnection?> FromIncomingConnectionAsync(
            TcpClient tcpClient, 
            string peerId, 
            string infoHash, 
            BitField bitField,
            CancellationToken cancellationToken)
        {
            try
            {
                var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                Logger.LogInfo($"[PeerConnection] Handling incoming connection from {remoteEndPoint}");

                // Настраиваем буферы для высокой производительности
                tcpClient.NoDelay = true;                   // Выключаем алгоритм Нагла (объединение мелких пакетов)
                tcpClient.LingerState = new LingerOption(false, 0);  // Состояние задержки закрытия = 0
                tcpClient.ReceiveBufferSize = 256 * 1024;
                tcpClient.SendBufferSize = 256 * 1024;

                var connection = new PeerConnection(remoteEndPoint, peerId, infoHash, bitField);
                connection.TcpClient = tcpClient;
                connection.Stream = tcpClient.GetStream();
                connection.SetIsConnected = true;
                connection.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Читаем входящий handshake
                if (!await connection.ReceiveHandshakeAsync())
                {
                    Logger.LogWarning($"[PeerConnection] Failed to receive handshake from incoming peer {remoteEndPoint}");
                    connection.Dispose();
                    return null;
                }

                // Отправляем ответный handshake
                if (!await connection.SendHandshakeAsync())
                {
                    Logger.LogWarning($"[PeerConnection] Failed to send handshake response to incoming peer {remoteEndPoint}");
                    connection.Dispose();
                    return null;
                }

                Logger.LogInfo($"[PeerConnection] Handshake completed with incoming peer {remoteEndPoint}");

                // Инициализируем PEX после handshake
                connection._peerExchange = new PeerExchange(connection);
                var pexCallbacks = new PeerExchangeCallbacksWrapper(connection);
                connection._peerExchange.SetCallbacks(pexCallbacks);

                // Отправляем extension handshake для поддержки PEX
                // Проверяем, поддерживает ли пир Extension Protocol
                // Extension handshake должен быть отправлен до Bitfield
                if (connection._remoteReservedBytes != null)
                {
                    var remoteFlags = GetReservedBytesFlags(connection._remoteReservedBytes);
                    if (remoteFlags.HasFlag(ReservedBytesFlags.ExtensionProtocol))
                    {
                        await connection._peerExchange.SendExtensionHandshakeAsync();
                    }
                }

                // Отправляем Bitfield сразу после handshake
                // Это позволяет пиру знать, какие куски у нас есть
                await connection.SendBitfieldAsync();

                // Запускаем цикл чтения сообщений
                var cts = connection._cancellationTokenSource;
                if (cts != null)
                {
                    // КРИТИЧНО: Сохраняем ссылку на задачу для корректного завершения
                    connection._readTask = Task.Run(() => connection.ReceiveMessagesLoopAsync(cts.Token));
                }

                return connection;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PeerConnection] Error creating connection from incoming peer", ex);
                // КРИТИЧНО: Гарантируем закрытие TcpClient даже при ошибках
                try
                {
                    tcpClient?.Close();
                    tcpClient?.Dispose();
                }
                catch { }
                return null;
            }
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            TcpClient? tempTcpClient = null;
            try
            {
                Logger.LogInfo($"[PeerConnection] Starting connection to {EndPoint}");
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tempTcpClient = new TcpClient
                {
                    NoDelay = true,                     // Выключаем алгоритм Нагла (объединение мелких пакетов)
                    LingerState = new LingerOption(false, 0),  // LingerState = 0
                    ReceiveTimeout = 30000,             // 30 секунд для стабильных соединений
                    SendTimeout = 30000,
                    ReceiveBufferSize = 256 * 1024,     // 256KB буфер приёма для высокой скорости
                    SendBufferSize = 256 * 1024         // 256KB буфер отправки
                };
                _tcpClient = tempTcpClient;

                Logger.LogInfo($"[PeerConnection] Attempting TCP connection to {EndPoint.Address}:{EndPoint.Port}");
                
                // Используем таймаут для подключения (5 секунд - уменьшаем для более быстрой обработки)
                await TaskTimeoutHelper.TimeoutAsync(
                    _tcpClient.ConnectAsync(EndPoint.Address, EndPoint.Port, cancellationToken),
                    TimeSpan.FromSeconds(5));
                Logger.LogInfo($"[PeerConnection] TCP connection established to {EndPoint}");
                
                _stream = _tcpClient.GetStream();
                _isConnected = true;
                tempTcpClient = null; // Успешно подключились, не нужно закрывать

                // Отправляем handshake
                Logger.LogInfo($"[PeerConnection] Sending handshake to {EndPoint}");
                if (!await SendHandshakeAsync())
                {
                    Logger.LogWarning($"[PeerConnection] Failed to send handshake to {EndPoint}");
                    Disconnect();
                    return false;
                }
                Logger.LogInfo($"[PeerConnection] Handshake sent successfully to {EndPoint}");

                // Читаем handshake ответ
                Logger.LogInfo($"[PeerConnection] Waiting for handshake response from {EndPoint}");
                if (!await ReceiveHandshakeAsync())
                {
                    Logger.LogWarning($"[PeerConnection] Failed to receive handshake from {EndPoint}");
                    Disconnect();
                    return false;
                }
                Logger.LogInfo($"[PeerConnection] Handshake completed successfully with {EndPoint}");

                // Инициализируем PEX после handshake
                _peerExchange = new PeerExchange(this);
                var pexCallbacks = new PeerExchangeCallbacksWrapper(this);
                _peerExchange.SetCallbacks(pexCallbacks);

                // Отправляем extension handshake для поддержки PEX
                // Проверяем, поддерживает ли пир Extension Protocol
                // Extension handshake должен быть отправлен до Bitfield
                if (_remoteReservedBytes != null)
                {
                    var remoteFlags = GetReservedBytesFlags(_remoteReservedBytes);
                    if (remoteFlags.HasFlag(ReservedBytesFlags.ExtensionProtocol))
                    {
                        await _peerExchange.SendExtensionHandshakeAsync();
                    }
                }

                // Отправляем Bitfield сразу после handshake
                // Это позволяет пиру знать, какие куски у нас есть
                await SendBitfieldAsync();

                // Запускаем цикл чтения сообщений
                // КРИТИЧНО: Сохраняем ссылку на задачу для корректного завершения
                _readTask = Task.Run(() => ReceiveMessagesLoopAsync(_cancellationTokenSource.Token));

                Logger.LogInfo($"[PeerConnection] Connection established successfully to {EndPoint}");
                return true;
            }
            catch (SocketException ex)
            {
                // Логируем все SocketException для диагностики с детальной информацией
                var errorMsg = ex.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => $"Connection refused - peer may not be listening or firewall blocking",
                    SocketError.TimedOut => $"Connection timed out - peer may be unreachable",
                    SocketError.NetworkUnreachable => $"Network unreachable",
                    SocketError.HostUnreachable => $"Host unreachable",
                    SocketError.ConnectionReset => $"Connection reset by peer",
                    _ => $"{ex.SocketErrorCode} - {ex.Message}"
                };
                Logger.LogWarning($"[PeerConnection] Socket error connecting to {EndPoint}: {errorMsg}");
                Disconnect();
                return false;
            }
            catch (TimeoutException)
            {
                Logger.LogWarning($"[PeerConnection] Connection to {EndPoint} timed out after 5 seconds - peer may be unreachable or not accepting connections");
                Disconnect();
                return false;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo($"[PeerConnection] Connection to {EndPoint} was cancelled by token");
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PeerConnection] Error connecting to peer {EndPoint}", ex);
                // КРИТИЧНО: Закрываем временный TcpClient если он был создан, но соединение не установлено
                if (tempTcpClient != null)
                {
                    try
                    {
                        tempTcpClient.Close();
                        tempTcpClient.Dispose();
                    }
                    catch { }
                    _tcpClient = null;
                }
                Disconnect();
                return false;
            }
        }

        private async Task<bool> SendHandshakeAsync()
        {
            if (_stream == null) return false;

            // Handshake согласно спецификации BitTorrent:
            // 1 байт: длина протокола (19)
            // 19 байт: "BitTorrent protocol"
            // 8 байт: reserved bytes (используются для расширений протокола)
            // 20 байт: info_hash
            // 20 байт: peer_id
            // Всего: 68 байт
            
            byte[] handshake = new byte[68];
            handshake[0] = 19; // Длина строки протокола
            byte[] protocol = Encoding.ASCII.GetBytes("BitTorrent protocol");
            Array.Copy(protocol, 0, handshake, 1, 19);
            
            // Reserved bytes (8 bytes) согласно спецификации BitTorrent BEP 3
            // Байты 20-27 (индексы 20-27 в массиве handshake)
            // Инициализируем все reserved bytes нулями
            for (int i = 20; i < 28; i++)
            {
                handshake[i] = 0x00;
            }
            
            // Устанавливаем битовые флаги согласно RFC протокола BitTorrent
            // Используем enum ReservedBytesFlags для установки флагов
            var flags = ReservedBytesFlags.ExtensionProtocol | ReservedBytesFlags.DHT | ReservedBytesFlags.FastPeers;
            SetReservedBytes(handshake, flags);
            
            byte[] infoHashBytes = Convert.FromHexString(_infoHash);
            Array.Copy(infoHashBytes, 0, handshake, 28, 20);
            
            // Peer ID должен быть ровно 20 байт
            byte[] peerIdBytes = Encoding.ASCII.GetBytes(_peerId);
            if (peerIdBytes.Length != 20)
            {
                Logger.LogError($"Invalid peer ID length: {peerIdBytes.Length}, expected 20");
                return false;
            }
            Array.Copy(peerIdBytes, 0, handshake, 48, 20);

            await _sendSemaphore.WaitAsync();
            try
            {
                var stream = _stream; // Локальная копия для потокобезопасности
                if (stream == null) return false;
                
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(handshake),
                    TimeSpan.FromSeconds(10));
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.FlushAsync(),
                    TimeSpan.FromSeconds(5));
                return true;
            }
            catch (SocketException ex)
            {
                // Таймауты и разрывы соединения - нормальная ситуация
                if (ex.SocketErrorCode != SocketError.TimedOut && 
                    ex.SocketErrorCode != SocketError.ConnectionReset &&
                    ex.SocketErrorCode != SocketError.Shutdown)
                {
                    Logger.LogWarning($"Socket error sending handshake to {EndPoint}: {ex.SocketErrorCode} - {ex.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending handshake to {EndPoint}", ex);
                return false;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private async Task<bool> ReceiveHandshakeAsync()
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null) return false;

            try
            {
                var buffer = new byte[68];
                var totalRead = 0;

                Logger.LogInfo($"[PeerConnection] Reading handshake response from {EndPoint} (need 68 bytes)");
                while (totalRead < 68)
                {
                    stream = _stream; // Проверяем актуальность
                    if (stream == null) return false;
                    
                    var read = await TaskTimeoutHelper.TimeoutAsync(
                        stream.ReadAsync(buffer, totalRead, 68 - totalRead),
                        TimeSpan.FromSeconds(10));
                    if (read == 0)
                    {
                        Logger.LogWarning($"[PeerConnection] Connection closed by peer {EndPoint} while reading handshake (read {totalRead}/68 bytes)");
                        return false;
                    }
                    totalRead += read;
                    Logger.LogInfo($"[PeerConnection] Read {totalRead}/68 bytes from {EndPoint}");
                }

                // Проверяем protocol string согласно спецификации BitTorrent
                if (buffer[0] != 19)
                {
                    Logger.LogWarning($"[PeerConnection] Invalid protocol length from {EndPoint}: expected 19, got {buffer[0]}");
                    return false;
                }
                var protocol = Encoding.ASCII.GetString(buffer, 1, 19);
                if (protocol != "BitTorrent protocol")
                {
                    Logger.LogWarning($"[PeerConnection] Invalid protocol string from {EndPoint}: '{protocol}'");
                    return false;
                }
                Logger.LogInfo($"[PeerConnection] Protocol string verified: '{protocol}'");

                // Reserved bytes (байты 20-27) - проверяем поддержку расширений протокола
                _remoteReservedBytes = new byte[8];
                Array.Copy(buffer, 20, _remoteReservedBytes, 0, 8);
                
                // Читаем флаги из reserved bytes
                var remoteFlags = GetReservedBytesFlags(_remoteReservedBytes);
                
                // Проверяем поддержку различных расширений
                if (remoteFlags.HasFlag(ReservedBytesFlags.ExtensionProtocol))
                {
                    Logger.LogInfo($"[PeerConnection] Peer {EndPoint} supports Extension Protocol");
                }
                if (remoteFlags.HasFlag(ReservedBytesFlags.DHT))
                {
                    Logger.LogInfo($"[PeerConnection] Peer {EndPoint} supports DHT");
                }
                if (remoteFlags.HasFlag(ReservedBytesFlags.FastPeers))
                {
                    Logger.LogInfo($"[PeerConnection] Peer {EndPoint} supports Fast Peers");
                }
                if (remoteFlags.HasFlag(ReservedBytesFlags.NATTraversal))
                {
                    Logger.LogInfo($"[PeerConnection] Peer {EndPoint} supports NAT Traversal");
                }

                // Проверяем info hash (байты 28-47)
                var receivedInfoHash = new byte[20];
                Array.Copy(buffer, 28, receivedInfoHash, 0, 20);
                var receivedInfoHashStr = Convert.ToHexString(receivedInfoHash);
                if (receivedInfoHashStr != _infoHash)
                {
                    Logger.LogWarning($"[PeerConnection] Info hash mismatch from {EndPoint}: expected {_infoHash}, got {receivedInfoHashStr}");
                    return false;
                }
                Logger.LogInfo($"[PeerConnection] Info hash verified: {receivedInfoHashStr}");
                
                // Peer ID (байты 48-67) - можно сохранить для идентификации пира
                // Пока не используем, но структура правильная

                return true;
            }
            catch (SocketException ex)
            {
                Logger.LogWarning($"[PeerConnection] Socket error receiving handshake from {EndPoint}: {ex.SocketErrorCode} - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PeerConnection] Error receiving handshake from {EndPoint}", ex);
                return false;
            }
        }

        private async Task ReceiveMessagesLoopAsync(CancellationToken cancellationToken)
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isConnected)
                {
                    stream = _stream; // Проверяем актуальность
                    if (stream == null) break;
                    
                    // Читаем длину сообщения (4 байта, big-endian) согласно спецификации
                    var lengthBytes = new byte[4];
                    var read = await TaskTimeoutHelper.TimeoutAsync(
                        stream.ReadAsync(lengthBytes.AsMemory(0, 4), cancellationToken),
                        TimeSpan.FromSeconds(30));
                    if (read != 4) break;

                    var messageLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes, 0));
                    
                    // Keep-alive: <len=0000> согласно спецификации BitTorrent
                    if (messageLength == 0)
                    {
                        continue;
                    }

                    // Проверка на разумный размер сообщения (максимум 16MB для piece сообщений)
                    if (messageLength < 0 || messageLength > 16 * 1024 * 1024)
                    {
                        Logger.LogWarning($"Invalid message length from {EndPoint}: {messageLength}");
                        break;
                    }

                    // Читаем тело сообщения
                    var message = new byte[messageLength];
                    var totalRead = 0;
                    while (totalRead < messageLength)
                    {
                        stream = _stream; // Проверяем актуальность
                        if (stream == null) break;
                        
                        var bytesRead = await TaskTimeoutHelper.TimeoutAsync(
                            stream.ReadAsync(message.AsMemory(totalRead, messageLength - totalRead), cancellationToken),
                            TimeSpan.FromSeconds(30));
                        if (bytesRead == 0) break;
                        totalRead += bytesRead;
                    }

                    if (totalRead < messageLength)
                    {
                        Logger.LogWarning($"Incomplete message from {EndPoint}: read {totalRead}/{messageLength} bytes");
                        break;
                    }

                    await ProcessMessageAsync(message);
                }
            }
            catch (SocketException ex)
            {
                // Таймауты и разрывы соединения - нормальная ситуация
                if (ex.SocketErrorCode != SocketError.TimedOut && 
                    ex.SocketErrorCode != SocketError.ConnectionReset &&
                    ex.SocketErrorCode != SocketError.Shutdown)
                {
                    Logger.LogWarning($"Socket error in message loop for {EndPoint}: {ex.SocketErrorCode} - {ex.Message}");
                }
            }
            catch (TimeoutException)
            {
                Logger.LogWarning($"Timeout in message loop for {EndPoint}");
            }
            catch (OperationCanceledException)
            {
                // Нормальная отмена, не нужно логировать
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in message loop for {EndPoint}", ex);
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task ProcessMessageAsync(byte[] message)
        {
            if (message.Length == 0) return;

            var messageId = message[0];

            switch (messageId)
            {
                case 0: // Choke (блокировка)
                    _peerChoked = true;
                    Logger.LogInfo($"Received Choke from {EndPoint} - peer stopped sending data");
                    break;

                case 1: // Unchoke (разблокировка)
                    _peerChoked = false;
                    Logger.LogInfo($"Received Unchoke from {EndPoint} - peer is now ready to send data");
                    break;

                case 2: // Interested (заинтересован)
                    _peerInterested = true;
                    Logger.LogInfo($"Received Interested from {EndPoint}");
                    // Когда пир отправляет Interested, отвечаем Unchoke
                    // чтобы пир мог отправлять Request
                    if (_isChoked)
                    {
                        await SendUnchokeAsync();
                    }
                    break;

                case 3: // Not interested (не заинтересован)
                    _peerInterested = false;
                    Logger.LogInfo($"Received Not Interested from {EndPoint}");
                    // Если пир больше не заинтересован, можем отправить Choke для экономии ресурсов
                    if (!_isChoked)
                    {
                        await SendChokeAsync();
                    }
                    break;

                case 4: // Have: <len=0005><id=4><индекс куска>
                    if (message.Length >= 5)
                    {
                        var pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 1));
                        if (pieceIndex >= 0 && pieceIndex < _bitField.Length)
                        {
                            // Обновляем bitfield пира
                            if (_peerBitField == null)
                            {
                                // Если bitfield еще не получен, создаем его (пир отправляет Have вместо Bitfield)
                                _peerBitField = new BitField(_bitField.Length);
                                Logger.LogInfo($"Creating peer bitfield from Have message for {EndPoint} (bitfield not received yet)");
                            }
                            
                            if (pieceIndex < _peerBitField.Length && !_peerBitField[pieceIndex])
                            {
                                _peerBitField[pieceIndex] = true;
                                Logger.LogInfo($"Peer {EndPoint} now has piece {pieceIndex} (total: {_peerBitField.SetCount})");
                                
                                // Уведомляем о получении Have сообщения
                                if (_callbacks != null)
                                {
                                    var args = new HaveEventArgs { PieceIndex = pieceIndex };
                                    _ = Task.Run(async () => await _callbacks.OnHaveReceivedAsync(args).ConfigureAwait(false));
                                }
                                
                                // Если мы еще не отправили Interested и у пира есть нужные куски, отправляем Interested
                                if (!_isInterested && _bitField != null)
                                {
                                    if (_peerBitField[pieceIndex] && !_bitField[pieceIndex])
                                    {
                                        await SendInterestedAsync();
                                        Logger.LogInfo($"Auto-sent Interested to {EndPoint} after receiving Have for needed piece {pieceIndex}");
                                    }
                                }
                            }
                        }
                    }
                    break;

                case 5: // Bitfield: <len=0001+X><id=5><битовое поле>
                    // Bitfield должен быть первым сообщением после handshake
                    if (message.Length > 1)
                    {
                        var bitfieldBytes = new byte[message.Length - 1];
                        Array.Copy(message, 1, bitfieldBytes, 0, bitfieldBytes.Length);
                        
                        // Создаем BitField из полученных данных согласно спецификации
                        // Bitfield кодируется как байты, где каждый бит представляет наличие куска
                        if (_bitField != null)
                        {
                            _peerBitField = new BitField(_bitField.Length);
                            _peerBitField.FromByteArray(bitfieldBytes);
                            Logger.LogInfo($"Received Bitfield from {EndPoint}, {_peerBitField.SetCount} pieces available");
                            
                            // Уведомляем о получении Bitfield через колбэк
                            if (_callbacks != null)
                            {
                                _ = Task.Run(async () => await _callbacks.OnPeerBitfieldUpdatedAsync().ConfigureAwait(false));
                            }
                            
                            // Автоматически отправляем Interested, если у пира есть нужные куски
                            // Проверяем, есть ли у пира хотя бы один кусок, которого нет у нас
                            bool hasNeededPieces = false;
                            for (int i = 0; i < _peerBitField.Length && i < _bitField.Length; i++)
                            {
                                if (_peerBitField[i] && !_bitField[i])
                                {
                                    hasNeededPieces = true;
                                    break;
                                }
                            }
                            
                            if (hasNeededPieces && !_isInterested)
                            {
                                await SendInterestedAsync();
                                Logger.LogInfo($"Auto-sent Interested to {EndPoint} after receiving Bitfield (peer has needed pieces)");
                            }
                            else if (!hasNeededPieces)
                            {
                                // Отправляем Not Interested, если у пира нет нужных кусков
                                if (_isInterested)
                                {
                                    await SendNotInterestedAsync();
                                }
                                Logger.LogInfo($"Peer {EndPoint} has no needed pieces (we have all pieces they have)");
                            }
                        }
                    }
                    break;

                case 6: // Request: <len=0013><id=6><индекс><начало><длина>
                    // Пир запрашивает кусок у нас
                    // Согласно спецификации: 4 байта index, 4 байта begin, 4 байта length
                    if (message.Length >= 13)
                    {
                        var pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 1));
                        var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 5));
                        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 9));
                        
                        // Проверяем, что мы не choked и пир заинтересован
                        if (!_isChoked && _peerInterested)
                        {
                            Logger.LogInfo($"Received Request from {EndPoint}: piece={pieceIndex}, begin={begin}, length={length}");
                            if (_callbacks != null)
                            {
                                var args = new RequestEventArgs
                                {
                                    PieceIndex = pieceIndex,
                                    Begin = begin,
                                    Length = length
                                };
                                _ = Task.Run(async () => await _callbacks.OnRequestReceivedAsync(args).ConfigureAwait(false));
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"Ignoring Request from {EndPoint}: choked={_isChoked}, interested={_peerInterested}");
                        }
                    }
                    break;

                case 7: // Piece (кусок)
                    await HandlePieceMessage(message);
                    break;

                case 8: // Cancel (отмена)
                    break;

                case 20: // Расширенное сообщение: <len=0001+X><id=20><id_расширения><данные>
                    if (message.Length >= 2)
                    {
                        var extId = message[1];
                        var payload = new byte[message.Length - 2];
                        Array.Copy(message, 2, payload, 0, payload.Length);
                        
                        // ID расширения 0 = extension handshake
                        if (extId == 0 && _peerExchange != null)
                        {
                            try
                            {
                                // Парсим extension handshake (bencoded dictionary)
                                var extensions = ParseBencodeDictionary(payload);
                                if (extensions != null)
                                {
                                    _peerExchange.HandleExtensionHandshake(extensions);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"[PeerConnection] Error parsing extension handshake from {EndPoint}", ex);
                            }
                        }
                        // Обрабатываем PEX сообщения (extId != 0)
                        else if (_peerExchange != null && extId != 0)
                        {
                            _peerExchange.HandlePexMessage(payload);
                        }
                    }
                    break;
            }
        }

        // Piece: <len=0009+X><id=7><index><begin><block>
        // Согласно спецификации: 4 байта index, 4 байта begin, X байт block data
        private Task HandlePieceMessage(byte[] message)
        {
            if (message.Length < 9) return Task.CompletedTask; // Минимум 1 байт id + 4 байта index + 4 байта begin

            var pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 1));
            var begin = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(message, 5));
            var blockLength = message.Length - 9; // Общая длина минус id (1) + index (4) + begin (4)
            var block = new byte[blockLength];
            Array.Copy(message, 9, block, 0, blockLength);

            if (_callbacks != null)
            {
                var args = new PieceDataEventArgs
                {
                    PieceIndex = pieceIndex,
                    Begin = begin,
                    Data = block
                };
                _ = Task.Run(async () => await _callbacks.OnPieceReceivedAsync(args).ConfigureAwait(false));
            }
            return Task.CompletedTask;
        }

        public async Task SendInterestedAsync()
        {
            if (!_isConnected || _isInterested) return;
            await SendMessageAsync(new byte[] { 2 }); // Interested
            _isInterested = true;
            Logger.LogInfo($"Sent Interested to {EndPoint}");
        }

        public async Task SendNotInterestedAsync()
        {
            if (!_isConnected) return;
            await SendMessageAsync(new byte[] { 3 }); // Not Interested
            _isInterested = false;
            Logger.LogInfo($"Sent Not Interested to {EndPoint}");
        }

        // Unchoke: <len=0001><id=1>
        // Отправляем Unchoke пиру, чтобы он мог отправлять Request
        public async Task SendUnchokeAsync()
        {
            if (!_isConnected || !_isChoked) return;
            await SendMessageAsync(new byte[] { 1 }); // Unchoke
            _isChoked = false;
            Logger.LogInfo($"Sent Unchoke to {EndPoint}");
        }

        // Choke: <len=0001><id=0>
        // Отправляем Choke пиру, чтобы остановить отправку данных
        public async Task SendChokeAsync()
        {
            if (!_isConnected || _isChoked) return;
            await SendMessageAsync(new byte[] { 0 }); // Choke
            _isChoked = true;
            Logger.LogInfo($"Sent Choke to {EndPoint}");
        }

        // Bitfield: <len=0001+X><id=5><bitfield>
        // Отправляем наш bitfield пиру, чтобы он знал, какие куски у нас есть
        // Согласно спецификации BitTorrent, Bitfield должен быть отправлен даже если все куски равны 0
        public async Task SendBitfieldAsync()
        {
            if (!_isConnected || _bitField == null) return;

            try
            {
                var bitfieldBytes = _bitField.ToByteArray();
                // Bitfield должен быть отправлен даже если все куски равны 0 (пустой bitfield)
                // Минимальный размер bitfield - 1 байт (даже если все куски равны 0)
                if (bitfieldBytes.Length == 0)
                {
                    // Если bitfield пустой, создаем минимальный bitfield (1 байт с нулями)
                    bitfieldBytes = new byte[1];
                }
                
                var message = new byte[1 + bitfieldBytes.Length];
                message[0] = 5; // ID сообщения Bitfield
                Array.Copy(bitfieldBytes, 0, message, 1, bitfieldBytes.Length);

                await SendMessageAsync(message);
                Logger.LogInfo($"Sent Bitfield to {EndPoint}, {_bitField.SetCount}/{_bitField.Length} pieces available");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending bitfield to {EndPoint}", ex);
            }
        }

        // Request: <len=0013><id=6><index><begin><length>
        // Согласно спецификации: 4 байта index, 4 байта begin, 4 байта length
        public async Task SendRequestAsync(int pieceIndex, int begin, int length)
        {
            if (!_isConnected || _peerChoked) return;

            // Проверяем, что пир имеет этот кусок согласно его bitfield
            if (_peerBitField != null && pieceIndex < _peerBitField.Length && !_peerBitField[pieceIndex])
            {
                Logger.LogInfo($"Peer {EndPoint} doesn't have piece {pieceIndex}");
                return;
            }

            var message = new byte[13]; // 1 байт id + 4 байта index + 4 байта begin + 4 байта length
            message[0] = 6; // ID сообщения Request
            var indexBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(pieceIndex));
            Array.Copy(indexBytes, 0, message, 1, 4);
            var beginBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(begin));
            Array.Copy(beginBytes, 0, message, 5, 4);
            var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
            Array.Copy(lengthBytes, 0, message, 9, 4);

            await SendMessageAsync(message);
        }

        // Piece: <len=0009+X><id=7><index><begin><block>
        // Отправляем кусок данных пиру в ответ на Request
        public async Task SendPieceAsync(int pieceIndex, int begin, byte[] blockData)
        {
            if (!_isConnected) return;
            
            // Отправляем Piece только если:
            // 1. Мы не choked пира (мы должны были отправить Unchoke)
            // 2. Пир заинтересован (отправил Interested)
            if (_isChoked)
            {
                Logger.LogWarning($"Cannot send Piece to {EndPoint}: we choked the peer");
                return;
            }
            
            if (!_peerInterested)
            {
                Logger.LogWarning($"Cannot send Piece to {EndPoint}: peer is not interested");
                return;
            }
            
            var message = new byte[9 + blockData.Length]; // 1 байт id + 4 байта index + 4 байта begin + X байт data
            message[0] = 7; // ID сообщения Piece
            var indexBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(pieceIndex));
            Array.Copy(indexBytes, 0, message, 1, 4);
            var beginBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(begin));
            Array.Copy(beginBytes, 0, message, 5, 4);
            Array.Copy(blockData, 0, message, 9, blockData.Length);
            
            await SendMessageAsync(message);
            Logger.LogInfo($"Sent Piece to {EndPoint}: piece={pieceIndex}, begin={begin}, length={blockData.Length}");
        }

        internal async Task SendMessageAsync(byte[] message)
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null || !_isConnected) return;

            await _sendSemaphore.WaitAsync();
            try
            {
                stream = _stream; // Проверяем актуальность после получения блокировки
                if (stream == null || !_isConnected) return;
                
                var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(message.Length));
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(lengthBytes),
                    TimeSpan.FromSeconds(30));
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(message),
                    TimeSpan.FromSeconds(30));
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.FlushAsync(),
                    TimeSpan.FromSeconds(5));
            }
            catch (SocketException ex)
            {
                // Таймауты и разрывы соединения - нормальная ситуация
                if (ex.SocketErrorCode != SocketError.TimedOut && 
                    ex.SocketErrorCode != SocketError.ConnectionReset &&
                    ex.SocketErrorCode != SocketError.Shutdown)
                {
                    Logger.LogWarning($"Socket error sending message to {EndPoint}: {ex.SocketErrorCode} - {ex.Message}");
                }
                Disconnect();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending message to {EndPoint}", ex);
                Disconnect();
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        public void Disconnect()
        {
            // КРИТИЧНО: Закрываем соединение даже если _isConnected еще false (например, при ошибке подключения)
            if (!_isConnected && _tcpClient == null && _stream == null) return;

            _isConnected = false;
            _cancellationTokenSource?.Cancel();
            
            if (_readTask != null)
            {
                try
                {
                    _readTask.Wait(TimeSpan.FromSeconds(1));
                }
                catch (TimeoutException) { }
                catch (AggregateException) { }
                catch (OperationCanceledException) { }
                _readTask = null;
            }

            // КРИТИЧНО: Гарантируем закрытие всех сетевых ресурсов даже при ошибках
            try
            {
                _stream?.Close();
            }
            catch { }
            
            try
            {
                _tcpClient?.Close();
            }
            catch { }

            try
            {
                _stream?.Dispose();
            }
            catch { }
            _stream = null;

            try
            {
                _tcpClient?.Dispose();
            }
            catch { }
            _tcpClient = null;

            // Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null)
            {
                _ = Task.Run(async () => await callbacks.OnConnectionClosedAsync().ConfigureAwait(false));
            }
        }
        
        private void ClearEvents()
        {
            _callbacks = null;
            
            // Очищаем колбэки PeerExchange перед очисткой ссылки
            if (_peerExchange != null)
            {
                _peerExchange.SetCallbacks(null!);
                _peerExchange = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
            ClearEvents();
            _sendSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            _isConnected = false;
            _cancellationTokenSource?.Cancel();

            if (_readTask != null)
            {
                try
                {
                    await _readTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[PeerConnection] Ошибка ожидания завершения задачи чтения: {ex.Message}");
                }
                _readTask = null;
            }

            if (_stream != null)
            {
                try
                {
                    await _stream.DisposeAsync().ConfigureAwait(false);
                }
                catch { }
                _stream = null;
            }

            _tcpClient?.Dispose();
            _tcpClient = null;
            
            // КРИТИЧНО: Освобождаем PeerExchange перед очисткой колбэков
            if (_peerExchange != null)
            {
                try
                {
                    _peerExchange.SetCallbacks(null!);
                }
                catch { }
                _peerExchange = null;
            }
            
            ClearEvents();
            _sendSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Устанавливает reserved bytes в handshake на основе флагов
        /// </summary>
        /// <param name="handshake">Массив handshake (68 байт)</param>
        /// <param name="flags">Битовые флаги для установки</param>
        private static void SetReservedBytes(byte[] handshake, ReservedBytesFlags flags)
        {
            // Инициализируем все reserved bytes нулями
            for (int i = 20; i < 28; i++)
            {
                handshake[i] = 0x00;
            }
            
            // Bit 63 (byte[20], bit 0): Extension Protocol
            if (flags.HasFlag(ReservedBytesFlags.ExtensionProtocol))
            {
                handshake[20] |= 0x01; // bit 0
            }
            
            // Bit 64 (byte[27], bit 0): DHT
            if (flags.HasFlag(ReservedBytesFlags.DHT))
            {
                handshake[27] |= 0x01; // bit 0
            }
            
            // Bit 62 (byte[27], bit 2): Fast Peers
            if (flags.HasFlag(ReservedBytesFlags.FastPeers))
            {
                handshake[27] |= 0x04; // bit 2
            }
            
            // Bit 61 (byte[27], bit 3): NAT Traversal
            if (flags.HasFlag(ReservedBytesFlags.NATTraversal))
            {
                handshake[27] |= 0x08; // bit 3
            }
            
            // Bit 60 (byte[27], bit 4): Azureus Messaging
            if (flags.HasFlag(ReservedBytesFlags.AzureusMessaging))
            {
                handshake[27] |= 0x10; // bit 4
            }
        }
        
        /// <summary>
        /// Читает битовые флаги из reserved bytes
        /// </summary>
        /// <param name="reservedBytes">Массив из 8 байт reserved bytes</param>
        /// <returns>Битовые флаги</returns>
        private static ReservedBytesFlags GetReservedBytesFlags(byte[] reservedBytes)
        {
            if (reservedBytes == null || reservedBytes.Length < 8)
                return ReservedBytesFlags.None;
            
            ReservedBytesFlags flags = ReservedBytesFlags.None;
            
            // Bit 63 (byte[20], bit 0): Extension Protocol
            if ((reservedBytes[0] & 0x01) != 0)
            {
                flags |= ReservedBytesFlags.ExtensionProtocol;
            }
            
            // Bit 64 (byte[27], bit 0): DHT
            if ((reservedBytes[7] & 0x01) != 0)
            {
                flags |= ReservedBytesFlags.DHT;
            }
            
            // Bit 62 (byte[27], bit 2): Fast Peers
            if ((reservedBytes[7] & 0x04) != 0)
            {
                flags |= ReservedBytesFlags.FastPeers;
            }
            
            // Bit 61 (byte[27], bit 3): NAT Traversal
            if ((reservedBytes[7] & 0x08) != 0)
            {
                flags |= ReservedBytesFlags.NATTraversal;
            }
            
            // Bit 60 (byte[27], bit 4): Azureus Messaging
            if ((reservedBytes[7] & 0x10) != 0)
            {
                flags |= ReservedBytesFlags.AzureusMessaging;
            }
            
            return flags;
        }
        
        /// <summary>
        /// Парсит bencoded dictionary для extension handshake
        /// </summary>
        private static Dictionary<string, object>? ParseBencodeDictionary(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            int pos = 0;
            var result = DecodeValue(data, ref pos) as Dictionary<string, object>;
            return result;
        }

        private static object? DecodeValue(byte[] data, ref int pos)
        {
            if (pos >= data.Length)
                return null;

            if (data[pos] == 'd')
            {
                // Dictionary
                pos++;
                Dictionary<string, object> dict = [];
                while (pos < data.Length && data[pos] != 'e')
                {
                    var key = DecodeString(data, ref pos);
                    if (key == null) break;
                    var value = DecodeValue(data, ref pos);
                    if (value != null)
                        dict[key] = value;
                }
                if (pos < data.Length && data[pos] == 'e')
                    pos++;
                return dict;
            }
            else if (data[pos] == 'i')
            {
                // Integer
                pos++;
                int start = pos;
                while (pos < data.Length && data[pos] != 'e')
                    pos++;
                if (pos >= data.Length || data[pos] != 'e')
                    return null;
                var intStr = Encoding.UTF8.GetString(data, start, pos - start);
                pos++; // skip 'e'
                if (long.TryParse(intStr, out var intValue))
                    return intValue;
                return null;
            }
            else if (data[pos] >= '0' && data[pos] <= '9')
            {
                // String
                return DecodeString(data, ref pos);
            }

            return null;
        }

        private static string? DecodeString(byte[] data, ref int pos)
        {
            int lengthStart = pos;
            while (pos < data.Length && data[pos] >= '0' && data[pos] <= '9')
            {
                pos++;
            }
            
            if (pos >= data.Length || data[pos] != ':')
                return null;
            
            int length = 0;
            for (int i = lengthStart; i < pos; i++)
            {
                length = length * 10 + (data[i] - '0');
            }
            
            pos++; // skip ':'
            
            if (pos + length > data.Length)
                return null;

            var str = Encoding.UTF8.GetString(data, pos, length);
            pos += length;
            return str;
        }
    }
}

