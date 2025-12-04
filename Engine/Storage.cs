using System.Security;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Менеджер хранилища файлов торрента
    /// Обрабатывает чтение и запись кусков в несколько файлов
    /// </summary>
    public class Storage : IDisposable, IAsyncDisposable
    {
        #region Поля

        private readonly TorrentMetadata _metadata;
        private readonly string _downloadPath;
        private readonly Dictionary<string, FileStream> _fileStreams = new();
        private readonly Dictionary<string, DateTime> _fileStreamLastUsed = new(); // Отслеживание времени последнего использования
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _disposed;
        
        /// <summary>Максимальное количество одновременно открытых файлов (ограничение неуправляемой памяти)</summary>
        private const int MaxOpenFiles = 50;
        
        /// <summary>Время неиспользования файла перед закрытием (5 минут)</summary>
        private static readonly TimeSpan FileIdleTimeout = TimeSpan.FromMinutes(5);

        #endregion

        #region Свойства

        /// <summary>Путь загрузки</summary>
        public string DownloadPath => _downloadPath;

        #endregion

        #region Конструктор

        public Storage(TorrentMetadata metadata, string downloadPath)
        {
            _metadata = metadata;
            _downloadPath = Path.GetFullPath(downloadPath);
        }

        #endregion

        #region Безопасность путей

        /// <summary>
        /// Безопасно объединяет пути, предотвращая Path Traversal атаки
        /// </summary>
        private string GetSafeFullPath(string relativePath)
        {
            // Нормализуем путь и проверяем на опасные символы
            var normalized = relativePath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => part != "." && part != "..")
                .ToArray();
            
            var safePath = Path.Combine(normalized);
            var fullPath = Path.GetFullPath(Path.Combine(_downloadPath, safePath));
            
            // Проверяем, что результирующий путь внутри downloadPath
            if (!fullPath.StartsWith(_downloadPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException($"Попытка доступа за пределы директории загрузки: {relativePath}");
            }
            
            return fullPath;
        }

        #endregion

        #region Инициализация

        /// <summary>
        /// Инициализирует хранилище - создаёт директории и предвыделяет файлы
        /// </summary>
        public async Task InitializeAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var file in _metadata.Files)
                {
                    var fullPath = GetSafeFullPath(file.Path);
                    var directory = Path.GetDirectoryName(fullPath);

                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Создаём файл если не существует
                    if (!File.Exists(fullPath))
                    {
                        using var fs = File.Create(fullPath);
                        // Предвыделяем размер файла для лучшей производительности
                        if (file.Length > 0)
                        {
                            fs.SetLength(file.Length);
                        }
                        Logger.LogInfo($"[Storage] Создан файл: {file.Path} ({file.Length:N0} байт)");
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region Чтение

        /// <summary>Читает кусок из хранилища</summary>
        public async Task<byte[]?> ReadPieceAsync(int pieceIndex)
        {
            if (pieceIndex < 0 || pieceIndex >= _metadata.PieceCount)
                return null;

            var pieceLength = _metadata.GetPieceLength(pieceIndex);
            var pieceData = new byte[pieceLength];
            var bytesRead = 0;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var (file, fileOffset, pieceOffset, length) in _metadata.GetFilesForPiece(pieceIndex))
                {
                    var fullPath = GetSafeFullPath(file.Path);
                    if (!File.Exists(fullPath))
                    {
                        Logger.LogWarning($"[Storage] Файл не найден: {fullPath}");
                        return null;
                    }

                    var stream = await GetOrOpenFileAsync(fullPath, FileMode.Open, FileAccess.Read).ConfigureAwait(false);
                    stream.Seek(fileOffset, SeekOrigin.Begin);
                    var read = await TaskTimeoutHelper.TimeoutAsync(
                        stream.ReadAsync(pieceData.AsMemory(pieceOffset, length)),
                        TimeSpan.FromSeconds(60)).ConfigureAwait(false);
                    bytesRead += read;
                }
            }
            finally
            {
                _lock.Release();
            }

            return bytesRead == pieceLength ? pieceData : null;
        }

        #endregion

        #region Запись

        /// <summary>Записывает кусок в хранилище</summary>
        public async Task<bool> WritePieceAsync(int pieceIndex, byte[] data)
        {
            if (pieceIndex < 0 || pieceIndex >= _metadata.PieceCount)
                return false;

            var expectedLength = _metadata.GetPieceLength(pieceIndex);
            if (data.Length != expectedLength)
            {
                Logger.LogWarning($"[Storage] Несоответствие размера куска {pieceIndex}: ожидалось {expectedLength}, получено {data.Length}");
                return false;
            }

            // Проверяем хэш перед записью
            if (!_metadata.VerifyPiece(pieceIndex, data))
            {
                Logger.LogWarning($"[Storage] Проверка хэша куска {pieceIndex} не прошла");
                return false;
            }

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var (file, fileOffset, pieceOffset, length) in _metadata.GetFilesForPiece(pieceIndex))
                {
                    var fullPath = GetSafeFullPath(file.Path);
                    var directory = Path.GetDirectoryName(fullPath);

                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var stream = await GetOrOpenFileAsync(fullPath, FileMode.OpenOrCreate, FileAccess.Write).ConfigureAwait(false);
                    stream.Seek(fileOffset, SeekOrigin.Begin);
                    await TaskTimeoutHelper.TimeoutAsync(
                        stream.WriteAsync(data.AsMemory(pieceOffset, length)),
                        TimeSpan.FromSeconds(60)).ConfigureAwait(false);
                    // FlushAsync убран - будет вызван при закрытии потока или периодически ОС
                }

                Logger.LogInfo($"[Storage] Записан кусок {pieceIndex} ({data.Length} байт)");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Storage] Ошибка записи куска {pieceIndex}", ex);
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region Проверка

        /// <summary>
        /// Проверяет какие куски уже загружены и верифицированы
        /// </summary>
        public async Task<BitArray> VerifyExistingPiecesAsync(IProgress<double>? progress = null)
        {
            var bitfield = new BitArray(_metadata.PieceCount);
            var verified = 0;
            var processed = 0;
            var pieceCount = _metadata.PieceCount;

            Logger.LogInfo($"[Storage] Проверка {pieceCount} кусков (параллельно)...");

            // Ограничиваем параллелизм для предотвращения перегрузки диска
            var parallelism = Math.Min(Environment.ProcessorCount, 4);
            
            await Parallel.ForEachAsync(
                Enumerable.Range(0, pieceCount),
                new ParallelOptions { MaxDegreeOfParallelism = parallelism },
                async (i, ct) =>
                {
                    var pieceData = await ReadPieceAsync(i).ConfigureAwait(false);
                    if (pieceData != null && _metadata.VerifyPiece(i, pieceData))
                    {
                        bitfield[i] = true;
                        Interlocked.Increment(ref verified);
                    }

                    var current = Interlocked.Increment(ref processed);
                    if (current % 100 == 0)
                    {
                        progress?.Report((double)current / pieceCount);
                    }
                }).ConfigureAwait(false);

            Logger.LogInfo($"[Storage] Проверено {verified}/{pieceCount} кусков");
            progress?.Report(1.0);

            return bitfield;
        }

        #endregion

        #region Очистка

        /// <summary>Закрывает все открытые файлы (освобождает память)</summary>
        public async Task CloseAllFilesAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var stream in _fileStreams.Values)
                {
                    try
                    {
                        await TaskTimeoutHelper.TimeoutAsync(
                            stream.FlushAsync(),
                            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        await stream.DisposeAsync().ConfigureAwait(false);
                    }
                    catch { /* игнорируем */ }
                }
                _fileStreams.Clear();
                _fileStreamLastUsed.Clear();
                Logger.LogInfo("[Storage] Все файлы закрыты");
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region Вспомогательные методы

        private async Task<FileStream> GetOrOpenFileAsync(string path, FileMode mode, FileAccess access)
        {
            await CleanupIdleFilesAsync().ConfigureAwait(false);
            
            if (_fileStreams.TryGetValue(path, out var existing))
            {
                if (existing.CanRead == (access == FileAccess.Read || access == FileAccess.ReadWrite) &&
                    existing.CanWrite == (access == FileAccess.Write || access == FileAccess.ReadWrite))
                {
                    // Обновляем время последнего использования
                    _fileStreamLastUsed[path] = DateTime.UtcNow;
                    return existing;
                }
                await existing.DisposeAsync().ConfigureAwait(false);
                _fileStreams.Remove(path);
                _fileStreamLastUsed.Remove(path);
            }

            // КРИТИЧНО: Если достигнут лимит открытых файлов, закрываем самые старые
            if (_fileStreams.Count >= MaxOpenFiles)
            {
                await CloseOldestFilesAsync(MaxOpenFiles - 10).ConfigureAwait(false);
            }

            var stream = new FileStream(path, mode, access, FileShare.ReadWrite, 65536, true);
            _fileStreams[path] = stream;
            _fileStreamLastUsed[path] = DateTime.UtcNow;
            return stream;
        }
        
        /// <summary>Закрывает неиспользуемые файлы (простаивающие более FileIdleTimeout)</summary>
        private async Task CleanupIdleFilesAsync()
        {
            var now = DateTime.UtcNow;
            var toClose = new List<string>();
            
            foreach (var kvp in _fileStreamLastUsed)
            {
                if (now - kvp.Value > FileIdleTimeout)
                {
                    toClose.Add(kvp.Key);
                }
            }
            
            foreach (var path in toClose)
            {
                if (_fileStreams.TryGetValue(path, out var stream))
                {
                    try
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                        _fileStreams.Remove(path);
                        _fileStreamLastUsed.Remove(path);
                    }
                    catch { /* игнорируем ошибки */ }
                }
            }
        }
        
        /// <summary>Закрывает N самых старых файлов</summary>
        private async Task CloseOldestFilesAsync(int count)
        {
            var sorted = _fileStreamLastUsed.OrderBy(kvp => kvp.Value).Take(count).ToList();
            
            foreach (var kvp in sorted)
            {
                if (_fileStreams.TryGetValue(kvp.Key, out var stream))
                {
                    try
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                        _fileStreams.Remove(kvp.Key);
                        _fileStreamLastUsed.Remove(kvp.Key);
                    }
                    catch { /* игнорируем ошибки */ }
                }
            }
        }

        #endregion

        #region IDisposable / IAsyncDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _lock.Wait();
            try
            {
                foreach (var stream in _fileStreams.Values)
                {
                    try
                    {
                        stream.Dispose();
                    }
                    catch
                    {
                        // Игнорируем ошибки при закрытии
                    }
                }
                _fileStreams.Clear();
                _fileStreamLastUsed.Clear();
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
            if (_disposed) return;
            _disposed = true;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var stream in _fileStreams.Values)
                {
                    try
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Игнорируем ошибки при закрытии
                    }
                }
                _fileStreams.Clear();
                _fileStreamLastUsed.Clear();
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
}
