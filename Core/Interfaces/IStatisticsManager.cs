namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления статистикой загрузки
    /// Принцип ISP: специфичный интерфейс только для статистики
    /// </summary>
    public interface IStatisticsManager
    {
        /// <summary>
        /// Обновляет статистику скорости загрузки
        /// </summary>
        void UpdateStatistics();

        /// <summary>
        /// Получает текущую скорость загрузки (байт/сек)
        /// </summary>
        long GetDownloadSpeed();

        /// <summary>
        /// Получает текущую скорость отдачи (байт/сек)
        /// </summary>
        long GetUploadSpeed();
    }
}

