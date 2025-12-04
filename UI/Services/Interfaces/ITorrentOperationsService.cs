namespace TorrentClient.UI.Services.Interfaces
{
    /// <summary>
    /// Интерфейс для операций с торрентами
    /// </summary>
    public interface ITorrentOperationsService
    {
        /// <summary>
        /// Добавляет торренты из файлов
        /// </summary>
        Task<AddTorrentsResult> AddTorrentsAsync(
            IEnumerable<string> torrentFilePaths,
            string? downloadPath,
            AppSettings appSettings);

        /// <summary>
        /// Удаляет торренты
        /// </summary>
        Task RemoveTorrentsAsync(IEnumerable<string> torrentIds, bool deleteFiles);

        /// <summary>
        /// Запускает торренты
        /// </summary>
        Task StartTorrentsAsync(IEnumerable<string> torrentIds);

        /// <summary>
        /// Ставит торренты на паузу
        /// </summary>
        Task PauseTorrentsAsync(IEnumerable<string> torrentIds);

        /// <summary>
        /// Останавливает торренты
        /// </summary>
        Task StopTorrentsAsync(IEnumerable<string> torrentIds);

        /// <summary>
        /// Загружает сохранённые торренты с автозапуском
        /// </summary>
        Task LoadSavedTorrentsAsync(AppSettings? appSettings);
    }
}

