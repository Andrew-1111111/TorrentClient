namespace TorrentClient.UI.Interfaces
{
    /// <summary>
    /// Интерфейс для управления троттлингом обновлений UI
    /// </summary>
    public interface IUpdateThrottler
    {
        /// <summary>Проверяет, можно ли обновить торрент (троттлинг)</summary>
        bool CanUpdate(string torrentId);
        
        /// <summary>Отмечает время обновления торрента</summary>
        void MarkUpdated(string torrentId);
        
        /// <summary>Очищает устаревшие записи</summary>
        void CleanupStaleEntries(IEnumerable<string> existingTorrentIds);
    }
}

