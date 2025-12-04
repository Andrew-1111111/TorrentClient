using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace TorrentClient.Utilities
{
    /// <summary>
    /// Логгер приложения с поддержкой включения/отключения
    /// Использует Channel и паттерн Producer/Consumer для асинхронной записи
    /// </summary>
    public static class Logger
    {
        #region Поля

        private static Channel<LogEntry>? _channel;
        private static Task? _consumerTask;
        private static CancellationTokenSource? _cts;
        private static string? _logDirectory;
        private static StreamWriter? _writer;
        private static string? _currentLogDate;
        private static volatile bool _initialized;

        #endregion

        #region Свойства

        /// <summary>Включено ли логирование</summary>
        public static bool IsEnabled { get; set; } = true;

        #endregion

        #region Структура записи лога

        private readonly struct LogEntry
        {
            public string Message { get; init; }
            public Exception? Exception { get; init; }
            public string MemberName { get; init; }
            public string SourceFilePath { get; init; }
            public int SourceLineNumber { get; init; }
            public DateTime Timestamp { get; init; }
        }

        #endregion

        #region Инициализация

        /// <summary>Инициализация логгера</summary>
        public static void Initialize(string? logDirectory = null)
        {
            if (_initialized) return;

            _logDirectory = logDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);

            // Создаём bounded channel для контроля памяти (макс 10000 записей в очереди)
            _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.DropOldest, // При переполнении отбрасываем старые записи
                SingleReader = true,
                SingleWriter = false
            });

            _cts = new CancellationTokenSource();
            
            // Запускаем consumer в фоновом потоке
            _consumerTask = Task.Run(() => ConsumerLoopAsync(_cts.Token));
            
            _initialized = true;
        }

        /// <summary>Включить/отключить логирование</summary>
        public static void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>Закрыть логгер и освободить ресурсы</summary>
        public static void Close()
        {
            CloseAsync().GetAwaiter().GetResult();
        }

        /// <summary>Асинхронно закрыть логгер и освободить ресурсы</summary>
        public static async Task CloseAsync()
        {
            if (!_initialized) return;
            _initialized = false;

            // Завершаем канал записи
            _channel?.Writer.TryComplete();

            // Даём время consumer обработать оставшиеся записи (макс 2 секунды)
            if (_consumerTask != null)
            {
                try
                {
                    await _consumerTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
            }

            // Отменяем и освобождаем ресурсы
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // Закрываем writer
            if (_writer != null)
            {
                try
                {
                    await _writer.FlushAsync().ConfigureAwait(false);
                    await _writer.DisposeAsync().ConfigureAwait(false);
                }
                catch { }
                _writer = null;
            }

            _channel = null;
            _consumerTask = null;
        }

        #endregion

        #region Consumer (обработчик очереди)

        private static async Task ConsumerLoopAsync(CancellationToken cancellationToken)
        {
            if (_channel == null) return;

            try
            {
                await foreach (var entry in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        await WriteEntryAsync(entry).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Игнорируем ошибки записи отдельных записей
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение
            }
            catch (ChannelClosedException)
            {
                // Канал закрыт - завершаем
            }
        }

        private static async Task WriteEntryAsync(LogEntry entry)
        {
            EnsureWriter();

            if (_writer == null) return;

            var logText = BuildLogEntry(entry);
            await _writer.WriteLineAsync(logText).ConfigureAwait(false);
            await _writer.WriteLineAsync().ConfigureAwait(false);
        }

        #endregion

        #region Вспомогательные методы

        private static void EnsureWriter()
        {
            if (_logDirectory == null)
            {
                _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(_logDirectory);
            }

            // ОПТИМИЗАЦИЯ: Используем UtcNow для лучшей производительности
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Если дата изменилась или writer не создан - создаём новый
            if (_currentLogDate != today || _writer == null)
            {
                // Закрываем старый writer
                if (_writer != null)
                {
                    try
                    {
                        _writer.Flush();
                        _writer.Dispose();
                    }
                    catch { }
                }

                var logPath = Path.Combine(_logDirectory, $"torrentclient_{today}.log");
                _writer = new StreamWriter(logPath, append: true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                _currentLogDate = today;
            }
        }

        private static string BuildLogEntry(LogEntry entry)
        {
            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var fileName = Path.GetFileName(entry.SourceFilePath);
            var builder = new StringBuilder();

            builder.Append($"{timestamp} [{fileName}:{entry.SourceLineNumber}] [{entry.MemberName}] {entry.Message}");

            if (entry.Exception != null)
            {
                builder.AppendLine();
                builder.Append($"  Тип: {entry.Exception.GetType().FullName}");
                builder.AppendLine();
                builder.Append($"  Сообщение: {entry.Exception.Message}");

                if (!string.IsNullOrWhiteSpace(entry.Exception.StackTrace))
                {
                    builder.AppendLine();
                    builder.Append("  Стек вызовов:");
                    builder.AppendLine();
                    builder.Append(entry.Exception.StackTrace);
                }

                if (entry.Exception.InnerException != null)
                {
                    builder.AppendLine();
                    builder.Append($"  Внутреннее исключение: {entry.Exception.InnerException.GetType().FullName} - {entry.Exception.InnerException.Message}");
                }
            }

            return builder.ToString();
        }

        /// <summary>Отправляет запись в очередь</summary>
        private static void EnqueueLog(string message, Exception? exception,
            string memberName, string sourceFilePath, int sourceLineNumber)
        {
            if (!IsEnabled || _channel == null) return;

            var entry = new LogEntry
            {
                Message = message,
                Exception = exception,
                MemberName = memberName,
                SourceFilePath = sourceFilePath,
                SourceLineNumber = sourceLineNumber,
                Timestamp = DateTime.Now
            };

            // TryWrite не блокирует - если канал полон, запись отбрасывается
            _channel.Writer.TryWrite(entry);
        }

        #endregion

        #region Синхронные методы логирования

        /// <summary>Записать сообщение в лог</summary>
        public static void Log(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog(message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>Записать ошибку в лог</summary>
        public static void LogError(string message, Exception? exception = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog($"[ОШИБКА] {message}", exception, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>Записать предупреждение в лог</summary>
        public static void LogWarning(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog($"[ПРЕДУПРЕЖДЕНИЕ] {message}", null, memberName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>Записать информационное сообщение в лог</summary>
        public static void LogInfo(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog($"[ИНФО] {message}", null, memberName, sourceFilePath, sourceLineNumber);
        }

        #endregion

        #region Асинхронные методы логирования

        /// <summary>Асинхронно записать сообщение в лог</summary>
        public static ValueTask LogAsync(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog(message, null, memberName, sourceFilePath, sourceLineNumber);
            return ValueTask.CompletedTask;
        }

        /// <summary>Асинхронно записать ошибку в лог</summary>
        public static ValueTask LogErrorAsync(string message, Exception? exception = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog($"[ОШИБКА] {message}", exception, memberName, sourceFilePath, sourceLineNumber);
            return ValueTask.CompletedTask;
        }

        /// <summary>Асинхронно записать предупреждение в лог</summary>
        public static ValueTask LogWarningAsync(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog($"[ПРЕДУПРЕЖДЕНИЕ] {message}", null, memberName, sourceFilePath, sourceLineNumber);
            return ValueTask.CompletedTask;
        }

        /// <summary>Асинхронно записать информационное сообщение в лог</summary>
        public static ValueTask LogInfoAsync(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            EnqueueLog($"[ИНФО] {message}", null, memberName, sourceFilePath, sourceLineNumber);
            return ValueTask.CompletedTask;
        }

        #endregion
    }
}
