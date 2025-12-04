using System.Buffers;
using System.Buffers.Binary;
using TorrentClient.Engine.Interfaces;
using TorrentClient.Utilities;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Реализация протокола BitTorrent Wire
    /// Основано на BEP 3: http://www.bittorrent.org/beps/bep_0003.html
    /// </summary>
    public class Wire : IDisposable, IAsyncDisposable
    {
        #region Константы протокола

        private const string ProtocolName = "BitTorrent protocol";
        private const int HandshakeLength = 68;
        private const int MaxMessageLength = 16 * 1024 * 1024;

        public const byte MsgChoke = 0;
        public const byte MsgUnchoke = 1;
        public const byte MsgInterested = 2;
        public const byte MsgNotInterested = 3;
        public const byte MsgHave = 4;
        public const byte MsgBitfield = 5;
        public const byte MsgRequest = 6;
        public const byte MsgPiece = 7;
        public const byte MsgCancel = 8;
        public const byte MsgPort = 9;
        public const byte MsgExtended = 20;

        #endregion

        #region Поля

        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        #endregion

        #region Свойства состояния

        public byte[] PeerId { get; private set; } = new byte[20];
        public byte[] InfoHash { get; private set; } = new byte[20];
        public byte[] RemotePeerId { get; private set; } = new byte[20];
        public byte[] Reserved { get; private set; } = new byte[8];
        public byte[] RemoteReserved { get; private set; } = new byte[8];

        public bool AmChoking { get; private set; } = true;
        public bool AmInterested { get; private set; } = false;
        public bool PeerChoking { get; private set; } = true;
        public bool PeerInterested { get; private set; } = false;

        public BitArray? PeerBitfield { get; private set; }
        public int PieceCount { get; private set; }

        public IPEndPoint? RemoteEndPoint { get; private set; }
        public bool IsConnected => _client?.Connected ?? false;
        public bool HandshakeComplete { get; private set; }

        public long Downloaded { get; private set; }
        public long Uploaded { get; private set; }
        public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

        #endregion

        #region Асинхронные колбэки (замена событий)

        private IWireCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(IWireCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        #region Вложенные классы

        public class PieceData
        {
            public int Index { get; set; }
            public int Begin { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        public class BlockRequest
        {
            public int Index { get; set; }
            public int Begin { get; set; }
            public int Length { get; set; }
        }

        #endregion

        #region Конструктор

        public Wire(byte[] peerId, byte[] infoHash, int pieceCount)
        {
            if (peerId.Length != 20) throw new ArgumentException("PeerId должен быть 20 байт");
            if (infoHash.Length != 20) throw new ArgumentException("InfoHash должен быть 20 байт");

            PeerId = peerId;
            InfoHash = infoHash;
            PieceCount = pieceCount;

            // Устанавливаем зарезервированные байты для расширений
            Reserved[5] = 0x10;
        }

        #endregion

        #region Подключение

        /// <summary>Подключается к пиру и выполняет рукопожатие</summary>
        public async Task<bool> ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default)
        {
            TcpClient? tempClient = null;
            try
            {
                RemoteEndPoint = endPoint;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                tempClient = new TcpClient
                {
                    NoDelay = true,                             // Отключаем Nagle для минимальной задержки (критично для pipelining!)
                    LingerState = new LingerOption(false, 0),    // LingerState = 0
                    ReceiveTimeout = 60000,
                    SendTimeout = 60000,
                    ReceiveBufferSize = 256 * 1024,             // 256KB буфер приёма
                    SendBufferSize = 256 * 1024                 // 256KB буфер отправки
                };
                _client = tempClient;

                await TaskTimeoutHelper.TimeoutAsync(
                    _client.ConnectAsync(endPoint.Address, endPoint.Port, _cts.Token),
                    TimeSpan.FromSeconds(30));
                _stream = _client.GetStream();
                tempClient = null; // Успешно подключились, не нужно закрывать

                Logger.LogInfo($"[Wire] TCP подключено к {endPoint}");

                await SendHandshakeAsync();

                if (!await ReceiveHandshakeAsync())
                {
                    Logger.LogWarning($"[Wire] Рукопожатие не удалось с {endPoint}");
                    Disconnect();
                    return false;
                }

                HandshakeComplete = true;
                Logger.LogInfo($"[Wire] Рукопожатие завершено с {endPoint}");

                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnConnectedAsync().ConfigureAwait(false));
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnHandshakeReceivedAsync().ConfigureAwait(false));
                }

                _readTask = ReadLoopAsync(_cts.Token);

                return true;
            }
            catch (TimeoutException)
            {
                Logger.LogInfo($"[Wire] Подключение к {endPoint} превысило таймаут");
                Disconnect();
                return false;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo($"[Wire] Подключение к {endPoint} отменено");
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Wire] Не удалось подключиться к {endPoint}: {ex.Message}");
                // КРИТИЧНО: Закрываем временный TcpClient если он был создан, но соединение не установлено
                if (tempClient != null)
                {
                    try
                    {
                        tempClient.Close();
                        tempClient.Dispose();
                    }
                    catch { }
                    _client = null;
                }
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(ex).ConfigureAwait(false));
                }
                Disconnect();
                return false;
            }
        }

        /// <summary>Принимает входящее соединение</summary>
        public async Task<bool> AcceptAsync(TcpClient client, CancellationToken cancellationToken = default)
        {
            try
            {
                _client = client;

                // Настраиваем параметры сокета для максимальной производительности
                _client.NoDelay = true;                  // Выключаем алгоритм Нагла (объединение мелких пакетов)
                _client.LingerState = new LingerOption(false, 0);  // LingerState = 0
                _client.ReceiveBufferSize = 256 * 1024;
                _client.SendBufferSize = 256 * 1024;
                
                _stream = client.GetStream();
                RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Logger.LogInfo($"[Wire] Принимаем соединение от {RemoteEndPoint}");

                if (!await ReceiveHandshakeAsync())
                {
                    Logger.LogWarning($"[Wire] Входящее рукопожатие не удалось от {RemoteEndPoint}");
                    Disconnect();
                    return false;
                }

                await SendHandshakeAsync();

                HandshakeComplete = true;
                Logger.LogInfo($"[Wire] Входящее рукопожатие завершено с {RemoteEndPoint}");

                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnConnectedAsync().ConfigureAwait(false));
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnHandshakeReceivedAsync().ConfigureAwait(false));
                }

                _readTask = ReadLoopAsync(_cts.Token);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Wire] Не удалось принять соединение: {ex.Message}");
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(ex).ConfigureAwait(false));
                }
                Disconnect();
                return false;
            }
        }

        #endregion

        #region Рукопожатие

        private async Task SendHandshakeAsync()
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null) throw new InvalidOperationException("Не подключено");

            var handshake = new byte[HandshakeLength];
            handshake[0] = 19;

            Encoding.ASCII.GetBytes(ProtocolName, 0, 19, handshake, 1);
            Array.Copy(Reserved, 0, handshake, 20, 8);
            Array.Copy(InfoHash, 0, handshake, 28, 20);
            Array.Copy(PeerId, 0, handshake, 48, 20);

            await _writeLock.WaitAsync();
            try
            {
                stream = _stream; // Проверяем актуальность после получения блокировки
                if (stream == null) return;
                
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(handshake),
                    TimeSpan.FromSeconds(10));
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.FlushAsync(),
                    TimeSpan.FromSeconds(5));
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task<bool> ReceiveHandshakeAsync()
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null) return false;

            var buffer = new byte[HandshakeLength];
            var totalRead = 0;

            while (totalRead < HandshakeLength)
            {
                stream = _stream; // Проверяем актуальность
                if (stream == null) return false;
                
                var read = await TaskTimeoutHelper.TimeoutAsync(
                    stream.ReadAsync(buffer, totalRead, HandshakeLength - totalRead),
                    TimeSpan.FromSeconds(10));
                if (read == 0) return false;
                totalRead += read;
            }

            if (buffer[0] != 19) return false;

            var protocol = Encoding.ASCII.GetString(buffer, 1, 19);
            if (protocol != ProtocolName) return false;

            Array.Copy(buffer, 20, RemoteReserved, 0, 8);

            var remoteInfoHash = new byte[20];
            Array.Copy(buffer, 28, remoteInfoHash, 0, 20);

            if (!InfoHash.AsSpan().SequenceEqual(remoteInfoHash))
            {
                Logger.LogWarning("[Wire] Несовпадение InfoHash");
                return false;
            }

            Array.Copy(buffer, 48, RemotePeerId, 0, 20);

            return true;
        }

        #endregion

        #region Цикл чтения

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null) return;

            var lengthBuffer = new byte[4];

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    var read = await ReadExactAsync(lengthBuffer, 4, cancellationToken);
                    if (read != 4) break;

                    var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

                    if (length == 0)
                    {
                        LastActivity = DateTime.UtcNow;
                        continue;
                    }

                    if (length < 0 || length > MaxMessageLength)
                    {
                        Logger.LogWarning($"[Wire] Неверная длина сообщения: {length}");
                        break;
                    }

                    // Используем ArrayPool для буфера сообщения
                    var message = ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        read = await ReadExactAsync(message, length, cancellationToken);
                        if (read != length) break;

                        LastActivity = DateTime.UtcNow;
                        ProcessMessage(message.AsSpan(0, length));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(message);
                    }
                }
            }
            catch (TimeoutException)
            {
                Logger.LogWarning("[Wire] Таймаут при чтении данных");
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Wire] Ошибка чтения: {ex.Message}");
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(ex).ConfigureAwait(false));
                }
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task<int> ReadExactAsync(byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null) return 0;

            var totalRead = 0;
            while (totalRead < count)
            {
                stream = _stream; // Проверяем актуальность
                if (stream == null) return totalRead;
                
                var read = await TaskTimeoutHelper.TimeoutAsync(
                    stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), cancellationToken),
                    TimeSpan.FromSeconds(30));
                if (read == 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }

        #endregion

        #region Обработка сообщений

        private void ProcessMessage(ReadOnlySpan<byte> message)
        {
            if (message.Length == 0) return;

            var type = message[0];
            // Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;

            switch (type)
            {
                case MsgChoke:
                    PeerChoking = true;
                    if (callbacks != null)
                    {
                        SafeTaskRunner.RunSafe(async () => await callbacks.OnChokeAsync().ConfigureAwait(false));
                    }
                    break;

                case MsgUnchoke:
                    PeerChoking = false;
                    if (callbacks != null)
                    {
                        SafeTaskRunner.RunSafe(async () => await callbacks.OnUnchokeAsync().ConfigureAwait(false));
                    }
                    break;

                case MsgInterested:
                    PeerInterested = true;
                    if (callbacks != null)
                    {
                        SafeTaskRunner.RunSafe(async () => await callbacks.OnInterestedAsync().ConfigureAwait(false));
                    }
                    break;

                case MsgNotInterested:
                    PeerInterested = false;
                    if (callbacks != null)
                    {
                        SafeTaskRunner.RunSafe(async () => await callbacks.OnNotInterestedAsync().ConfigureAwait(false));
                    }
                    break;

                case MsgHave:
                    if (message.Length >= 5)
                    {
                        var index = BinaryPrimitives.ReadInt32BigEndian(message.Slice(1, 4));
                        if (PeerBitfield == null)
                        {
                            PeerBitfield = new BitArray(PieceCount);
                        }
                        if (index >= 0 && index < PieceCount)
                        {
                            PeerBitfield[index] = true;
                        }
                        // Используем уже захваченную ссылку callbacks из начала метода
                        if (callbacks != null)
                        {
                            var indexCopy = index; // Захватываем копию для лямбды
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnHaveAsync(indexCopy).ConfigureAwait(false));
                        }
                    }
                    break;

                case MsgBitfield:
                    if (message.Length > 1)
                    {
                        PeerBitfield = new BitArray(PieceCount);
                        PeerBitfield.FromSpan(message.Slice(1));
                        // Используем уже захваченную ссылку callbacks из начала метода
                        if (callbacks != null)
                        {
                            var bitfieldCopy = PeerBitfield; // Захватываем копию для лямбды
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnBitfieldAsync(bitfieldCopy).ConfigureAwait(false));
                        }
                    }
                    break;

                case MsgRequest:
                    if (message.Length >= 13)
                    {
                        var request = new BlockRequest
                        {
                            Index = BinaryPrimitives.ReadInt32BigEndian(message.Slice(1, 4)),
                            Begin = BinaryPrimitives.ReadInt32BigEndian(message.Slice(5, 4)),
                            Length = BinaryPrimitives.ReadInt32BigEndian(message.Slice(9, 4))
                        };
                        // Используем уже захваченную ссылку callbacks из начала метода
                        if (callbacks != null)
                        {
                            var requestCopy = request; // Захватываем копию для лямбды
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnRequestAsync(requestCopy).ConfigureAwait(false));
                        }
                    }
                    break;

                case MsgPiece:
                    if (message.Length >= 9)
                    {
                        // КРИТИЧНО: Используем прямое копирование для максимальной скорости
                        // message уже из ArrayPool в ReadLoopAsync, создаем только финальный массив
                        var dataLength = message.Length - 9;
                        var pieceData = new PieceData
                        {
                            Index = BinaryPrimitives.ReadInt32BigEndian(message.Slice(1, 4)),
                            Begin = BinaryPrimitives.ReadInt32BigEndian(message.Slice(5, 4)),
                            Data = message.Slice(9, dataLength).ToArray() // Одно копирование для передачи в событие
                        };
                        Downloaded += pieceData.Data.Length;
                        // Используем уже захваченную ссылку callbacks из начала метода
                        if (callbacks != null)
                        {
                            var pieceDataCopy = pieceData; // Захватываем копию для лямбды
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnPieceAsync(pieceDataCopy).ConfigureAwait(false));
                        }
                    }
                    break;

                case MsgCancel:
                    // Обработка отмены - не критично для загрузки
                    break;

                case MsgExtended:
                    if (message.Length > 1)
                    {
                        // Прямое копирование для максимальной скорости
                        var payload = message.Slice(1).ToArray();
                        // Используем уже захваченную ссылку callbacks из начала метода
                        if (callbacks != null)
                        {
                            var payloadCopy = payload; // Захватываем копию для лямбды
                            SafeTaskRunner.RunSafe(async () => await callbacks.OnExtendedAsync(payloadCopy).ConfigureAwait(false));
                        }
                    }
                    break;
            }
        }

        #endregion

        #region Методы отправки

        public async Task SendKeepAliveAsync()
        {
            await SendMessageAsync(Array.Empty<byte>());
        }

        public async Task SendChokeAsync()
        {
            AmChoking = true;
            await SendMessageAsync(new byte[] { MsgChoke });
        }

        public async Task SendUnchokeAsync()
        {
            AmChoking = false;
            await SendMessageAsync(new byte[] { MsgUnchoke });
        }

        public async Task SendInterestedAsync()
        {
            AmInterested = true;
            await SendMessageAsync(new byte[] { MsgInterested });
        }

        public async Task SendNotInterestedAsync()
        {
            AmInterested = false;
            await SendMessageAsync(new byte[] { MsgNotInterested });
        }

        public async Task SendHaveAsync(int index)
        {
            var message = new byte[5];
            message[0] = MsgHave;
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(1), index);
            await SendMessageAsync(message);
        }

        public async Task SendBitfieldAsync(BitArray bitfield)
        {
            var bytes = bitfield.ToBytes();
            var message = new byte[1 + bytes.Length];
            message[0] = MsgBitfield;
            Array.Copy(bytes, 0, message, 1, bytes.Length);
            await SendMessageAsync(message);
        }

        public async Task SendRequestAsync(int index, int begin, int length)
        {
            var message = new byte[13];
            message[0] = MsgRequest;
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(1), index);
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(5), begin);
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(9), length);
            await SendMessageAsync(message);
        }

        public Task SendPieceAsync(int index, int begin, byte[] data) 
            => SendPieceAsync(index, begin, data, 0, data.Length);

        public async Task SendPieceAsync(int index, int begin, byte[] sourceData, int sourceOffset, int length)
        {
            var messageLength = 9 + length;
            var message = ArrayPool<byte>.Shared.Rent(messageLength);
            try
            {
                message[0] = MsgPiece;
                BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(1), index);
                BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(5), begin);
                Array.Copy(sourceData, sourceOffset, message, 9, length);
                Uploaded += length;
                await SendMessageAsync(message.AsMemory(0, messageLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(message);
            }
        }

        public async Task SendCancelAsync(int index, int begin, int length)
        {
            var message = new byte[13];
            message[0] = MsgCancel;
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(1), index);
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(5), begin);
            BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(9), length);
            await SendMessageAsync(message);
        }

        private Task SendMessageAsync(byte[] message) => SendMessageAsync(message.AsMemory());

        private async Task SendMessageAsync(ReadOnlyMemory<byte> message)
        {
            var stream = _stream; // Локальная копия для потокобезопасности
            if (stream == null || !IsConnected) return;

            await _writeLock.WaitAsync();
            try
            {
                // Повторная проверка после получения блокировки
                stream = _stream;
                if (stream == null || !IsConnected) return;
                
                var lengthPrefix = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, message.Length);

                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(lengthPrefix),
                    TimeSpan.FromSeconds(30));
                if (message.Length > 0)
                {
                    await TaskTimeoutHelper.TimeoutAsync(
                        stream.WriteAsync(message),
                        TimeSpan.FromSeconds(30));
                }
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.FlushAsync(),
                    TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Wire] Ошибка отправки: {ex.Message}");
                // Захватываем ссылку в локальную переменную для предотвращения race condition
                var callbacks = _callbacks;
                if (callbacks != null)
                {
                    SafeTaskRunner.RunSafe(async () => await callbacks.OnErrorAsync(ex).ConfigureAwait(false));
                }
                Disconnect();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        #endregion

        #region Отключение

        public void Disconnect()
        {
            // КРИТИЧНО: Закрываем соединение даже если IsConnected еще false (например, при ошибке подключения)
            if (!IsConnected && _client == null && _stream == null) return;

            // КРИТИЧНО: Гарантируем закрытие всех сетевых ресурсов даже при ошибках
            try
            {
                _cts?.Cancel();
            }
            catch { }

            try
            {
                _stream?.Close();
            }
            catch { }

            try
            {
                _client?.Close();
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
                _client?.Dispose();
            }
            catch { }
            _client = null;

            // Захватываем ссылку в локальную переменную для предотвращения race condition
            var callbacks = _callbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnDisconnectedAsync().ConfigureAwait(false));
            }
        }

        public void Dispose()
        {
            Disconnect();
            
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
            
            ClearEvents();
            _writeLock.Dispose();
            _cts?.Dispose();
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            
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
                    Logger.LogWarning($"[Wire] Ошибка ожидания завершения задачи чтения: {ex.Message}");
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
            
            _client?.Dispose();
            _client = null;
            
            ClearEvents();
            _writeLock.Dispose();
            _cts?.Dispose();
            GC.SuppressFinalize(this);
        }
        
        private void ClearEvents()
        {
            _callbacks = null;
        }

        #endregion
    }

    /// <summary>
    /// Простой битовый массив для операций с битовым полем
    /// </summary>
    public class BitArray
    {
        private readonly bool[] _bits;
        private int _setCount;

        public BitArray(int length)
        {
            _bits = new bool[length];
        }

        public bool this[int index]
        {
            get => index >= 0 && index < _bits.Length && _bits[index];
            set
            {
                if (index < 0 || index >= _bits.Length) return;
                if (_bits[index] != value)
                {
                    _bits[index] = value;
                    _setCount += value ? 1 : -1;
                }
            }
        }

        public int Length => _bits.Length;
        public int SetCount => _setCount;
        public bool IsComplete => _setCount == _bits.Length;

        public void FromBytes(byte[] data, int offset = 0)
        {
            for (int i = 0; i < _bits.Length; i++)
            {
                int byteIndex = offset + (i / 8);
                int bitIndex = 7 - (i % 8);
                if (byteIndex < data.Length)
                {
                    this[i] = (data[byteIndex] & (1 << bitIndex)) != 0;
                }
            }
        }
        
        public void FromSpan(ReadOnlySpan<byte> data)
        {
            for (int i = 0; i < _bits.Length; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = 7 - (i % 8);
                if (byteIndex < data.Length)
                {
                    this[i] = (data[byteIndex] & (1 << bitIndex)) != 0;
                }
            }
        }

        public byte[] ToBytes()
        {
            int byteCount = (_bits.Length + 7) / 8;
            var bytes = new byte[byteCount];
            for (int i = 0; i < _bits.Length; i++)
            {
                if (_bits[i])
                {
                    int byteIndex = i / 8;
                    int bitIndex = 7 - (i % 8);
                    bytes[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
            return bytes;
        }

        /// <summary>Проверяет, есть ли у пира куски, которые нам нужны</summary>
        public bool HasPiecesWeNeed(BitArray ourBitfield)
        {
            for (int i = 0; i < Math.Min(Length, ourBitfield.Length); i++)
            {
                if (this[i] && !ourBitfield[i])
                    return true;
            }
            return false;
        }
    }
}
