using TorrentClient.Core.Interfaces;
using TorrentClient.UI.Services.Interfaces;

namespace TorrentClient.UI.Services
{
    /// <summary>
    /// Сервис для выполнения операций с торрентами
    /// Инкапсулирует бизнес-логику операций
    /// </summary>
    public class TorrentOperationsService : ITorrentOperationsService
    {
        private readonly ITorrentManager _torrentManager;
        private readonly IAppSettingsManager _settingsManager;

        public TorrentOperationsService(ITorrentManager torrentManager, IAppSettingsManager settingsManager)
        {
            _torrentManager = torrentManager ?? throw new ArgumentNullException(nameof(torrentManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        /// <summary>
        /// Добавляет торренты из файлов
        /// </summary>
        public async Task<AddTorrentsResult> AddTorrentsAsync(
            IEnumerable<string> torrentFilePaths,
            string? downloadPath,
            AppSettings appSettings)
        {
            var result = new AddTorrentsResult();
            
            if (downloadPath == null)
            {
                result.ErrorMessage = "Путь загрузки не указан";
                return result;
            }

            // Создаём папку если не существует
            if (!Directory.Exists(downloadPath))
            {
                Directory.CreateDirectory(downloadPath);
            }

            bool copyTorrentFile = appSettings?.CopyTorrentFileToDownloadFolder ?? false;
            bool autoStart = appSettings?.AutoStartOnAdd ?? false;

            foreach (var fileName in torrentFilePaths)
            {
                try
                {
                    // Парсим метаданные торрента для получения размера
                    TorrentMetadata metadata;
                    try
                    {
                        metadata = TorrentParser.Parse(fileName);
                    }
                    catch (Exception parseEx)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"{Path.GetFileName(fileName)}: Ошибка парсинга торрента - {parseEx.Message}");
                        Logger.LogError($"Ошибка парсинга торрента {Path.GetFileName(fileName)}", parseEx);
                        continue;
                    }

                    // Проверяем свободное место на диске
                    var diskCheck = DiskSpaceChecker.CheckDiskSpace(downloadPath, metadata.TotalLength);
                    if (!diskCheck.HasEnoughSpace)
                    {
                        var torrentName = metadata.Name ?? Path.GetFileName(fileName);
                        var warningMsg = $"{torrentName}: {diskCheck.WarningMessage}";
                        result.Warnings.Add(warningMsg);
                        result.SkippedCount++;
                        Logger.LogWarning($"[TorrentOperationsService] Недостаточно места на диске для {torrentName}. Требуется: {FormatBytes(diskCheck.RequiredBytes)}, доступно: {FormatBytes(diskCheck.AvailableBytes)}");
                        // Продолжаем обработку остальных торрентов, но не добавляем этот
                        continue;
                    }

                    // Копируем торрент-файл в папку загрузки если включена опция
                    string torrentFilePath = fileName;
                    if (copyTorrentFile)
                    {
                        var destPath = Path.Combine(downloadPath, Path.GetFileName(fileName));
                        if (!File.Exists(destPath))
                        {
                            File.Copy(fileName, destPath);
                            torrentFilePath = destPath;
                        }
                        else if (fileName != destPath)
                        {
                            torrentFilePath = destPath;
                        }
                    }

                    var torrent = _torrentManager.AddTorrent(torrentFilePath, downloadPath);
                    if (torrent != null)
                    {
                        result.AddedTorrentIds.Add(torrent.Id);
                        result.AddedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"{Path.GetFileName(fileName)}: {ex.Message}");
                    Logger.LogError($"Ошибка при добавлении торрента {Path.GetFileName(fileName)}", ex);
                }
            }

            // Автоматически запускаем добавленные торренты, если включена опция
            if (autoStart && result.AddedTorrentIds.Count > 0)
            {
                try
                {
                    await Task.Delay(500); // Небольшая задержка для завершения инициализации
                    
                    var startTasks = result.AddedTorrentIds.Select(id => _torrentManager.StartTorrentAsync(id));
                    await Task.WhenAll(startTasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[TorrentOperationsService] Ошибка автозапуска торрентов", ex);
                    result.Warnings.Add($"Ошибка автозапуска: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Удаляет торренты
        /// </summary>
        public async Task RemoveTorrentsAsync(IEnumerable<string> torrentIds, bool deleteFiles)
        {
            await _torrentManager.RemoveTorrentsAsync(torrentIds, deleteFiles).ConfigureAwait(false);
        }

        /// <summary>
        /// Запускает торренты
        /// </summary>
        public async Task StartTorrentsAsync(IEnumerable<string> torrentIds)
        {
            var tasks = torrentIds.Select(id => _torrentManager.StartTorrentAsync(id));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Ставит торренты на паузу
        /// </summary>
        public async Task PauseTorrentsAsync(IEnumerable<string> torrentIds)
        {
            var tasks = torrentIds.Select(id => _torrentManager.PauseTorrentAsync(id));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Останавливает торренты
        /// </summary>
        public async Task StopTorrentsAsync(IEnumerable<string> torrentIds)
        {
            var tasks = torrentIds.Select(id => _torrentManager.StopTorrentAsync(id));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Загружает сохранённые торренты с автозапуском
        /// </summary>
        public async Task LoadSavedTorrentsAsync(AppSettings? appSettings)
        {
            try
            {
                await Task.Run(() => _torrentManager.LoadSavedTorrents()).ConfigureAwait(false);

                // Автозапуск торрентов при открытии приложения
                if (appSettings?.AutoStartOnLaunch == true)
                {
                    const int initializationDelayMs = 2000;
                    await Task.Delay(initializationDelayMs).ConfigureAwait(false);

                    var torrents = await Task.Run(() => _torrentManager.GetAllTorrents()).ConfigureAwait(false);
                    var startTasks = torrents
                        .Where(t => t.State == TorrentState.Stopped || t.State == TorrentState.Paused)
                        .Select(t => _torrentManager.StartTorrentAsync(t.Id));

                    if (startTasks.Any())
                    {
                        await Task.WhenAll(startTasks).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[TorrentOperationsService] Ошибка загрузки торрентов", ex);
                throw;
            }
        }

        /// <summary>
        /// Форматирует байты в читаемый формат (вспомогательный метод для логирования)
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            var sizes = new[] { "B", "KB", "MB", "GB", "TB" };
            var len = (double)bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Результат добавления торрентов
    /// </summary>
    public class AddTorrentsResult
    {
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> AddedTorrentIds { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
        public string? ErrorMessage { get; set; }

        public bool HasErrors => Errors.Count > 0 || !string.IsNullOrEmpty(ErrorMessage);
        public bool HasWarnings => Warnings.Count > 0;
        public bool IsSuccess => AddedCount > 0 && FailedCount == 0 && !HasWarnings;
    }
}

